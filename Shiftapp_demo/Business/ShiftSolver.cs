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

        // 1日に集中する代休の上限人数。超えた分は近くの空いている平日にずらす
        // （連休明けなど、複数の当直日/日勤日の代休が同じ日に重なって出勤者が極端に減るのを防ぐ）。
        private const int MaxCompOffPerDay = 3;

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

                    // 既存シフトの有無を確認（当直・日勤どちらの禁止判定にも使う）
                    bool hasExistingOffOrSubOff = false;
                    if (_existingMap.TryGetValue((emp.EmployeeId, currentDate), out int stid))
                    {
                        hasExistingOffOrSubOff = stid == _stidOff || stid == _stidSubstituteOff;
                    }

                    // 既存が公休/代休なら当直禁止（例）
                    if (hasExistingOffOrSubOff)
                    {
                        model.Add(x[e, d] == 0);
                    }

                    // 当直対応不可の職員は当直そのものを禁止する。
                    // これが無いと、セクション3のカテ可/不可グルーピングがCanDoCatheterizationのみで
                    // 行われているため、日勤専任(CanDoNightDuty=false)の職員が誤って当直に選ばれ得た。
                    if (!emp.CanDoNightDuty)
                    {
                        model.Add(x[e, d] == 0);
                    }

                    // 土曜当直は必ず「その週の土曜出勤班」から選ぶ（出勤班でないなら当直禁止）
                    if (currentDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        string wkClass = ShiftBusiness.GetWorkingClass(currentDate, _baselineIsA);
                        if (!string.IsNullOrEmpty(emp.SaturdayClass) &&
                            !emp.SaturdayClass.Equals(wkClass, StringComparison.OrdinalIgnoreCase))
                        {
                            model.Add(x[e, d] == 0);
                        }
                    }

                    // 金曜当直は必ず「翌日の土曜が休みの班」から選ぶ（出勤班なら当直禁止）。
                    // 明け(土曜)がその職員にとって元々休みの日と一致するようにするための制約。
                    if (currentDate.DayOfWeek == DayOfWeek.Friday)
                    {
                        var followingSaturday = currentDate.AddDays(1);
                        string wkClassNextSat = ShiftBusiness.GetWorkingClass(followingSaturday, _baselineIsA);
                        if (!string.IsNullOrEmpty(emp.SaturdayClass) &&
                            emp.SaturdayClass.Equals(wkClassNextSat, StringComparison.OrdinalIgnoreCase))
                        {
                            model.Add(x[e, d] == 0);
                        }
                    }

                    // 代休は必ず平日（週末は0固定）
                    if (!IsWeekday(currentDate))
                    {
                        model.Add(s[e, d] == 0);
                    }

                    // 日勤は日曜/祝日のみ、かつ日勤対応可能でカテ不可の職員のみ
                    // （ShiftBusiness.GetActiveEmployeesWithDayDutyClassの
                    //  "CanDoCatheterization==0 and CanDoDayduty==1" と同じ基準に統一）。
                    // 既に公休/代休が入っている日も対象外にする（当直と同じ既存シフト保護ルール）。
                    bool dayWorkApplicable = currentDate.DayOfWeek == DayOfWeek.Sunday || _holidays.Contains(currentDate.Date);
                    if (!dayWorkApplicable || !emp.CanDayDuty || emp.CanDoCatheterization || hasExistingOffOrSubOff)
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
                var dayWorkVars = new List<BoolVar>();
                for (int e = 0; e < numEmp; e++) dayWorkVars.Add(w[e, d]);

                // 月最終日はセクション4で全員 x=0 に固定されるため、その日も当直必須(==1)にすると
                // 必ずINFEASIBLEになる。最終日は当直なし（要運用調整）とし、日勤のみ対象とする。
                if (d == daysCount - 1)
                {
                    model.Add(LinearExpr.Sum(dayWorkVars) <= 1);
                    continue;
                }

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

            // ========= 6b) 日勤は同一職員につき月1回までとする =========
            // 日勤対応可能な職員が少ないと同じ人物に偏りがちなため（連続日はもちろん、
            // 月内で離れた日でも複数回になるのは避けたいという運用要望）、
            // 職員ごとに月内の日勤回数の合計を1回までに制限する。
            // ハード制約だが日勤自体はsum<=1の任意割当（section 3）なので、
            // 該当者が足りない日は単に日勤なしになるだけでINFEASIBLEにはならない。
            for (int e = 0; e < numEmp; e++)
            {
                var monthlyDayWorkVars = Enumerable.Range(0, daysCount).Select(d => w[e, d]).ToArray();
                model.Add(LinearExpr.Sum(monthlyDayWorkVars) <= 1);
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
                var t1 = compDate.HasValue ? ToIndex(compDate.Value) : null;

                // 二重代休（明けも祝日で本来の祝日を1日つぶしてしまうケース）の対象日は
                // 職員に依存しない純粋なカレンダー判定のため、ループの外で1回だけ計算する。
                // ShiftBusiness.AkeAlsoLandsOnHoliday/GetExtraCompWorkOffForRestfulAkeは
                // ConvertToShiftWrites側の実際のDB書き込みと同一関数（単一の真実源）。
                // 金曜当直は「翌土曜が休みの班」から選ぶよう既に固定しているため、金曜自体が祝日でも
                // 二重代休の対象外とする（金曜ルールの通常の代休＝次の水曜1日で足りるため）。
                int? t2 = null;
                if (dates[d].DayOfWeek != DayOfWeek.Friday && ShiftBusiness.AkeAlsoLandsOnHoliday(dates[d], _holidays))
                {
                    var extraCompDate = ShiftBusiness.GetExtraCompWorkOffForRestfulAke(dates[d], compDate, _holidays);
                    t2 = extraCompDate.HasValue ? ToIndex(extraCompDate.Value) : null;
                }

                for (int e = 0; e < numEmp; e++)
                {
                    if (t1 is not null)
                    {
                        // 当直(d)があれば代休(t1)を確保する（片方向の含意のみ）。
                        // 双方向の等式 s[e,t1]==x[e,d] にすると、別々の当直日dが同じ代休先t1に
                        // 集約されるケース（例: 土曜当直と平日祝日当直がどちらも同じ月曜に代休となる場合）で
                        // 「その2つの当直日は同一人物でなければならない」という誤った制約になり、
                        // 間隔制約(セクション6)と矛盾してINFEASIBLEになることがあったため、
                        // 片方向の含意にとどめる。他シフトとの排他はセクション5のsum<=1に委ねるため、
                        // x[e,t1]/a[e,t1]を個別に0固定する必要もない。
                        model.Add(s[e, t1.Value] == 1).OnlyEnforceIf(x[e, d]);
                    }

                    if (t2 is not null)
                    {
                        model.Add(s[e, t2.Value] == 1).OnlyEnforceIf(x[e, d]);
                    }

                    // 日勤(d)も当直と同様に代休(t1)を確保する（片方向の含意のみ、理由は上と同じ）。
                    // これが無いと、ConvertToShiftWrites側で日勤者に代休(t1)を書き込む一方で
                    // モデルはt1を空いている日として扱ってしまい、同じ職員がt1に当直で
                    // 選ばれてしまう（代休が実際には付与されない）事態になり得る。
                    // 日勤は二重代休(t2)の対象外（当直の明けに相当する概念が無いため）。
                    if (t1 is not null)
                    {
                        model.Add(s[e, t1.Value] == 1).OnlyEnforceIf(w[e, d]);
                    }
                }
            }

            // ========= 8) 目的関数：当直回数の平準化（max-min最小） + 希望のペナルティ =========
            int upper = Math.Max(31, daysCount); // 安全側
            var minD = model.NewIntVar(0, upper, "minDuty");
            var maxD = model.NewIntVar(0, upper, "maxDuty");

            // 当直対応不可の職員（セクション2でx[e,d]==0固定済み）を含めてしまうと、
            // その職員の当直回数(常に0)にminDが引っ張られてminD=0で固定されてしまい、
            // 「当直対応可能な人達の間での公平性」が実質働かなくなる
            // （max-minのペナルティがmaxDのみを下げようとする動機になり、特定の少人数に
            // 偏っても他の対応可能者が0回のままで許容されてしまう＝個人単位でばらつかない原因）。
            // 平準化の対象は当直対応可能な職員のみに絞る。
            for (int e = 0; e < numEmp; e++)
            {
                if (!_employees[e].CanDoNightDuty) continue;

                var dutyVars = Enumerable.Range(0, daysCount).Select(d => x[e, d]).ToArray();
                var dutySum = LinearExpr.Sum(dutyVars);
                model.Add(dutySum >= minD);
                model.Add(dutySum <= maxD);
            }

            LinearExpr objective = FairnessWeight * (maxD - minD);

            // 希望のペナルティ項を先に構築する。日勤(w)の優先度は「公平性・希望のペナルティが
            // どれだけ積み上がっても絶対に上回らない」重みにする必要があるため、先に希望側の
            // 実際の最大想定合計を知っておく（ユーザーが希望の重みを大きく設定しても優先度が
            // 逆転しないようにするため、固定値ではなく実行時に動的計算する）。
            // 希望はハード制約にせず、必ず目的関数のペナルティ項としてのみ組み込む
            // （こうすることで希望がどれだけ厳しくてもINFEASIBLEにはならない）。
            // Avoid: 割当自体(x[e,d])がそのままペナルティ。Prefer: 割当にマイナスの重み＝ボーナス。
            var prefLiterals = new List<BoolVar>();
            var prefWeights = new List<int>();

            if (_preferencesByEmployee != null)
            {
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
                            prefLiterals.Add(x[e, d]);
                            prefWeights.Add(sign * p.Weight);
                        }
                    }
                }
            }

            // 日勤(w)は目的関数に何も現れないと「割り当てなくても最適」になってしまい、
            // 適格者がいても日勤が一人も選ばれない不具合の原因になっていた。
            // これを防ぐため、日勤を割り当てるほど得になる支配的な報酬を与える。
            int dayWorkWeight = ComputeDayWorkWeight(FairnessWeight, daysCount, prefWeights);

            // 日勤(w)を割り当てるほど得になる報酬を加える（適格でないw[e,d]はセクション2で
            // 既に0固定されているため、対象を絞らずw全体を合計しても安全）。
            var allDayWorkVars = new List<BoolVar>();
            for (int e = 0; e < numEmp; e++)
                for (int d = 0; d < daysCount; d++)
                    allDayWorkVars.Add(w[e, d]);
            objective -= dayWorkWeight * LinearExpr.Sum(allDayWorkVars);

            if (prefLiterals.Count > 0)
            {
                objective += LinearExpr.WeightedSum(prefLiterals, prefWeights);
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
                var akeDate = dutyDate.AddDays(1);
                AddShift(tempMap, upserts, eid, akeDate, _stidAfterDuty);

                // 3. 代休（モデルのセクション7と同じ ShiftBusiness.GetCompWorkOff を使用。単一の真実源）
                var compDate = ShiftBusiness.GetCompWorkOff(dutyDate, _holidays);
                if (compDate.HasValue)
                {
                    AddCompOffIfFree(tempMap, upserts, eid, compDate.Value, dutyDate);
                }

                // 3b. 当直日が日曜/祝日で、明けも祝日の場合（祝日が連続するケース）は、
                // 通常の代休とは別にもう1日代休を確保する。金曜当直は対象外（モデルのセクション7と同じ判定。単一の真実源）。
                if (dutyDate.DayOfWeek != DayOfWeek.Friday && ShiftBusiness.AkeAlsoLandsOnHoliday(dutyDate, _holidays))
                {
                    var extraCompDate = ShiftBusiness.GetExtraCompWorkOffForRestfulAke(dutyDate, compDate, _holidays);
                    if (extraCompDate.HasValue)
                    {
                        AddCompOffIfFree(tempMap, upserts, eid, extraCompDate.Value, dutyDate);
                    }
                }
            }

            // 4. 日勤（明けは付与しないが、当直と同様に代休は付与する。
            //    日勤は必ず本来休みの日曜/祝日に割り当てられるため、出勤した分の代休が必要）
            foreach (var (eid, date) in assignments.DayWorks)
            {
                AddShift(tempMap, upserts, eid, date, _stidDayWork);

                var compDate = ShiftBusiness.GetCompWorkOff(date, _holidays);
                if (compDate.HasValue)
                {
                    AddCompOffIfFree(tempMap, upserts, eid, compDate.Value, date);
                }
            }

            // 5. 代休の集中を緩和する（連休明けなど、複数の当直日/日勤日の代休が
            //    同じ日に集まりすぎた場合、超過分だけ近くの空いている平日にずらす）
            RedistributeCrowdedCompOffs(tempMap, upserts, _stidSubstituteOff, _holidays, MaxCompOffPerDay);

            return upserts;
        }

        // --- Helper Methods (内部利用) ---

        // 同じ日に集中しすぎた代休(_stidSubstituteOff)を、近くの空いている平日にずらす。
        // 上限を超えた分だけを対象にし、職員ID順で先頭maxPerDay件は元の日のまま据え置く
        // （どの職員をずらすかは任意で構わないため、決定的な結果になるようEmployeeId順にしている）。
        // 移動先が見つからない場合（近隣に空いている平日が無い）は、諦めて元の日のまま残す
        // （GetCompWorkOff等と同じく、無理に押し込んでINFEASIBLE相当の状態を作らない方針）。
        internal static void RedistributeCrowdedCompOffs(
            Dictionary<(int EmployeeId, DateTime Date), int> map,
            List<ShiftWrite> upserts,
            int stidSubOff,
            List<DateTime> holidays,
            int maxPerDay)
        {
            var compOffIndexes = new List<int>();
            for (int i = 0; i < upserts.Count; i++)
            {
                if (upserts[i].ShiftTypeId == stidSubOff) compOffIndexes.Add(i);
            }

            var countByDate = compOffIndexes
                .GroupBy(i => upserts[i].Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var crowdedGroups = compOffIndexes
                .GroupBy(i => upserts[i].Date)
                .Where(g => g.Count() > maxPerDay)
                .ToList();

            foreach (var group in crowdedGroups)
            {
                var overflowIndexes = group.OrderBy(i => upserts[i].EmployeeId).Skip(maxPerDay);

                foreach (var idx in overflowIndexes)
                {
                    var entry = upserts[idx];
                    var newDate = FindNearbyBusinessDayBelowCap(entry.EmployeeId, entry.Date, map, countByDate, maxPerDay, holidays);
                    if (newDate is null) continue;

                    map.Remove((entry.EmployeeId, entry.Date));
                    map[(entry.EmployeeId, newDate.Value)] = stidSubOff;

                    countByDate[entry.Date]--;
                    countByDate[newDate.Value] = countByDate.GetValueOrDefault(newDate.Value) + 1;

                    upserts[idx] = entry with { Date = newDate.Value };
                }
            }
        }

        // 元の代休日から近い順（前後交互）に、その職員が空いていて、かつその日の代休人数が
        // まだ上限未満の平日を探す。maxSearchDays日以内に見つからなければnullを返す。
        internal static DateTime? FindNearbyBusinessDayBelowCap(
            int employeeId,
            DateTime original,
            Dictionary<(int EmployeeId, DateTime Date), int> map,
            Dictionary<DateTime, int> countByDate,
            int maxPerDay,
            List<DateTime> holidays,
            int maxSearchDays = 10)
        {
            for (int offset = 1; offset <= maxSearchDays; offset++)
            {
                foreach (var candidate in new[] { original.AddDays(offset).Date, original.AddDays(-offset).Date })
                {
                    if (!ShiftBusiness.IsBusinessDay(candidate, holidays)) continue;
                    if (map.ContainsKey((employeeId, candidate))) continue;
                    if (countByDate.GetValueOrDefault(candidate) >= maxPerDay) continue;

                    return candidate;
                }
            }

            return null;
        }

        // 日勤(w)の目的関数上の重みを計算する。公平性(fairnessWeight*daysCount)と希望ペナルティの
        // 絶対値合計を確実に上回る大きさにすることで、「適格者がいるなら必ず割り当てる」を保証する。
        // ハード制約(==1)にはしないため、本当に割当不可能な稀なケースでもINFEASIBLEにはならず、
        // 単にその日は0のまま解が返る。ユーザーが希望の重みをどれだけ大きく設定しても優先度が
        // 逆転しないよう、固定値ではなく実行時の実際の希望設定から動的に計算する
        // （オーバーフロー対策として int.MaxValue/4 で頭打ちにする）。
        internal static int ComputeDayWorkWeight(int fairnessWeight, int daysCount, IReadOnlyList<int> preferenceWeights)
        {
            long maxFairnessPenalty = (long)fairnessWeight * daysCount;
            long maxPreferencePenalty = preferenceWeights.Aggregate(0L, (acc, wgt) => acc + Math.Abs((long)wgt));
            long dayWorkWeightLong = maxFairnessPenalty + maxPreferencePenalty + 1;
            return dayWorkWeightLong > int.MaxValue / 4 ? int.MaxValue / 4 : (int)dayWorkWeightLong;
        }

        private void AddShift(Dictionary<(int, DateTime), int> map, List<ShiftWrite> upserts, int eid, DateTime date, int stid)
        {
            var key = (eid, date);
            map[key] = stid;
            upserts.Add(new ShiftWrite(eid, date, stid));
        }

        private void AddCompOffIfFree(Dictionary<(int, DateTime), int> map, List<ShiftWrite> upserts, int eid, DateTime date, DateTime originDutyDate)
        {
            var key = (eid, date);

            // 重複チェック（既に当直などが入っていないか簡易チェック）
            if (!map.TryGetValue(key, out int current) || current != _stidDuty)
            {
                map[key] = _stidSubstituteOff;
                upserts.Add(new ShiftWrite(eid, date, _stidSubstituteOff)
                {
                    originDutyDate = originDutyDate
                });
            }
        }
    }
}
