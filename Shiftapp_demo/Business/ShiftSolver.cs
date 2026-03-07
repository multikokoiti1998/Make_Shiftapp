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
            // ========= 0) 基本データ =========
            int daysCount = (_lastDate - _firstDate).Days + 1;
            var dates = Enumerable.Range(0, daysCount).Select(i => _firstDate.AddDays(i).Date).ToList();
            int numEmp = _employees.Count;

            bool IsWeekday(DateTime dt)
                => dt.DayOfWeek >= DayOfWeek.Monday && dt.DayOfWeek <= DayOfWeek.Friday && !_holidays.Contains(dt.Date);

            // 祝日集合（外から注入 or DB or CSV等で用意しておく想定）
            bool IsHoliday(DateTime dt) => _holidays.Contains(dt.Date) || dt.DayOfWeek==DayOfWeek.Sunday;

            // 代休付与オフセット（日付→何日後か）
            // 優先順位：祝日ルール最優先
            int? SubOffOffset(DateTime dutyDate)
            {
                if (IsHoliday(dutyDate)) return 3; // 祝日なら3日後
                return dutyDate.DayOfWeek switch
                {
                    DayOfWeek.Sunday => 2,   // 日曜当直 → 火曜代休
                    DayOfWeek.Saturday => 2, // 土曜当直 → 月曜代休
                    DayOfWeek.Friday => 5,   // 金曜当直 → 水曜代休
                    _ => (int?)null
                };


            }

            // 「次の平日」を返す（インデックス）
            int NextWeekdayIndex(int start)
            {
                for (int k = start; k < daysCount; k++)
                    if (IsWeekday(dates[k])) return k;
                return -1;
            }

            // ========= 1) モデル & 変数 =========
            var model = new CpModel();

            // x[e,d] : 当直
            var x = new BoolVar[numEmp, daysCount];
            // a[e,d] : 明け（当直翌日）
            var a = new BoolVar[numEmp, daysCount];
            // s[e,d] : 代休（1日目）
            var s = new BoolVar[numEmp, daysCount];
            // s2[e,d] : 代休（2日目：祝日当直＆明けも祝日のとき）
            var s2 = new BoolVar[numEmp, daysCount];

            // ========= 2) 変数作成 + 事前フィルタ（確定情報から禁止） =========
            for (int e = 0; e < numEmp; e++)
            {
                for (int d = 0; d < daysCount; d++)
                {
                    x[e, d] = model.NewBoolVar($"duty_{e}_{d}");
                    a[e, d] = model.NewBoolVar($"after_{e}_{d}");
                    s[e, d] = model.NewBoolVar($"suboff_{e}_{d}");
                    s2[e, d] = model.NewBoolVar($"suboff2_{e}_{d}");

                    var currentDate = dates[d];
                    var emp = _employees[e];

                    // 既存が公休/代休なら当直禁止（例）
                    if (_existingMap.TryGetValue((emp.EmployeeId, currentDate), out int stid))
                    {
                        if (stid == _stidOff || stid == _stidSubstituteOff)
                            model.Add(x[e, d] == 0);
                    }

                    // 土曜班が違うなら当直禁止（例）
                    if (currentDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        string wkClass = GetWorkingClass(currentDate);
                        if (!string.IsNullOrEmpty(emp.SaturdayClass) &&
                            !emp.SaturdayClass.Equals(wkClass, StringComparison.OrdinalIgnoreCase))
                        {
                            model.Add(x[e, d] == 0);
                        }
                    }

                    // 代休は必ず平日（週末は0固定）
                    if (!IsWeekday(currentDate))
                    {
                        model.Add(s[e, d] == 0);
                        model.Add(s2[e, d] == 0);
                    }
                }
            }

            // ========= 3) 当直人数制約（毎日：カテ可1 + カテ不可1） =========
            for (int d = 0; d < daysCount; d++)
            {
                var cath = new List<BoolVar>();
                var nonCath = new List<BoolVar>();

                for (int e = 0; e < numEmp; e++)
                {
                    if (_employees[e].CanDoCatheterization) cath.Add(x[e, d]);
                    else nonCath.Add(x[e, d]);
                }
                // カテ可から1人、カテ不可から1人の合計2人が必要
                model.Add(LinearExpr.Sum(cath) == 1);
            
                model.Add(LinearExpr.Sum(nonCath) == 1);
            }

            // ========= 4) 当直→明け（翌日） =========
            for (int e = 0; e < numEmp; e++)
            {
                // 最終日に当直を置くと明けが期間外になるので禁止（運用により調整）
                model.Add(x[e, daysCount - 1] == 0);

                // 前月持ち越しを考えないなら初日は明け0固定
                model.Add(a[e, 0] == 0);

                for (int d = 0; d < daysCount - 1; d++)
                {
                    model.Add(a[e, d + 1] == x[e, d]);
                }
            }

            // ========= 5) 排他（当直・明け・代休が同日に重ならない） =========
            for (int e = 0; e < numEmp; e++)
            {
                for (int d = 0; d < daysCount; d++)
                {
                    model.Add(x[e, d] + a[e, d] + s[e, d] + s2[e, d] <= 1);
                }
            }

            // ========= 6) 当直は最低3日はあける（4日窓に当直は最大1回） =========
            const int MinDutyGapDays = 3;
            for (int e = 0; e < numEmp; e++)
            {
                for (int d = 0; d < daysCount - MinDutyGapDays; d++)
                {
                    var window = new List<BoolVar>();
                    for (int i = 0; i <= MinDutyGapDays; i++)
                        window.Add(x[e, d + i]);

                    model.Add(LinearExpr.Sum(window) <= 1);
                }
            }

            // ========= 7) 代休(1日目)をルールで固定 =========
            for (int e = 0; e < numEmp; e++)
            {
                for (int d = 0; d < daysCount; d++)
                {
                    int? off = SubOffOffset(dates[d]);
                    if (off is null) continue; // ルール対象外日は何もしない（必要なら別ルールを足す）

                    int t1 = d + off.Value; // 代休(1日目)のインデックス

                    // 期間外 or 平日でない → その当直を禁止
                    if (t1 < 0 || t1 >= daysCount || !IsWeekday(dates[t1]))
                    {
                        model.Add(x[e, d] == 0);
                        continue;
                    }

                    // 当直(d) ↔ 代休(t1) を同値にして固定
                    model.Add(s[e, t1] == x[e, d]);

                    // 代休の日は当直/明けにしない（当直が立ったときだけ強制）
                    model.Add(x[e, t1] == 0).OnlyEnforceIf(x[e, d]);
                    model.Add(a[e, t1] == 0).OnlyEnforceIf(x[e, d]);
                }
            }

            // ========= 8) 祝日当直 & 明けも祝日 → 代休(2日目)を付与 =========
            for (int e = 0; e < numEmp; e++)
            {
                for (int d = 0; d < daysCount - 1; d++) // d+1を見るので -1
                {
                    // 条件：当直日が祝日 && 明け日（翌日）も祝日
                    bool needsExtra = IsHoliday(dates[d]) && IsHoliday(dates[d + 1]);
                    if (!needsExtra) continue;

                    int? off = SubOffOffset(dates[d]);
                    if (off is null) continue;

                    int t1 = d + off.Value;
                    if (t1 < 0 || t1 >= daysCount || !IsWeekday(dates[t1]))
                    {
                        model.Add(x[e, d] == 0);
                        continue;
                    }

                    // 2日目は「t1 の次の平日」
                    int t2 = NextWeekdayIndex(t1 + 1);
                    if (t2 < 0)
                    {
                        model.Add(x[e, d] == 0);
                        continue;
                    }

                    // 当直(d) ↔ 追加代休(t2) を同値で固定
                    model.Add(s2[e, t2] == x[e, d]);

                    // 追加代休の日も当直/明けにしない（当直が立ったときだけ強制）
                    model.Add(x[e, t2] == 0).OnlyEnforceIf(x[e, d]);
                    model.Add(a[e, t2] == 0).OnlyEnforceIf(x[e, d]);
                }
            }

            // ========= 9) 目的関数：当直回数の平準化（max-min最小） =========
            int upper = Math.Max(31, daysCount); // 安全側
            var minD = model.NewIntVar(0, upper, "minDuty");
            var maxD = model.NewIntVar(0, upper, "maxDuty");

            for (int e = 0; e < numEmp; e++)
            {
                var dutyVars = Enumerable.Range(0, daysCount).Select(d => x[e, d]).ToArray();
                var dutySum = LinearExpr.Sum(dutyVars);
                model.Add(dutySum >= minD);
                model.Add(dutySum <= maxD);
            }
            model.Minimize(maxD - minD);

            // ========= 10) Solve =========
            var solver = new CpSolver();
            var status = solver.Solve(model);

            if (status != CpSolverStatus.Optimal && status != CpSolverStatus.Feasible)
                throw new InvalidOperationException("シフト生成不可: 条件が厳しすぎます。");

            // ========= 11) 結果抽出（当直だけ返す。明け/代休も欲しければ同様に拾える） =========
            var result = new List<(int EmployeeId, DateTime Date)>();
            for (int d = 0; d < daysCount; d++)
            {
                for (int e = 0; e < numEmp; e++)
                {
                    if (solver.Value(x[e, d]) == 1)
                        result.Add((_employees[e].EmployeeId, dates[d]));
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



