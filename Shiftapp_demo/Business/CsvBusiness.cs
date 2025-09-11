using CsvHelper;
using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Shiftapp_demo.Models.ShiftCsvRow;

namespace Shiftapp_demo.Business
{
    internal class CsvBusiness
    {
        private readonly DatabaseHelper _db;

        public CsvBusiness(DatabaseHelper db)
        {
            _db = db;

        }

        // シンボル → CSV「シフト区分」変換（必要に応じて調整）
        private static string MapSymbolToShiftName(string? symbol)
            => symbol switch
            {
                "半" => "半日",
                "当" => "当直",
                "〇" => "日勤",
                "/" => "日勤",
                null or "" => "日勤",
                _ => "日勤"
            };

        public async Task ExportMonthAsync(DateTime month, string savePath, CancellationToken ct = default)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            // ① 全社員
            var employees = _db.GetAllEmployees();

            // ② 最新レコードを辞書化 (eid,date) → (symbol, reg, name)
            var latestList = _db.GetShiftsOnly(first, last);

            var latestMap = latestList.ToDictionary(
                x => (x.EmployeeId, x.ShiftDate.Date),
                x => (x.Symbol, x.EmployeeName)
            );

            // ③ 全員×全日で前処理してCSV行を作る
            var rows = new List<ShiftCsvRow>(capacity: employees.Count * DateTime.DaysInMonth(month.Year, month.Month));

            foreach (var e in employees)
            {
                int eid = e.EmployeeId;

                string name = e.EmployeeName;

                for (var d = first; d <= last; d = d.AddDays(1))
                {
                    ct.ThrowIfCancellationRequested();

                    latestMap.TryGetValue((eid, d.Date), out var found);

                    var symbol = found.Symbol; // null可

                    rows.Add(new ShiftCsvRow
                    {
                        個人コード = eid,
                        氏名 = name,
                        処理日 = d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                        カレンダー = _holiday.IsHoliday(d) ? "休日" : "勤務",
                        勤怠区分 = "なし",
                        シフト区分 = MapSymbolToShiftName(symbol),
                        出勤例外 = "なし",
                        退勤例外 = "なし",
                        修正処理日 = d.ToString("yyyy/M/d", CultureInfo.InvariantCulture)
                    });
                }
            }


        }
    }
}

