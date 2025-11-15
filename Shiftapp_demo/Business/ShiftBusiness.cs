using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using static Shiftapp_demo.DataAccess.MainDatabaseHelper;

namespace Shiftapp_demo.Business
{
    internal class ShiftBusiness
    {
        private DateTime baseSaturday = new DateTime(2025, 8, 16);
        private readonly MainDatabaseHelper _db;
        private Random rand = new Random();
        private readonly int stidWork;
        private readonly int stidOff;
        private readonly int stidDuty;
        private readonly int stidSubstituteOff;
        private readonly int stidAfterDuty;
        private readonly int stidDayWork;
        const int MinDutyGapDays = 3;


        public ShiftBusiness(MainDatabaseHelper db)
        {
            _db = db;
            stidWork = _db.GetShiftTypeIdBySymbol("/");   // 土曜出勤
            stidOff = _db.GetShiftTypeIdBySymbol("○");   // 日・祭日休み
            stidDuty = _db.GetShiftTypeIdBySymbol("当");  // 当直
            stidSubstituteOff = _db.GetShiftTypeIdBySymbol("●");  // 代休
            stidAfterDuty = _db.GetShiftTypeIdBySymbol("明");  // 明け
            stidDayWork = _db.GetShiftTypeIdBySymbol("日");  // 日勤
        }

        //その月の土曜日を取得するメソッド
        //TODO 最後の日伸ばす？
        public static List<DateTime> GetSaturdaysInMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            var list = new List<DateTime>();
            for (var d = first; d <= last; d = d.AddDays(1))
                if (d.DayOfWeek == DayOfWeek.Saturday) list.Add(d);
            return list;
        }

        //その月の日曜日を取得するメソッド
        //TODO 最後の日伸ばす？
        public static List<DateTime> GetSundaysInMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            var list = new List<DateTime>();
            for (var d = first; d <= last; d = d.AddDays(1))
                if (d.DayOfWeek == DayOfWeek.Sunday) list.Add(d);
            return list;
        }

        //その月の祝日を取得するメソッド
        public List<Holiday> GetHolidaysInMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(2).AddDays(-1);
            return _db.GetHolidays(first, last);
        }

        // 土曜日勤務登録
        public void UpdateSaturdayShifts(DateTime month, string worksClassAtBaseline = "B")
        {
            // 1) データ取得
            var employees = _db.GetActiveEmployeesWithSaturdayClass(); // EmployeeId, SaturdayClass("A"/"B")

            var saturdays = GetSaturdaysInMonth(month);

            // 返り値の想定: Dictionary<(int EmployeeId, DateTime Date), int ShiftTypeId>
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            var existing = _db.GetShiftMap(first, last);

            // 2) 基準週→偶奇を求める関数（“土曜基点”で数える）
            // rotationStartSaturday は必ず土曜日を渡すのが前提
            static int SaturdayIndexFromBaseline(DateTime saturday, DateTime baselineSaturday)
            {
                // baseline の翌日から何日差か → 7 で割る（負もOK）
                var delta = (saturday.Date - baselineSaturday.Date).TotalDays;
                // 同じ土曜なら 0
                return (int)Math.Floor(delta / 7.0);
            }

            bool baselineIsA = worksClassAtBaseline.Equals("A", StringComparison.OrdinalIgnoreCase);

            // 3) 一括割当バッファ
            var assigns = new List<(int eid, DateTime date, int stid)>();

            foreach (var sat in saturdays)
            {
                // 今週が基準から何番目の土曜か（0,1,2,...）
                int k = SaturdayIndexFromBaseline(sat, baseSaturday);

                // この土曜に“出勤する班”はどっち？
                // k 偶数 → baseline と同じ班が出勤 / k 奇数 → 逆の班が出勤
                string workingClass = (k % 2 == 0)
                    ? (baselineIsA ? "A" : "B")
                    : (baselineIsA ? "B" : "A");

                foreach (var emp in employees)
                {
                    // SaturdayClass 未設定はスキップ
                    if (string.IsNullOrWhiteSpace(emp.SaturdayClass)) continue;

                    bool isWorker = emp.SaturdayClass.Equals(workingClass, StringComparison.OrdinalIgnoreCase);

                    // 出勤者には "/"、休みには "〇" を入れる
                    assigns.Add((emp.EmployeeId, sat, isWorker ? stidWork : stidOff));
                }
            }

            // 4) 一括Upsert
            var map = new Dictionary<(int, DateTime), int>(existing);
            var upserts = new List<ShiftWrite>();

            // ★夜勤の子（明/代休）は上書き禁止で適用
            foreach (var (eid, date, stid) in assigns)
            {
                TrySetWithPriorityWeekend(map, upserts, eid, date, stid, cur => IsNightChild(cur));
            }

            // 5) 変更分のみをDBへ反映
            if (upserts.Count > 0)
            {
                // DatabaseHelper 側のシグネチャが (IEnumerable<(int eid, DateTime date, int stid)>, DateTime month)
                // の場合は、3タプルに落として渡す
                var triples = upserts
                    .Select(x => (x.EmployeeId, x.Date, x.ShiftTypeId))
                    .ToList();

                _db.BulkUpsertShifts(triples, month);
            }
        }
        //日曜日勤務登録
        public void UpdateSundayShifts(DateTime month)
        {
            // 1) データ取得
            var employees = _db.GetAllEmployees();
            var sundays = GetSundaysInMonth(month);
            var holidays = GetHolidaysInMonth(month);

            // 月間の既存シフト（当直など）があれば優先したいので先に取得
            // 返り値の想定: Dictionary<(int EmployeeId, DateTime Date), int ShiftTypeId>
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            var existing = _db.GetShiftMap(first, last);

            var assigns = new List<(int eid, DateTime date, int stid)>();

            var days = sundays.Select(d => d.Date)
                              .Union(holidays.Select(d => d.date)); // 重複自動除去

            foreach (var day in days)
            {
                foreach (var emp in employees)
                    assigns.Add((emp.EmployeeId, day, stidOff));
            }

            // 4) 一括Upsert
            var map = new Dictionary<(int, DateTime), int>(existing);
            var upserts = new List<ShiftWrite>();

            // ★夜勤の子（明/代休）は上書き禁止で適用
            foreach (var (eid, date, stid) in assigns)
            {
                TrySetWithPriorityWeekend(map, upserts, eid, date, stid, cur => IsNightChild(cur));
            }

            // 5) 変更分のみをDBへ反映
            if (upserts.Count > 0)
            {
                // DatabaseHelper 側のシグネチャが (IEnumerable<(int eid, DateTime date, int stid)>, DateTime month)
                // の場合は、3タプルに落として渡す
                var triples = upserts
                    .Select(x => (x.EmployeeId, x.Date, x.ShiftTypeId))
                    .ToList();

                _db.BulkUpsertShifts(triples, month);
            }

        }

        /// <summary>
        /// 指定月の当直を作成する。
        /// 1日あたり カテ可1名 + カテ不可1名 を選出し、翌日明け休/週末代休を付与。
        /// 既存データがある場合は優先度（当＞〇＞／）で上書きを避ける。
        /// </summary>
        public void GenerateNightDutiesForMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            // 1) 対象社員の取得（is_active=1、夜勤できる前提）
            var employees_active_all = _db.GetActiveEmployeesWithNightDutyClass();


            var canDayduty = _db.GetActiveEmployeesWithDayDutyClass();

            // カテ可/不可でリスト分割
            var canCath = employees_active_all.Where(e => e.CanDoCatheterization && e.CanDoNightDuty).ToList();
            var cannotCath = employees_active_all.Where(e => !e.CanDoCatheterization && e.CanDoNightDuty).ToList();

            if (canCath.Count == 0 || cannotCath.Count == 0)
                throw new InvalidOperationException("当直編成に必要な人員（カテ可/不可）が不足しています。");

            // 2) 既存のシフトをマップ化（当月＋周辺の休み付与に備えて＋3日まで読むのが安全）
            var preloadStart = first.AddDays(-7);
            var preloadEnd = last.AddDays(21); // 週末代休を安全に見る
            var existingMap = _db.GetShiftMap(preloadStart, preloadEnd);


            //祭日取得
            var holidays = GetHolidaysInMonth(month).Select(h => h.date).ToList();

            // 3) 各人の「次に入れる日」・当月カウントを初期化    
            var nextAvailable = new Dictionary<int, DateTime>();     // EmployeeId -> Date
            var dayWorkCount = new Dictionary<int, int>();           // EmployeeId -> 当月の日勤回数
            var dutyCount = new Dictionary<int, int>();
            var restcount = new Dictionary<int, int>();

            // 初期化
            foreach (var e in employees_active_all)
            {
                var lastDutyDate = existingMap
                    .Where(kv => kv.Key.Item1 == e.EmployeeId && kv.Value == stidDuty && kv.Key.Item2 < first)
                    .Select(kv => kv.Key.Item2)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();

                nextAvailable[e.EmployeeId] = (lastDutyDate == DateTime.MinValue)
                    ? first
                    : lastDutyDate.AddDays(MinDutyGapDays);
                restcount[e.EmployeeId] = 0;
                dutyCount[e.EmployeeId] = 0;

                // 月初の“明け”をシード（前月末の当直→当月1日の明け）
                if (lastDutyDate != DateTime.MinValue)
                {
                    var ake = lastDutyDate.AddDays(1);
                    if (ake >= first && ake <= last)
                        existingMap[(e.EmployeeId, ake.Date)] = stidAfterDuty; // 表示/衝突回避のため map にも反映
                }
            }

            foreach (var e in canDayduty)
            {
                dayWorkCount[e.EmployeeId] = 0;
                if (!nextAvailable.ContainsKey(e.EmployeeId))
                    nextAvailable[e.EmployeeId] = preloadStart;
            }

            // 5) 選抜アルゴリズム（フェアネス：当月回数が少ない人→次に早く入れる人→ラウンド）
            var upserts = new List<ShiftWrite>();

            for (var day = first; day <= last; day = day.AddDays(1))
            {
                // その日にすでに当直が入っていればスキップ
                // 今回は作成者なので、既存当直は尊重しつつ足りない側だけ補完したければ、
                // 片側ずつ判定するロジックに分ける。ここではシンプルに新規作成前提。
                // 候補抽出：カテ可
                var cand1 = canCath
                .Where(e => nextAvailable[e.EmployeeId] <= day)
                .OrderBy(e => dutyCount.TryGetValue(e.EmployeeId, out var v) ? v : 0)
                .ThenBy(e => restcount.TryGetValue(e.EmployeeId, out var v) ? v : 0)
                .ThenBy(e => nextAvailable[e.EmployeeId])
                .ThenBy(_ => rand.Next())
                .FirstOrDefault();

                var cand2 = cannotCath
                .Where(e => nextAvailable[e.EmployeeId] <= day)
                .OrderBy(e => dutyCount.TryGetValue(e.EmployeeId, out var v) ? v : 0)
                .ThenBy(e => restcount.TryGetValue(e.EmployeeId, out var v) ? v : 0)
                .ThenBy(e => nextAvailable[e.EmployeeId])
                .ThenBy(_ => rand.Next())
                .FirstOrDefault();

                //日勤候補
                Models.Employee? cand3 = null, cand4 = null;

                //祝日候補
                if (day.DayOfWeek == DayOfWeek.Sunday)
                {
                    var prevDay = day.AddDays(-1).Date;
                    cand3 = canDayduty
                        .Where(e => e.EmployeeId != cand1?.EmployeeId
                                 && e.EmployeeId != cand2?.EmployeeId
                                 && existingMap.TryGetValue((e.EmployeeId, prevDay), out var st) && st == stidOff)
                        //.OrderBy(e => dayWorkCount.TryGetValue(e.EmployeeId, out var v) ? v : 0)
                        //.ThenBy(e => nextAvailable[e.EmployeeId])
                        //.ThenBy(_ => rand.Next())
                        .FirstOrDefault();
                }
                else if (holidays.Contains(day.Date))
                {
                    cand4 = canDayduty
                        .Where(e => e.EmployeeId != cand1?.EmployeeId && e.EmployeeId != cand2?.EmployeeId)
                        .OrderBy(e => dayWorkCount.TryGetValue(e.EmployeeId, out var v) ? v : 0)
                        .ThenBy(e => nextAvailable[e.EmployeeId])
                        .ThenBy(_ => rand.Next())
                        .FirstOrDefault();
                }

                // 当直（上書きOK、nullガード）
                var placed1 = false;
                var placed2 = false;
                var placed3 = false;
                var placed4 = false;

                if (cand1 != null)
                {
                    placed1 = TrySetWithPriority(existingMap, upserts, cand1.EmployeeId, day, stidDuty);
                    dutyCount[cand1.EmployeeId] = dutyCount.TryGetValue(cand1.EmployeeId, out var c1) ? c1 + 1 : 1;
                }
                if (cand2 != null)
                {
                    placed2 = TrySetWithPriority(existingMap, upserts, cand2.EmployeeId, day, stidDuty);
                    dutyCount[cand2.EmployeeId] = dutyCount.TryGetValue(cand2.EmployeeId, out var c2) ? c2 + 1 : 1;
                }

                // 日勤（同一人物の重複は避ける。上書きOK）
                if (cand3 != null && cand3.EmployeeId != cand1?.EmployeeId && cand3.EmployeeId != cand2?.EmployeeId)
                {
                    placed3 = TrySetWithPriority(existingMap, upserts, cand3.EmployeeId, day, stidDayWork);
                    dayWorkCount[cand3.EmployeeId] = dayWorkCount.TryGetValue(cand3.EmployeeId, out var w3) ? w3 + 1 : 1;
                }
                if (cand4 != null && cand4.EmployeeId != cand1?.EmployeeId && cand4.EmployeeId != cand2?.EmployeeId)
                {
                    placed4 = TrySetWithPriority(existingMap, upserts, cand4.EmployeeId, day, stidDayWork);
                    dayWorkCount[cand4.EmployeeId] = dayWorkCount.TryGetValue(cand4.EmployeeId, out var w4) ? w4 + 1 : 1;
                }

                DateTime nextDay = day.Date.AddDays(1);
                if (placed1) TrySetWithPriority(existingMap, upserts, cand1!.EmployeeId, nextDay, stidAfterDuty);
                if (placed2) TrySetWithPriority(existingMap, upserts, cand2!.EmployeeId, nextDay, stidAfterDuty);

                // （必要なら）週末代休も復活：当直者にのみ付与（上書きOK）
                DateTime? comp1 = null, comp2 = null;
                var comp = GetCompDutyDayOff(day, holidays);
                if (comp.HasValue)
                {
                    if (placed1)
                    {
                        SetChildWithOrigin(existingMap, upserts, cand1!.EmployeeId, comp.Value, stidSubstituteOff, day); comp1 = comp.Value;
                        restcount[cand1.EmployeeId]++;
                    }
                    if (placed2)
                    {
                        SetChildWithOrigin(existingMap, upserts, cand2!.EmployeeId, comp.Value, stidSubstituteOff, day); comp2 = comp.Value;
                        restcount[cand1.EmployeeId]++;
                    }
                }

                DateTime? comp3 = null, comp4 = null;
                var comp_day = GetCompDayWorkOff(day, holidays);
                if (comp_day.HasValue)
                {
                    if (placed3) { SetChildWithOrigin(existingMap, upserts, cand3!.EmployeeId, comp_day.Value, stidSubstituteOff, day); comp3 = comp_day.Value; }
                    if (placed4) { SetChildWithOrigin(existingMap, upserts, cand4!.EmployeeId, comp_day.Value, stidSubstituteOff, day); comp4 = comp_day.Value; }
                }

                // 連勤ガード（例：3日空け）
                const int MinDutyGapDays = 3;
                if (placed1) nextAvailable[cand1!.EmployeeId] = day.AddDays(MinDutyGapDays);
                if (placed2) nextAvailable[cand2!.EmployeeId] = day.AddDays(MinDutyGapDays);
                if (placed3) nextAvailable[cand3!.EmployeeId] = day.AddDays(MinDutyGapDays);
                if (placed4) nextAvailable[cand4!.EmployeeId] = day.AddDays(MinDutyGapDays);
            }

            // 6) 一括反映
            if (upserts.Count > 0)
            {
                _db.BulkUpsert_Duty_Shifts(upserts, month);
            }


        }



        /// <summary>
        /// 週末当直日の代休日を返す。
        /// 金曜→次水 / 土曜→次月 / 日曜→次火
        /// それ以外は null。
        /// </summary>
        /// <summary>
        /// 週末当直日の代休日を返す（必ず平日に補正）。
        /// 金曜→次水 / 土曜→次月 / 日曜→次火 を起点に、土日祝なら次の営業日にずらす。
        /// それ以外は null。
        /// </summary>
        private DateTime? GetCompDutyDayOff(DateTime dutyDay, List<DateTime> holidays)
        {
            // 正規化（時刻部分を落とす）
            dutyDay = dutyDay.Date;

            // 祝日リストを高速検索用に
            var hset = new HashSet<DateTime>(holidays.Select(x => x.Date));

            bool isFriSatSun = dutyDay.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;
            bool isHoliday = hset.Contains(dutyDay);

            // 「金土日」 または 「祝日」のいずれかなら代休付与対象
            if (!isFriSatSun && !isHoliday)
                return null;

            DateTime? raw = dutyDay.DayOfWeek switch
            {

                DayOfWeek.Friday => NextWeekday(dutyDay, DayOfWeek.Wednesday),
                DayOfWeek.Saturday => dutyDay.AddDays(2), // 月
                DayOfWeek.Sunday => dutyDay.AddDays(2), // 火
                _ => dutyDay.AddDays(3)//祝日
            };

            return raw.HasValue ? BumpToNextBusinessDay(raw.Value, holidays) : null;
        }

        private bool IsNightChild(int stid)
            => stid == stidAfterDuty   // 明
            || stid == stidSubstituteOff// 代休
            || stid == stidDuty// 当直
            || stid == stidDayWork; // 日勤




        /// <summary>
        /// 週末当直日の代休日を返す（必ず平日に補正）。
        /// 優先順位：金/土/日 ルールを祝日より優先。
        ///   金曜→次水 / 土曜→次月 / 日曜→次火
        /// 平日の祝日は +3 日を起点に営業日に補正。
        /// さらに「金曜当直／月〜木の祝日」で翌月にハミ出す場合は、
        /// その日から1週間以内の直前営業日に付け替える（無ければ null）。
        /// </summary>
        private DateTime? GetCompDayWorkOff(DateTime dutyDay, List<DateTime> holidays)
        {
            var day = dutyDay.Date;
            var hset = new HashSet<DateTime>(holidays.Select(x => x.Date));

            DateTime? raw = null;
            bool isWeekdayHoliday = (day.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Thursday) && hset.Contains(day);

            switch (day.DayOfWeek)
            {
                case DayOfWeek.Friday:
                    // 金曜が祝日でも金曜ルール優先：次の水
                    raw = NextWeekday(day, DayOfWeek.Wednesday);
                    break;
                case DayOfWeek.Saturday:
                    // 次の月（翌月跨ぎOK）
                    raw = day.AddDays(2);
                    break;
                case DayOfWeek.Sunday:
                    // 次の火（翌月跨ぎOK）
                    raw = day.AddDays(2);
                    break;
                default:
                    // 平日（月〜木）の祝日のみ対象
                    if (isWeekdayHoliday)
                        raw = day.AddDays(3);
                    break;
            }

            if (!raw.HasValue) return null;

            var candidate = BumpToNextBusinessDay(raw.Value, holidays);

            // 翌月に出るなら「その1週間前以内の直前営業日」に付け替え（対象：金曜当直 or 月〜木祝日）
            if ((day.DayOfWeek == DayOfWeek.Friday || isWeekdayHoliday) && candidate.Month != day.Month)
            {
                var fallback = PickPrevBusinessDayWithin(candidate, 7, holidays, day.Month);
                return fallback; // 見つからなければ null
            }

            return candidate;
        }
        // 直前の営業日を最大 daysBack 日さかのぼって探す（同一 monthOnly に限定）
        private static DateTime? PickPrevBusinessDayWithin(DateTime anchor, int daysBack, List<DateTime> holidays, int monthOnly)
        {
            for (int i = 1; i <= daysBack; i++)
            {
                var d = anchor.Date.AddDays(-i);
                if (d.Month != monthOnly) break;       // 当月から外れたら打ち切り
                if (IsBusinessDay(d, holidays)) return d;
            }
            return null;
        }
        /// <summary>
        /// すでに当が入っていればそのまま。休みや日勤があっても当で上書きする。
        /// </summary>
        private bool TrySetWithPriority(
            Dictionary<(int EmployeeId, DateTime Date), int> map,
            List<ShiftWrite> upserts,
            int eid, DateTime d, int newStid,
            Func<int, bool>? cannotOverwrite = null)
        {
            var key = (eid, d.Date);
            if (!map.TryGetValue(key, out var cur))
            {
                map[key] = newStid;
                upserts.Add(new ShiftWrite(eid, d.Date, newStid));
                return true;
            }

            // 既存が「明 or 代休」等、保護対象なら上書きしない
            if (cannotOverwrite?.Invoke(cur) == true)
                return false;

            //if (PriorityOf(newStid) > PriorityOf(cur))
            //{
            map[key] = newStid;
            upserts.Add(new ShiftWrite(eid, d.Date, newStid));
            return true;
            //}
            //return false;
        }

        private bool TrySetWithPriorityWeekend(
            Dictionary<(int EmployeeId, DateTime Date), int> map,
            List<ShiftWrite> upserts,
            int eid, DateTime d, int newStid,
            Func<int, bool>? cannotOverwrite = null)
        {
            var key = (eid, d.Date);
            if (!map.TryGetValue(key, out var cur))
            {
                map[key] = newStid;
                upserts.Add(new ShiftWrite(eid, d.Date, newStid));
                return true;
            }

            // 既存が「明 or 代休」等、保護対象なら上書きしない
            if (cannotOverwrite?.Invoke(cur) == true)
                return false;

            map[key] = newStid;
            upserts.Add(new ShiftWrite(eid, d.Date, newStid));
            return true;

        }

        private void SetChildWithOrigin(
            Dictionary<(int EmployeeId, DateTime Date), int> map,
            List<ShiftWrite> upserts,
            int eid, DateTime targetDate, int stidChild, DateTime parentDutyDate)
        {
            var key = (eid, targetDate.Date);
            // 既存がある日に上書きしたくないなら、ここで return してもOK（方針次第）
            map[key] = stidChild;
            upserts.Add(new ShiftWrite(eid, targetDate.Date, stidChild)
            {
                originDutyDate = parentDutyDate.Date  // ★ 親は“当直日”を渡す
            });
        }

        private DateTime NextWeekday(DateTime from, DayOfWeek target)
        {
            // 「次の target 曜日」（翌週の同曜日も含めて最短）
            int diff = ((int)target - (int)from.DayOfWeek + 7) % 7;
            if (diff == 0) diff = 7;
            return from.AddDays(diff);
        }

        // 平日の営業日 (Mon-Fri かつ 祝日でない)
        private static bool IsBusinessDay(DateTime d, List<DateTime> holidays)
            => d.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday
               && !holidays.Contains(d.Date);

        // 営業日に前進（自分が営業日ならそのまま）
        private static DateTime BumpToNextBusinessDay(DateTime d, List<DateTime> holidays)
        {
            var x = d.Date;
            while (!IsBusinessDay(x, holidays))
                x = x.AddDays(1);
            return x;
        }

        public void CleanOrphanNightChildrenForMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var end = first.AddMonths(1).AddDays(2); // 翌月2日まで掃除（業務判断）

            _db.DeleteOrphanNightChildren(
                start: first,
                end: end,
                stidDuty: stidDuty,
                stidAke: stidAfterDuty,
                stidSubOff: stidSubstituteOff
            );
        }


    }

}
