using Shiftapp_demo.DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.Business
{
    internal class ShiftBusiness
    {
        DateTime baseSaturday = new DateTime(2025, 8, 16);

        private readonly DatabaseHelper _db;

        private readonly int stidWork;
        private readonly int stidOff;
        private readonly int stidDuty;
        private readonly int stidSubstituteOff;
        private readonly int stidAfterDuty;
        private readonly int stidDayWork;

        public ShiftBusiness(DatabaseHelper db)
        {
            _db = db;

            // シンボルID取得（※あなたのDBに合わせて）
            stidWork = _db.GetShiftTypeIdBySymbol("/");   // 土曜出勤
            stidOff = _db.GetShiftTypeIdBySymbol("○");   // 日・祭日休み
            stidDuty = _db.GetShiftTypeIdBySymbol("当");  // 当直
            stidSubstituteOff = _db.GetShiftTypeIdBySymbol("●");  // 代休
            stidAfterDuty = _db.GetShiftTypeIdBySymbol("明");  // 明け
            stidDayWork = _db.GetShiftTypeIdBySymbol("日");  // 日勤


        }
        //その月の土曜日を取得するメソッド
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
        public List<DateTime> GetHolidaysInMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
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
            if (assigns.Count > 0)
                _db.BulkUpsertShifts(assigns);
        }
        //日曜日勤務登録
        public void UpdateSundayShifts(DateTime month)
        {
            // 1) データ取得
            var employees = _db.GetAllEmployees();
            var sundays = GetSundaysInMonth(month);

            // 月間の既存シフト（当直など）があれば優先したいので先に取得
            // 返り値の想定: Dictionary<(int EmployeeId, DateTime Date), int ShiftTypeId>
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            var existing = _db.GetShiftMap(first, last);

            var assigns = new List<(int eid, DateTime date, int stid)>();

            foreach (var sunday in sundays)
            {
                foreach (var emp in employees)
                {
                    assigns.Add((emp.EmployeeId, sunday, stidOff));
                }
            }

            // 4) 一括Upsert
            if (assigns.Count > 0)
                _db.BulkUpsertShifts(assigns);

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
            var employees_Night_duty = _db.GetActiveEmployeesWithNightDutyClass();

            var canDayduty = _db.GetActiveEmployeesWithDayDutyClass();

            // カテ可/不可でリスト分割
            var canCath = employees_Night_duty.Where(e => e.CanDoCatheterization && e.CanDoNightDuty == 1).ToList();
            var cannotCath = employees_Night_duty.Where(e => !e.CanDoCatheterization && e.CanDoNightDuty == 1).ToList();

            if (canCath.Count == 0 || cannotCath.Count == 0)
                throw new InvalidOperationException("当直編成に必要な人員（カテ可/不可）が不足しています。");

            // 2) 既存のシフトをマップ化（当月＋周辺の休み付与に備えて＋3日まで読むのが安全）
            var preloadStart = first.AddDays(-2);
            var preloadEnd = last.AddDays(7); // 週末代休を安全に見る
            var existingMap = _db.GetShiftMap(preloadStart, preloadEnd);
            //祭日取得
            var holidays = _db.GetHolidays(preloadStart, preloadEnd);
            // existingMap: Dictionary<(int EmployeeId, DateTime Date), int shiftTypeId>

            // 3) 各人の「次に入れる日」・当月カウントを初期化    
            var nextAvailable = new Dictionary<int, DateTime>();     // EmployeeId -> Date
            var dayWorkCount = new Dictionary<int, int>();           // EmployeeId -> 当月の日勤回数
            var dutyCount = new Dictionary<int, int>();              // EmployeeId -> 当月の当直回数
            foreach (var e in employees_Night_duty)
            {
                nextAvailable[e.EmployeeId] = preloadStart; // とりあえず最小に
                dutyCount[e.EmployeeId] = 0;
            }

            foreach (var e in canDayduty)
            {
                nextAvailable[e.EmployeeId] = preloadStart; // とりあえず最小に
                dayWorkCount[e.EmployeeId] = 0;
            }

            // 4) 当月すでに存在する当直をカウント＆明け休から nextAvailable を更新
            foreach (var e in employees_Night_duty)
            {
                for (var d = preloadStart; d <= preloadEnd; d = d.AddDays(1))
                {
                    if (existingMap.TryGetValue((e.EmployeeId, d), out var stid))
                    {
                        if (stid == stidDuty)
                        {
                            // 当直済み → 翌日を少なくとも明けとしてブロック
                            var na = d.AddDays(1);
                            if (nextAvailable[e.EmployeeId] < na)
                                nextAvailable[e.EmployeeId] = na;
                        }
                        else if (stid == stidOff)
                        {
                            // 休みの日は nextAvailable を d を超えるようにしても良いが、
                            // 明け休のみ厳密ブロックできていれば十分。必要なら拡張。
                        }
                    }
                }
            }

            // 5) 選抜アルゴリズム（フェアネス：当月回数が少ない人→次に早く入れる人→ラウンド）

            List<(int EmployeeId, DateTime Date, int ShiftTypeId)> upserts = new();

            for (var day = first; day <= last; day = day.AddDays(1))
            {

                var rand = new Random();
                // その日にすでに当直が入っていればスキップ
                // 今回は作成者なので、既存当直は尊重しつつ足りない側だけ補完したければ、
                // 片側ずつ判定するロジックに分ける。ここではシンプルに新規作成前提。
                // 候補抽出：カテ可
                var cand1 = canCath
                    .Where(e => nextAvailable[e.EmployeeId] <= day)
                    .OrderBy(e => dutyCount[e.EmployeeId]) // 当月少ない順
                    .ThenBy(e => nextAvailable[e.EmployeeId])
                    .ThenBy(_ => rand.Next())  // 乱数を並べ替えに混ぜる
                    .FirstOrDefault();

                // 候補抽出：カテ不可
                var cand2 = cannotCath
                    .Where(e => nextAvailable[e.EmployeeId] <= day)
                    .OrderBy(e => dutyCount[e.EmployeeId])
                    .ThenBy(e => nextAvailable[e.EmployeeId])
                    .ThenBy(_ => rand.Next())  // 乱数を並べ替えに混ぜる
                    .FirstOrDefault();
                //日勤候補
                Models.Employee? cand3 = null;

                //祝日候補
                Models.Employee? cand4 = null;
                if (day.DayOfWeek == DayOfWeek.Sunday)
                {
                    DateTime prevDay = day.AddDays(-1);
                    
                    cand3 = canDayduty
                       .Where(e => existingMap.TryGetValue(
                       (e.EmployeeId, prevDay), out var stid) && stid == stidOff)
                       .OrderBy(e => dayWorkCount[e.EmployeeId])
                       .ThenBy(_ => rand.Next())
                       .FirstOrDefault();
                }

                else if (holidays.Contains(day))
                {
                    cand4 = canDayduty
                       .OrderBy(e => dayWorkCount[e.EmployeeId])
                       .ThenBy(_ => rand.Next())
                       .FirstOrDefault();
                }

                TrySet(existingMap, upserts, cand1.EmployeeId, day, stidDuty);
                TrySet(existingMap, upserts, cand2.EmployeeId, day, stidDuty);

                if (cand3 != null)
                {
                    TrySet(existingMap, upserts, cand3.EmployeeId, day, stidDayWork);

                    dayWorkCount[cand3.EmployeeId]++;
                }

                if (cand4 != null)
                {
                    TrySet(existingMap, upserts, cand4.EmployeeId, day, stidDayWork);

                    dayWorkCount[cand4.EmployeeId]++;
                }


                dutyCount[cand1.EmployeeId]++;
                dutyCount[cand2.EmployeeId]++;



                // 明け休（翌日）をセット
                var nextDay = day.AddDays(1);
                TrySetOffIfNoStronger(existingMap, upserts, cand1.EmployeeId, nextDay, stidAfterDuty);
                TrySetOffIfNoStronger(existingMap, upserts, cand2.EmployeeId, nextDay, stidAfterDuty);

                // 週末代休
                var comp = GetCompDayOff(day, holidays);
                if (comp.HasValue)
                {
                    TrySetOffIfNoStronger(existingMap, upserts, cand1.EmployeeId, comp.Value, stidSubstituteOff);
                    TrySetOffIfNoStronger(existingMap, upserts, cand2.EmployeeId, comp.Value, stidSubstituteOff);
                    if (cand3 != null)
                    {
                        TrySetOffIfNoStronger(existingMap, upserts, cand3.EmployeeId, comp.Value, stidSubstituteOff);
                    }
                    if (cand4 != null)
                    {
                        TrySetOffIfNoStronger(existingMap, upserts, cand4.EmployeeId, comp.Value, stidSubstituteOff);
                    }
                }

                // 次に入れる日は最低でも翌日以降（明け休が入ったため）
                nextAvailable[cand1.EmployeeId] = nextDay;
                nextAvailable[cand2.EmployeeId] = nextDay;

            }

            // 6) 一括反映
            if (upserts.Count > 0)
            {
                _db.BulkUpsertShifts(upserts);
            }
        }

        /// <summary>
        /// すでに当が入っていればそのまま。休みや日勤があっても当で上書きする。
        /// </summary>
        private void TrySet(
            Dictionary<(int EmployeeId, DateTime Date), int> map,
            List<(int EmployeeId, DateTime Date, int ShiftTypeId)> upserts,
            int eid, DateTime d, int stid)
        {
            map[(eid, d)] = stid;
            upserts.Add((eid, d, stid));
        }

        /// <summary>
        /// 休み（／）を「当を潰さない」形でセット。
        /// </summary>
        private void TrySetOffIfNoStronger(
            Dictionary<(int EmployeeId, DateTime Date), int> map,
            List<(int EmployeeId, DateTime Date, int ShiftTypeId)> upserts,
            int eid, DateTime d, int stid)
        {
            map[(eid, d)] = stid;
            upserts.Add((eid, d, stid));
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
        private DateTime? GetCompDayOff(DateTime dutyDay, List<DateTime> holidays)
        {
            DateTime? raw = dutyDay.DayOfWeek switch
            {
                DayOfWeek.Friday => NextWeekday(dutyDay, DayOfWeek.Wednesday),
                DayOfWeek.Saturday => dutyDay.AddDays(2), // 月
                DayOfWeek.Sunday => dutyDay.AddDays(2), // 火
                _ => null
            };

            return raw.HasValue ? BumpToNextBusinessDay(raw.Value, holidays) : null;
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
    }

}
