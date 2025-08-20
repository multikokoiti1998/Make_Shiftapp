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

        public ShiftBusiness(DatabaseHelper db) => _db = db;

        public static List<DateTime> GetSaturdaysInMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);
            var list = new List<DateTime>();
            for (var d = first; d <= last; d = d.AddDays(1))
                if (d.DayOfWeek == DayOfWeek.Saturday) list.Add(d);
            return list;
        }

        // 例：A班は偶数週、B班は奇数週に当直「当」を入れる などの単純ルール
        public void UpdateSaturdayShifts(
            DateTime month,
            DateTime rotationStartSaturday,          // 例: 2025-08-16 の土曜
            string worksClassAtBaseline = "B"        // 基準週に出勤する班 ("A" or "B")
              )
        {
            // 1) データ取得
            var employees = _db.GetActiveEmployeesWithSaturdayClass(); // EmployeeId, SaturdayClass("A"/"B")
            var saturdays = GetSaturdaysInMonth(month);

            // シンボルID取得（※あなたのDBに合わせて）
            var stidWork = _db.GetShiftTypeIdBySymbol("/");   // 出勤
            var stidOff = _db.GetShiftTypeIdBySymbol("〇");   // 休み
            var stidDuty = _db.GetShiftTypeIdBySymbol("当");  // 当直

            // 月間の既存シフト（当直など）があれば優先したいので先に取得
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
                int k = SaturdayIndexFromBaseline(sat, rotationStartSaturday);

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
                    var key = (emp.EmployeeId, sat);

                    // 既に“当直”が入っている場合はそれを尊重（上書きしない）
                    if (existing.TryGetValue(key, out var currentStid) && currentStid == stidDuty)
                    {
                        // 当直を優先: 何も入れない（＝現状維持）
                        continue;
                    }

                    // 出勤者には "/"、休みには "〇" を入れる
                    assigns.Add((emp.EmployeeId, sat, isWorker ? stidWork : stidOff));
                }
            }

            // 4) 一括Upsert（is_active=1 は SQL 側で面倒みる実装のままでOK）
            if (assigns.Count > 0)
                _db.BulkUpsertShifts(assigns);
        }
    }
}
