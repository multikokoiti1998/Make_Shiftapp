using Serilog;
using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System.Security.RightsManagement;
using static Shiftapp_demo.DataAccess.MainDatabaseHelper;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Shiftapp_demo.Business
{
    internal class ShiftBusiness
    {
        //private readonly　DateTime _baselineSaturday;
        private bool _baselineIsA;
        private readonly MainDatabaseHelper _db;
        private Random rand = new Random();
        private readonly int stidWork;
        private readonly int stidOff;
        private readonly int stidDuty;
        private readonly int stidSubstituteOff;
        private readonly int stidAfterDuty;
        private readonly int stidDayWork;
        public static int MinDutyGapDays = 3;
        public static readonly DateTime _baselineSaturday = new DateTime(2025, 8, 16);

        //テスト用
        //public static readonly DateTime _baselineSaturday = new DateTime(2026, 2, 28);


        public ShiftBusiness(MainDatabaseHelper db)
        {
            _db = db;
            //_baselineSaturday= new DateTime(2025, 8, 16);
            stidWork = _db.GetShiftTypeIdBySymbol("/");   // 土曜出勤
            stidOff = _db.GetShiftTypeIdBySymbol("○");   // 日・祭日休み
            stidDuty = _db.GetShiftTypeIdBySymbol("当");  // 当直
            stidSubstituteOff = _db.GetShiftTypeIdBySymbol("●");  // 代休
            stidAfterDuty = _db.GetShiftTypeIdBySymbol("明");  // 明け
            stidDayWork = _db.GetShiftTypeIdBySymbol("日");  // 日勤
        }

        //START---------------------------基本関数-----------------
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
        public List<Holiday> GetHolidaysInMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(2).AddDays(-1);
            return _db.GetHolidays(first, last);
        }


        private bool IsNightChild(int stid)
       => stid == stidAfterDuty   // 明
       || stid == stidSubstituteOff// 代休
       || stid == stidDuty// 当直
       || stid == stidDayWork; // 日勤
        //END---------------------------基本関数-----------------

        //START---------------------A,B班の判定のための基準土曜日をセットするメソッド----------------
        /// 指定日の属する週の「土曜日」との差を返す

        //END---------------------A,B班の判定のための基準土曜日をセットするメソッド----------------
        private string GetWorkingClass(DateTime day)
        {
            // 1. その日が属する「基準の土曜日」を特定する
            // 日曜日の場合は「前日の土曜」、それ以外は「次の土曜」をその週の基準とする
            DateTime targetSat = (day.DayOfWeek == DayOfWeek.Sunday)
                ? day.AddDays(-1).Date
                : day.AddDays(DayOfWeek.Saturday - day.DayOfWeek).Date;

            // 2. 運用開始（ベースライン）からの経過週数を計算
            var deltaDays = (targetSat - _baselineSaturday.Date).TotalDays;
            int weekIndex = (int)Math.Floor(deltaDays / 7.0);

            // 3. 隔週（偶数週・奇数週）で A か B を判定
            bool isEvenWeek = (weekIndex % 2 == 0);

            return isEvenWeek
                ? (_baselineIsA ? "A" : "B")
                : (_baselineIsA ? "B" : "A");
        }

        //START---------------------------代休付与のヘルパー関数-----------------
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


        private DateTime? GetCompWorkOff(DateTime dutyDay, List<DateTime> holidays)
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
                return fallback;
            }

            return candidate;
        }


        // 直前の営業日を最大 daysBack 日さかのぼって探す（同一 monthOnly に限定）
        private static DateTime? PickPrevBusinessDayWithin(DateTime anchor, int daysBack, List<DateTime> holidays, int monthOnly)
        {
            for (int i = 1; i <= daysBack; i++)
            {
                var d = anchor.Date.AddDays(-i);
                if (d.Month != monthOnly) break;
                if (IsBusinessDay(d, holidays)) return d;
            }
            return null;
        }
        //END---------------------------代休付与のヘルパー関数-----------------

        //START---------------------------代休付与関数-------------------------

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
            map[key] = stidChild;
            upserts.Add(new ShiftWrite(eid, targetDate.Date, stidChild)
            {
                originDutyDate = parentDutyDate.Date
            });
        }

        private DateTime NextWeekday(DateTime from, DayOfWeek target)
        {
            // 「次の target 曜日」（翌週の同曜日も含めて最短）
            int diff = ((int)target - (int)from.DayOfWeek + 7) % 7;
            if (diff == 0) diff = 7;
            return from.AddDays(diff);
        }

        public void CleanOrphanNightChildrenForMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var end = first.AddMonths(1).AddDays(2);

            _db.DeleteOrphanNightChildren(
                start: first,
                end: end,
                stidDuty: stidDuty,
                stidAke: stidAfterDuty,
                stidSubOff: stidSubstituteOff
            );
        }

        //プールから当直者を抽出


        //UIからシフト登録用
        public void SaveShifts(
        DateTime month,
        IEnumerable<ShiftDataLoader> dirtyRows,
        IReadOnlyDictionary<string, int> symbolToId)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            _db.SaveDailyShifts(first, last, dirtyRows, symbolToId);
        }


        //---------------------------ここまでシフト生成ロジックのヘルパー関数-----------------

        //----------------------------ここからシフト生成ロジック-----------------
        // 土曜日勤務登録
        public void UpdateSaturdayShifts(DateTime month, string worksClassAtBaseline = "B")
        {
            _baselineIsA = worksClassAtBaseline.Equals("A", StringComparison.OrdinalIgnoreCase);

            var employees = _db.GetActiveEmployeesWithSaturdayClass(); // EmployeeId, SaturdayClass("A"/"B")

            var saturdays = GetSaturdaysInMonth(month);

            // 返り値の想定: Dictionary<(int EmployeeId, DateTime Date), int ShiftTypeId>
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            var existing = _db.GetShiftMap(first, last);


            var assigns = new List<(int eid, DateTime date, int stid)>();

            foreach (var sat in saturdays)
            {
                // この週に働く班を共通関数で取得
                string workingClass = GetWorkingClass(sat);

                foreach (var emp in employees)
                {
                    // SaturdayClass 未設定はスキップ
                    if (string.IsNullOrWhiteSpace(emp.SaturdayClass)) continue;

                    bool isWorker =
                        emp.SaturdayClass.Equals(workingClass, StringComparison.OrdinalIgnoreCase);

                    // 出勤者には勤務シフト、休みにはOFFシフト
                    assigns.Add((emp.EmployeeId, sat.Date, isWorker ? stidWork : stidOff));
                }
            }

            var map = new Dictionary<(int, DateTime), int>(existing);
            var upserts = new List<ShiftWrite>();

            // ★夜勤の子（明/代休）は上書き禁止で適用
            foreach (var (eid, date, stid) in assigns)
            {
                TrySetWithPriority(map, upserts, eid, date, stid, cur => IsNightChild(cur));
            }

            // 5) 変更分のみをDBへ反映
            if (upserts.Count > 0)
            {
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
                TrySetWithPriority(map, upserts, eid, date, stid, cur => IsNightChild(cur));
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

        //シフト作成の状態保持クラス
        public class EmployeeWorkState
        {
            public int EmployeeId { get; init; }
            //次、当直できる日
            public DateTime NextAvailable { get; set; } = DateTime.MinValue;
            //日勤回数
            public int DayWorkCount { get; set; } = 0;
            //当直回数
            public int DutyCount { get; set; } = 0;
            //休日出勤回数
            public int RestCount { get; set; } = 0;

            public void AddDutyWorkCount()
            {
                DutyCount++;
                NextAvailable.AddDays(MinDutyGapDays);
            }
            public void AddDayWorkCount()
            {
                DayWorkCount++;
                NextAvailable.AddDays(MinDutyGapDays);
            }

            public void AddRestCount()
            {
                RestCount++;
            }
        }

        /// <summary>
        /// 指定月の当直を作成する。
        /// 1日あたり カテ可1名 + カテ不可1名 を選出し、翌日明け休/週末代休を付与。
        /// 既存データがある場合は優先度（当＞〇＞／）で上書きを避ける。
        /// </summary>
        public void GenerateNightDutiesForMonth(DateTime month)
        {
            //----変数セット
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

            // 初期化
            // まとめた状態管理用の辞書
            var employeeStates = new Dictionary<int, EmployeeWorkState>();

            // 1) 全アクティブ職員の状態を初期化
            foreach (var e in employees_active_all)
            {
                var state = new EmployeeWorkState(); // 新しい状態オブジェクトを作成

                var lastDutyDate = existingMap
                    .Where(kv => kv.Key.Item1 == e.EmployeeId && kv.Value == stidDuty && kv.Key.Item2 < first)
                    .Select(kv => kv.Key.Item2)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();

                // 辞書の代わりにオブジェクトのプロパティに代入
                state.NextAvailable = (lastDutyDate == DateTime.MinValue)
                    ? first
                    : lastDutyDate.AddDays(MinDutyGapDays);

                state.RestCount = 0;
                state.DutyCount = 0;

                // 月初の“明け”をシード
                if (lastDutyDate != DateTime.MinValue)
                {
                    var ake = lastDutyDate.AddDays(1);
                    if (ake >= first && ake <= last)
                        existingMap[(e.EmployeeId, ake.Date)] = stidAfterDuty;
                }

                // まとめた辞書に追加
                employeeStates[e.EmployeeId] = state;
            }

            // 2) 日勤可能者の追加初期化
            foreach (var e in canDayduty)
            {
                // もし上のループで登録されていなければ作成
                if (!employeeStates.ContainsKey(e.EmployeeId))
                {
                    employeeStates[e.EmployeeId] = new EmployeeWorkState { NextAvailable = preloadStart };
                }

                // 日勤カウントを初期化
                employeeStates[e.EmployeeId].DayWorkCount = 0;
            }

            // 5) 選抜アルゴリズム（フェアネス：当月回数が少ない人→次に早く入れる人→ラウンド）
            var upserts = new List<ShiftWrite>();

            //プールから該当者抽出
            Employee? FindEployee(List<Employee> pools, Dictionary<int, EmployeeWorkState> states, DateTime CurrentDay)
            {
                return pools
                 .Where(e => states.GetValueOrDefault(e.EmployeeId)?.NextAvailable <= CurrentDay)
                 .OrderBy(e => states.GetValueOrDefault(e.EmployeeId)?.DutyCount ?? 0)
                 .ThenBy(e => states.GetValueOrDefault(e.EmployeeId)?.DayWorkCount ?? 0)
                 .ThenBy(e => states.GetValueOrDefault(e.EmployeeId)?.RestCount ?? 0)
                 .ThenBy(e => states.GetValueOrDefault(e.EmployeeId)?.NextAvailable ?? DateTime.MinValue)
                 .ThenBy(_ => rand.Next())
                 .FirstOrDefault();
            }

            List<Employee> FilterBySaturday(List<Employee> source, string satClass, DateTime day, bool CanSatWork)
            {
                var filtered = CanSatWork
                    ? source.Where(e => e.SaturdayClass.Equals(satClass, StringComparison.OrdinalIgnoreCase)).ToList()
                    : source.Where(e => !e.SaturdayClass.Equals(satClass, StringComparison.OrdinalIgnoreCase)).ToList();

                if (filtered.Count == 0)
                {
                    Log.Information("No duty candidate found for {day}, using original pool", day);
                    return source.ToList();
                }

                return filtered;
            }

            // --- 共通ヘルパー関数 ---
            Employee? AssignAndLog(
                List<Employee> source,
                string workingClassSat,
                bool? shouldBeWorking,
                string label,
                DateTime day,
                Action<EmployeeWorkState> updateAction) // ← 何を更新するかを「命令」として受け取る
            {
                var pool = shouldBeWorking.HasValue
                    ? FilterBySaturday(source, workingClassSat, day, shouldBeWorking.Value)
                    : source.ToList();

                var candidate = FindEployee(pool, employeeStates, day);

                if (candidate != null)
                {
                    // 見つかったら、渡された「命令（updateAction）」を実行する
                    updateAction(employeeStates[candidate.EmployeeId]);
                }
                else
                {
                    Log.Warning("No {label} found for {day}", label, day);
                }

                return candidate;
            }

            for (var day = first; day <= last; day = day.AddDays(1))
            {
                Models.Employee? cand1 = null, cand2 = null;
                var saturday = day.AddDays(1).Date;
                var workingClassSat = GetWorkingClass(saturday); // 土曜に働く班

                bool? shouldBeWorking = day.DayOfWeek switch
                {
                    DayOfWeek.Friday or DayOfWeek.Sunday => false, // 休み班から選ぶ
                    DayOfWeek.Saturday => true,  // 出勤班から選ぶ
                    _ => null   // 平日はフィルタなし
                };

                // 2) can/cannot それぞれから1人ずつ選んで更新
                cand1 = AssignAndLog(canCath, workingClassSat, shouldBeWorking, "cand1", day, state =>
                {
                    state.AddDutyWorkCount();
                    // 祝日・日曜なら明け休みも追加
                    if (holidays.Contains(day.Date) || day.DayOfWeek == DayOfWeek.Sunday)
                    {
                        state.AddRestCount();
                    }
                });

                cand2 = AssignAndLog(cannotCath, workingClassSat, shouldBeWorking, "cand2", day, state =>
                {
                    state.AddDutyWorkCount();
                    // 祝日・日曜なら明け休みも追加
                    if (holidays.Contains(day.Date) || day.DayOfWeek == DayOfWeek.Sunday)
                    {
                        state.AddRestCount();
                    }
                });

                //-----当直候補者------

                //-----日勤候補者------
                //日勤候補
                Models.Employee? cand3 = null, cand4 = null;
                var workingClass = GetWorkingClass(day);
                //祝日候補
                if (day.DayOfWeek == DayOfWeek.Sunday)
                {
                    var pool3 = canDayduty.Where(e => e.EmployeeId != cand1?.EmployeeId && e.EmployeeId != cand2?.EmployeeId).ToList();
                    var poolSun = FilterBySaturday(pool3, workingClass, day, false);
                    cand3 = AssignAndLog(poolSun, workingClassSat, shouldBeWorking, "cand3", day, state =>
                    {
                        state.AddDayWorkCount(); // 日勤の回数だけを増やす
                    });

                }
                else if (holidays.Contains(day.Date))
                {
                    var pool4 = canDayduty.Where(e => e.EmployeeId != cand1?.EmployeeId && e.EmployeeId != cand2?.EmployeeId).ToList();
                    cand4 = AssignAndLog(pool4, workingClassSat, shouldBeWorking, "cnad4", day, state =>
                    {
                        state.AddDayWorkCount(); // 日勤の回数だけを増やす
                    });
                }

                //-----代休付与------
                Employee? RegisterAssignment(Employee? cand, DateTime day, int statusId, bool isDuty)
                {
                    if (cand == null) return null;

                    // 1. メインの勤務を登録
                    bool placed = TrySetWithPriority(existingMap, upserts, cand.EmployeeId, day, statusId);
                    if (!placed) return null;

                    // 2. 当直なら翌日に「明け休み」を入れる
                    if (isDuty)
                    {
                        TrySetWithPriority(existingMap, upserts, cand.EmployeeId, day.AddDays(1), stidAfterDuty);
                    }

                    // 3. 次回可能日を更新 (間隔をあける)
                    employeeStates[cand.EmployeeId].NextAvailable = day.AddDays(MinDutyGapDays);

                    return cand;
                }

                // 当直の確定
                var p1 = RegisterAssignment(cand1, day, stidDuty, isDuty: true);
                var p2 = RegisterAssignment(cand2, day, stidDuty, isDuty: true);

                // 日勤の確定 (当直と被っていないかチェック)
                var p3 = (cand3?.EmployeeId != cand1?.EmployeeId && cand3?.EmployeeId != cand2?.EmployeeId)
                         ? RegisterAssignment(cand3, day, stidDayWork, isDuty: false) : null;
                var p4 = (cand4?.EmployeeId != cand1?.EmployeeId && cand4?.EmployeeId != cand2?.EmployeeId)
                         ? RegisterAssignment(cand4, day, stidDayWork, isDuty: false) : null;

                // 代休の設定 (週末や祝日の場合)
                var compDay = GetCompWorkOff(day, holidays);
                if (compDay.HasValue)
                {
                    foreach (var p in new[] { p1, p2, p3, p4 }.Where(x => x != null))
                    {
                        SetChildWithOrigin(existingMap, upserts, p!.EmployeeId, compDay.Value, stidSubstituteOff, day);
                        employeeStates[p.EmployeeId].AddRestCount(); // 代休カウントUP
                    }
                }


                // 6) 一括DB反映
                if (upserts.Count > 0)
                {
                    _db.BulkUpsert_Duty_Shifts(upserts, month);
                }


            }

            //----------------------------ここまでシフト生成ロジック-----------------
        }
    }
}
