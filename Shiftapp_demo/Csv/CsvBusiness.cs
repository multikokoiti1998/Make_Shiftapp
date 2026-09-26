using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Excel;
using Shiftapp_demo.Models;
using System.Globalization;

namespace Shiftapp_demo.Csv
{

    public sealed class CsvBusiness
    {
        private readonly MainDatabaseHelper _db;

        public CsvBusiness(MainDatabaseHelper db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 「在職中の全職員 × 毎日」を ShiftCsvRow にして返す（役職→職員コード順）。
        /// その月にシフト実績が1件も無い新人・非正規等も、当日分は空欄のまま含める。
        /// </summary>
        public IReadOnlyList<ShiftCsvRow> BuildMonthRows(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            // 1) その月の実績（氏名も含む：GetShiftRowでJOIN済み）を職員ごとの日付→記号マップにする
            var shiftsByEmployee = _db.GetShiftRow(start, end)
                .GroupBy(r => r.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Date.Date, x => x.ShiftSymbol ?? ""));

            // 2) 対象日一覧（1日～末日）
            var days = Enumerable.Range(0, (end - start).Days + 1)
                                 .Select(i => start.AddDays(i).Date)
                                 .ToList();

            // 3) 在職中の職員は全員対象にする（実績が1件も無い新人・非正規等も空欄のまま出力する。
            //    以前はGetShiftRowに出てきた職員だけを対象にしていたため、まだ実績の無い
            //    新規登録者が丸ごと抜け落ち、後続の職員が本来の並び順より前に詰まって
            //    見えてしまっていた）
            var employees = _db.GetActiveEmployeesOrdered();

            var result = new List<ShiftCsvRow>();
            foreach (var emp in employees)
            {
                shiftsByEmployee.TryGetValue(emp.EmployeeId, out var dayMap);
                dayMap ??= new Dictionary<DateTime, string>();

                foreach (var d in days)
                {
                    dayMap.TryGetValue(d, out var symbol);
                    var (calendar, shiftKind) = ShiftCsvMapper.MapCalendarAndShiftKind(symbol, d);

                    result.Add(new ShiftCsvRow
                    {
                        個人コード = emp.EmployeeId,
                        氏名 = emp.EmployeeName ?? string.Empty,
                        処理日 = d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                        カレンダー = calendar,
                        勤怠区分 = "なし",
                        シフト区分 = shiftKind,
                        出勤例外 = "なし",
                        退勤例外 = "なし",
                        修正処理日 = d.ToString("yyyy/M/d", CultureInfo.InvariantCulture)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 指定月のデータを勤務表テンプレートの「デイリーデータ」シートに書き込み、
        /// 完成形の勤務表(xlsx)として出力する。
        /// </summary>
        public Task ExportMonthAsExcelAsync(int year, int month, string templatePath, string outputPath, CancellationToken ct = default)
        {
            var data = BuildMonthRows(year, month);

            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var holidayDates = _db.GetHolidays(start, end).Select(h => h.date.Date).ToList();

            return Task.Run(() => ShiftExcelWriter.WriteMonthlyData(templatePath, outputPath, year, month, data, holidayDates), ct);
        }
    }

}
