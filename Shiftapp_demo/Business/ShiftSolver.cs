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
        private readonly bool _baselineIsA;
        private readonly Dictionary<int, List<EmployeePreference>>? _preferencesByEmployee;

        // --- シフトID設定 (コンストラクタで受け取る) ---
        private readonly int _stidDuty;
        private readonly int _stidAfterDuty;
        private readonly int _stidSubstituteOff;
        private readonly int _stidOff;
        private readonly int _stidDayWork;

        private const int MinDutyGapDays = 3;

        // 公平性（当直回数の平準化）を希望充足より優先させるための重み。
        // 公平性が同点となる解が複数あるときに限り、希望のペナルティで決着させる想定。
        private const int FairnessWeight = 100;

        //コンストラクタで必要な情報を受け取る
        public ShiftsSolver(DateTime month,
            List<Employee> employees,
            Dictionary<(int, DateTime), int> existingMap,
            List<DateTime> holidays,
            int stidDuty, int stidAfterDuty, int stidSubOff, int stidOff, int stidDayWork,
            bool baselineIsA = false,
            Dictionary<int, List<EmployeePreference>>? preferencesByEmployee = null)
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
            _stidDayWork = stidDayWork;
            _baselineIsA = baselineIsA;
            _preferencesByEmployee = preferencesByEmployee;
        }

        public List<ShiftWrite> Solve()
        {
            // 1. 最適化計算を実行
            var assignments = RunOptimization();

            // 2. 結果をShiftWriteに変換（明け・代休・日勤付与）
            return ConvertToShiftWrites(assignments);
        }

        private (List<(int EmployeeId, DateTime Date)> Duties, List<(int EmployeeId, DateTime Date)> DayWorks) RunOptimization()
        {
            // ========= 0) 基本データ =========
            int daysCount = (_lastDate - _firstDate).Days + 1;
            var dates = Enumerable.Range(0, daysCount).Select(i => _firstDate.AddDays(i).Date).ToList();
            int numEmp = _employees.Count;

            bool IsWeekday(DateTime dt)
                => dt.DayOfWeek >= DayOfWeek.Monday && dt.DayOfWeek <= DayOfWeek.Friday && !_holidays.Contains(dt.Date);

            // dates配列内のインデックスに変換（範囲外はnull）
            int? ToIndex(DateTime d)
            {
                var diff = (d.Date - _firstDate).Days;
                return (diff >= 0 && diff < daysCount) ? diff : (int?)null;
            }

            // ========= 1) モデル & 変数 =========
            var model = new CpModel();

            // x[e,d] : 当直
            var x = new BoolVar[numEmp, daysCount];
            // a[e,d] : 明け（当直翌日）
            var a = new BoolVar[numEmp, daysCount];
            // s[e,d] : 代休
            var s = new BoolVar[numEmp, daysCount];
            // w[e,d] : 日勤（日曜/祝日のみ）
            var w = new BoolVar[numEmp, daysCount];

            // ========= 2) 変数作成 + 事前フィルタ（確定情報から禁止） =========
            for (int e = 0; e < numEmp; e++)
            {
                var emp = _employees[e];

                for (int d = 0; d < daysCount; d++)
                {
                    x[e, d] = model.NewBoolVar($"duty_{e}_{d}");
                    a[e, d] = model.NewBoolVar($"after_{e}_{d}");
                    s[e, d] = model.NewBoolVar($"suboff_{e}_{d}");
                    w[e, d] = model.NewBoolVar($"daywork_{e}_{d}");

                    var currentDate = dates[d];

                    // 既存が公休/代休なら当直禁止（例）
                    if (_existingMap.TryGetValue((emp.EmployeeId, currentDate), out int stid))
                    {
                        if (stid == _stidOff || stid == _stidSubstituteOff)
                            model.Add(x[e, d] == 0);
                    }

                    // 土曜班が違うなら当直禁止（例）
                    if (currentDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        string wkClass = ShiftBusiness.GetWorkingClass(currentDate, _baselineIsA);
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
                    }

                    // 日勤は日曜/祝日のみ、かつ日勤対応可能な職員のみ
                    bool dayWorkApplicable = currentDate.DayOfWeek == DayOfWeek.Sunday || _holidays.Contains(currentDate.Date);
                    if (!dayWorkApplicable || !emp.CanDayDuty)
                    {
                        model.Add(w[e, d] == 0);
                    }
                    else if (currentDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        // 日曜の日勤は「その週末に出勤していない班」のみ対象
                        // （ShiftBusiness.FilterBySaturdayの休み班フィルタに相当。ハード制約のため
                        //  FilterBySaturdayのような「全員0件ならフォールバック」は行わない簡略化）
                        string workingClass = ShiftBusiness.GetWorkingClass(currentDate, _baselineIsA);
                        if (!string.IsNullOrEmpty(emp.SaturdayClass) &&
                            emp.SaturdayClass.Equals(workingClass, StringComparison.OrdinalIgnoreCase))
                        {
                            model.Add(w[e, d] == 0);
                        }
                    }
                }
            }

            // ========= 3) 当直・日勤の人数制約（毎日：当直=カテ可1+カテ不可1、日勤は0～1） =========
            for (int d = 0; d < daysCount; d++)
            {
                var cath = new List<BoolVar>();
                var nonCath = new List<BoolVar>();
                var dayWorkVars = new List<BoolVar>();

                for (int e = 0; e < numEmp; e++)
                {
                    if (_employees[e].CanDoCatheterization) cath.Add(x[e, d]);
                    else nonCath.Add(x[e, d]);

                    dayWorkVars.Add(w[e, d]);
                }
                // カテ可から1人、カテ不可から1人の合計2人が必要
                model.Add(LinearExpr.Sum(cath) == 1);
                model.Add(LinearExpr.Sum(nonCath) == 1);

                // 日勤はヒューリスティックのcand3/cand4同様、該当者がいなくても良い（<=1、必須ではない）
                model.Add(LinearExpr.Sum(dayWorkVars) <= 1);
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

            // ========= 5) 排他（当直・明け・代休・日勤が同日に重ならない） =========
            for (int e = 0; e < numEmp; e++)
            {
                for (int d = 0; d < daysCount; d++)
                {
                    model.Add(x[e, d] + a[e, d] + s[e, d] + w[e, d] <= 1);
                }
            }

            // ========= 6) 当直は最低3日はあける（4日窓に当直は最大1回） =========
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

            // ========= 7) 代休を ShiftBusiness.GetCompWorkOff と同一ルールで固定 =========
            // 代休先の日付計算は ConvertToShiftWrites 側とまったく同じ ShiftBusiness.GetCompWorkOff を使う。
            // こうすることでモデルが確保する日と実際にDBへ書き込む代休日が食い違わないようにする
            // （以前は独自のSubOffOffsetという簡易ルールでモデル内だけ別計算していたため、
            //  ダミーのGetCompWorkOffを本物に差し替えただけでは日付が食い違う恐れがあった）。
            // 同月内に有効な代休日が無い場合（ShiftBusiness.GetCompWorkOffがnullを返す、または
            // 翌月に出てモデルの日付範囲外になる場合）は、ヒューリスティックと同様に当直自体は禁止しない。
            for (int d = 0; d < daysCount; d++)
            {
                var compDate = ShiftBusiness.GetCompWorkOff(dates[d], _holidays);
                if (compDate is null) continue;

                var t1 = ToIndex(compDate.Value);
                if (t1 is null) continue;

                for (int e = 0; e < numEmp; e++)
                {
                    // 当直(d) ↔ 代休(t1) を同値にして固定
                    model.Add(s[e, t1.Value] == x[e, d]);

                    // 代休の日は当直/明けにしない（当直が立ったときだけ強制）
                    model.Add(x[e, t1.Value] == 0).OnlyEnforceIf(x[e, d]);
                    model.Add(a[e, t1.Value] == 0).OnlyEnforceIf(x[e, d]);
                }
            }

            // ========= 8) 目的関数：当直回数の平準化（max-min最小） + 希望のペナルティ =========
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

            LinearExpr objective = FairnessWeight * (maxD - minD);

            // 希望はハード制約にせず、必ず目的関数のペナルティ項としてのみ組み込む
            // （こうすることで希望がどれだけ厳しくてもINFEASIBLEにはならない）。
            // Avoid: 割当自体(x[e,d])がそのままペナルティ。Prefer: 割当にマイナスの重み＝ボーナス。
            if (_preferencesByEmployee != null)
            {
                var literals = new List<BoolVar>();
                var weights = new List<int>();

                for (int e = 0; e < numEmp; e++)
                {
                    if (!_preferencesByEmployee.TryGetValue(_employees[e].EmployeeId, out var prefs) || prefs.Count == 0)
                        continue;

                    for (int d = 0; d < daysCount; d++)
                    {
                        var dow = dates[d].DayOfWeek;
                        bool isWeekendDay = dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday;

                        foreach (var p in prefs)
                        {
                            bool matches = p.IsWeekend ? isWeekendDay : p.DayOfWeek == dow;
                            if (!matches) continue;

                            int sign = p.Polarity == PreferencePolarity.Avoid ? 1 : -1;
                            literals.Add(x[e, d]);
                            weights.Add(sign * p.Weight);
                        }
                    }
                }

                if (literals.Count > 0)
                {
                    objective += LinearExpr.WeightedSum(literals, weights);
                }
            }

            model.Minimize(objective);

            // ========= 9) Solve =========
            var solver = new CpSolver();
            var status = solver.Solve(model);

            if (status != CpSolverStatus.Optimal && status != CpSolverStatus.Feasible)
                throw new InvalidOperationException("シフト生成不可: 条件が厳しすぎます。");

            // ========= 10) 結果抽出 =========
            var duties = new List<(int EmployeeId, DateTime Date)>();
            var dayWorks = new List<(int EmployeeId, DateTime Date)>();
            for (int d = 0; d < daysCount; d++)
            {
                for (int e = 0; e < numEmp; e++)
                {
                    if (solver.Value(x[e, d]) == 1)
                        duties.Add((_employees[e].EmployeeId, dates[d]));
                    if (solver.Value(w[e, d]) == 1)
                        dayWorks.Add((_employees[e].EmployeeId, dates[d]));
                }
            }
            return (duties, dayWorks);
        }

        private List<ShiftWrite> ConvertToShiftWrites(
            (List<(int EmployeeId, DateTime Date)> Duties, List<(int EmployeeId, DateTime Date)> DayWorks) assignments)
        {
            var upserts = new List<ShiftWrite>();
            // 判定用に一時的なマップを作る（同じ処理内で重複チェックするため）
            var tempMap = new Dictionary<(int, DateTime), int>(_existingMap);

            foreach (var (eid, dutyDate) in assignments.Duties)
            {
                // 1. 当直
                AddShift(tempMap, upserts, eid, dutyDate, _stidDuty);

                // 2. 明け
                AddShift(tempMap, upserts, eid, dutyDate.AddDays(1), _stidAfterDuty);

                // 3. 代休（モデルのセクション7と同じ ShiftBusiness.GetCompWorkOff を使用。単一の真実源）
                var compDate = ShiftBusiness.GetCompWorkOff(dutyDate, _holidays);
                if (compDate.HasValue)
                {
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

            // 4. 日勤（明け・代休の子は付与しない）
            foreach (var (eid, date) in assignments.DayWorks)
            {
                AddShift(tempMap, upserts, eid, date, _stidDayWork);
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
    }
}
