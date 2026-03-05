using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using static Shiftapp_demo.DataAccess.MainDatabaseHelper;
using Google.OrTools.Sat;

namespace Shiftapp_demo.Business
{
    internal class ShiftsSolver
    {
        private readonly DateTime _firstDate;
        private readonly DateTime _lastDate;
        private readonly List<Employee> _employees;
        private readonly Dictionary<(int, DateTime), int> _existingMap;
        private readonly List<DateTime> _holidays;

        // --- シフトID設定 (コンストラクタで受け取る) ---
        private readonly int _stidDuty;
        private readonly int _stidAfterDuty;
        private readonly int _stidSubstituteOff;
        private readonly int _stidOff;
        private const int MinDutyGapDays = 3;

        //コンストラクタで必要な情報を受け取る
        public ShiftsSolver(DateTime month,
            List<Employee> employees,
            Dictionary<(int, DateTime), int> existingMap,
            List<DateTime> holidays,
            int stidDuty, int stidAfterDuty, int stidSubOff, int stidOff)
        {
            _firstDate = new DateTime(month.Year, month.Month, 1);
            _lastDate = _firstDate.AddMonths(1).AddDays(-1);
            _employees = employees;
            _existingMap = existingMap;
            _holidays = holidays;
            _stidDuty = stidDuty;
            _stidAfterDuty = stidAfterDuty;
            _stidSubstituteOff = stidSubOff;
            _stidOff = stidOff;
        }

        public List<ShiftWrite> Solve()
        {
            // 1. 最適化計算を実行
            var assignments = RunOptimization();

            // 2. 結果をShiftWriteに変換（明け・代休付与）
            return ConvertToShiftWrites(assignments);
        }


        private List<(int EmployeeId, DateTime Date)> RunOptimization()
        {
            int daysCount = (_lastDate - _firstDate).Days + 1;
            //日付をインデックス化
            var dates = Enumerable.Range(0, daysCount).Select(i => _firstDate.AddDays(i)).ToList();
            int numEmp = _employees.Count;

            var model = new CpModel();
            var x = new BoolVar[numEmp, daysCount];

            // A. 変数定義 & 事前フィルタリング
            for (int e = 0; e < numEmp; e++)
            {
                for (int d = 0; d < daysCount; d++)
                {
                    x[e, d] = model.NewBoolVar($"duty_{e}_{d}");
                    var currentDate = dates[d];
                    var emp = _employees[e];

                    if (_existingMap.TryGetValue((emp.EmployeeId, currentDate), out int stid))
                    {
                        // 既に休み等が入っている場合は当直禁止
                        // ※ stidOff(公休)や stidSubstituteOff(代休) が入っていたらNGとする例
                        if (stid == _stidOff || stid == _stidSubstituteOff)
                        {
                            model.Add(x[e, d] == 0);
                        }
                    }

                    // (2) 土曜班の確認
                    if (currentDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        string wkClass = GetWorkingClass(currentDate); 
                        if (!string.IsNullOrEmpty(emp.SaturdayClass) &&
                            !emp.SaturdayClass.Equals(wkClass, StringComparison.OrdinalIgnoreCase))
                        {
                            model.Add(x[e, d] == 0);
                        }
                    }
                }
            }

            // B. 制約条件
            for (int d = 0; d < daysCount; d++)
            {
                // 人数制約: カテ可1名 + カテ不可1名
                var cath = new List<BoolVar>();
                var nonCath = new List<BoolVar>();
                for (int e = 0; e < numEmp; e++)
                {
                    if (_employees[e].CanDoCatheterization) cath.Add(x[e, d]);
                    else nonCath.Add(x[e, d]);
                }
                model.Add(LinearExpr.Sum(cath) == 1);
                model.Add(LinearExpr.Sum(nonCath) == 1);
            }

            // 勤務間隔制約
            for (int e = 0; e < numEmp; e++)
            {
                for (int d = 0; d < daysCount - MinDutyGapDays; d++)
                {
                    var window = new List<BoolVar>();
                    for (int i = 0; i <= MinDutyGapDays; i++) window.Add(x[e, d + i]);
                    model.Add(LinearExpr.Sum(window) <= 1);
                }
            }

            // C. 目的関数 (平準化)
            var minD = model.NewIntVar(0, 31, "min");
            var maxD = model.NewIntVar(0, 31, "max");
            for (int e = 0; e < numEmp; e++)
            {
                var sums = new List<BoolVar>();
                for (int d = 0; d < daysCount; d++) sums.Add(x[e, d]);
                model.Add(LinearExpr.Sum(sums) >= minD);
                model.Add(LinearExpr.Sum(sums) <= maxD);
            }
            model.Minimize(maxD - minD);

            // D. 実行
            var solver = new CpSolver();
            var status = solver.Solve(model);

            if (status != CpSolverStatus.Optimal && status != CpSolverStatus.Feasible)
                throw new InvalidOperationException("シフト生成不可: 条件が厳しすぎます。");

            // E. 結果抽出
            var result = new List<(int, DateTime)>();
            for (int d = 0; d < daysCount; d++)
            {
                for (int e = 0; e < numEmp; e++)
                {
                    if (solver.Value(x[e, d]) == 1)
                    {
                        result.Add((_employees[e].EmployeeId, dates[d]));
                    }
                }
            }
            return result;
        }

        private List<ShiftWrite> ConvertToShiftWrites(List<(int eid, DateTime date)> assignments)
        {
            var upserts = new List<ShiftWrite>();
            // 判定用に一時的なマップを作る（同じ処理内で重複チェックするため）
            var tempMap = new Dictionary<(int, DateTime), int>(_existingMap);

            foreach (var (eid, dutyDate) in assignments)
            {
                // 1. 当直
                AddShift(tempMap, upserts, eid, dutyDate, _stidDuty);

                // 2. 明け
                AddShift(tempMap, upserts, eid, dutyDate.AddDays(1), _stidAfterDuty);

                // 3. 代休 (Helperロジックを利用)
                var compDate = GetCompWorkOff(dutyDate, _holidays);
                if (compDate.HasValue)
                {
                    // 代休セット（originDutyDate付き）
                    // ※ここで ShiftBusiness にあった SetChildWithOrigin 相当の処理を行う
                    var key = (eid, compDate.Value);

                    // 重複チェック（既に当直などが入っていないか簡易チェック）
                    if (!tempMap.TryGetValue(key, out int current) || current != _stidDuty)
                    {
                        tempMap[key] = _stidSubstituteOff;
                        upserts.Add(new ShiftWrite(eid, compDate.Value, _stidSubstituteOff)
                        {
                            originDutyDate = dutyDate
                        });
                    }
                }
            }
            return upserts;
        }

        // --- Helper Methods (内部利用) ---

        private void AddShift(Dictionary<(int, DateTime), int> map, List<ShiftWrite> upserts, int eid, DateTime date, int stid)
        {
            var key = (eid, date);
            map[key] = stid;
            upserts.Add(new ShiftWrite(eid, date, stid));
        }

        // ※ ShiftBusinessにあったロジックを持ってくる
        private DateTime? GetCompWorkOff(DateTime dutyDay, List<DateTime> holidays)
        {
            // (省略: 先ほど統合した GetCompWorkOff の中身をここにペーストするか、
            //  ShiftBusiness.GetCompWorkOff を public static にして呼ぶ)
            //  ここでは省略します
            return null; // ダミー
        }

        // ※ 土曜班判定ロジックも必要ならここにコピー
        private string GetWorkingClass(DateTime date) { return "A"; /*ダミー*/ }
    }




}



