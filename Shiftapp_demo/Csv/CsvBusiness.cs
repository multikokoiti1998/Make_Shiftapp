using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo.Csv
{

    public sealed class CsvBusiness
    {
        private readonly DatabaseHelper _db;
        private readonly IShiftCsvExporter _exporter;

        public CsvBusiness(DatabaseHelper db, IShiftCsvExporter exporter)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        /// <summary>
        /// 指定月の「(その月に1件でも記録がある)全員 × 毎日」を ShiftCsvRow にして返す
        /// ※ 記録が1件も無い社員は含まれません
        /// </summary>
        public IReadOnlyList<ShiftCsvRow> BuildMonthRows(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            // 1) その月の登録分を取得（氏名も含む：GetShiftRowでJOIN済み）
            var rows = _db.GetShiftRow(start, end);

            // 2) 対象日一覧（1日～末日）
            var days = Enumerable.Range(0, (end - start).Days + 1)
                                 .Select(i => start.AddDays(i).Date)
                                 .ToList();

            // 3) 社員ごとにグルーピング（GetShiftRowに出てきた社員のみ対象）
            var result = new List<ShiftCsvRow>();

            foreach (var g in rows.GroupBy(r => new { r.EmployeeId, r.EmployeeName })
                                  .OrderBy(g => g.Key.EmployeeId))
            {
                // その社員の「日付 → シンボル」マップ
                var dayMap = g.ToDictionary(x => x.Date.Date, x => x.ShiftSymbol ?? "");

                foreach (var d in days)
                {
                    dayMap.TryGetValue(d, out var symbol);
                    var (calendar, shiftKind) = ShiftCsvMapper.MapCalendarAndShiftKind(symbol, d);

                    result.Add(new ShiftCsvRow
                    {
                        個人コード = g.Key.EmployeeId,
                        氏名 = g.Key.EmployeeName ?? string.Empty,
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
        /// 指定月のCSVを縦形式で出力（1行＝1社員×1日）
        /// </summary>
        public Task ExportMonthAsRowsAsync(int year, int month, string filePath, CancellationToken ct = default)
        {
            var data = BuildMonthRows(year, month);
            var layout = new RowLayout_ForShiftCsvRow(); // ShiftCsvRow用レイアウト
            return _exporter.ExportAsync(data, layout, filePath, ct);
        }
    }

}
