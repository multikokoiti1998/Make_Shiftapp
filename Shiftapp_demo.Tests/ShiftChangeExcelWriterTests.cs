using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Shiftapp_demo.Excel;
using Shiftapp_demo.Models;
using System.Globalization;
using Xunit;

namespace Shiftapp_demo.Tests
{
    public class ShiftChangeExcelWriterTests
    {
        private static string TemplatePath =>
            Path.Combine(AppContext.BaseDirectory, "Data", ShiftChangeExcelWriter.TemplateFileName);

        private static List<ShiftCsvRow> BuildRows(int year, int month, params (int Id, string Name, string[] Symbols)[] staff)
        {
            var rows = new List<ShiftCsvRow>();
            var days = DateTime.DaysInMonth(year, month);
            foreach (var (id, name, symbols) in staff)
            {
                for (int d = 1; d <= days; d++)
                {
                    var date = new DateTime(year, month, d);
                    var symbol = d <= symbols.Length ? symbols[d - 1] : "";
                    var (calendar, kind) = ShiftCsvMapper.MapCalendarAndShiftKind(symbol, date);
                    rows.Add(new ShiftCsvRow
                    {
                        個人コード = id,
                        氏名 = name,
                        処理日 = date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                        カレンダー = calendar,
                        シフト区分 = kind,
                        修正処理日 = date.ToString("yyyy/M/d", CultureInfo.InvariantCulture)
                    });
                }
            }
            return rows;
        }

        [Fact]
        public void WriteDetaSheet_書き込んだ氏名と記号がdetaに入りマクロも残る()
        {
            // 2026/10/3 は土曜（○→指定のまま）、10/5 は月曜（○→休日=●）
            var rows = BuildRows(2026, 10,
                (101, "山田 太郎", new[] { "当", "明", "○", "●", "○", "/", "" }),
                (102, "佐藤 花子", new[] { "", "当", "明" }));

            var output = Path.Combine(Path.GetTempPath(), $"勤務変更_test_{Guid.NewGuid():N}.xlsm");
            try
            {
                ShiftChangeExcelWriter.WriteDetaSheet(TemplatePath, output, rows);

                using (var doc = SpreadsheetDocument.Open(output, false))
                {
                    Assert.NotNull(doc.WorkbookPart!.VbaProjectPart);
                }

                using var wb = new XLWorkbook(output);
                var ws = wb.Worksheet("deta");

                Assert.Equal("山田 太郎", ws.Cell("C4").GetString());
                Assert.False(ws.Cell("C4").HasFormula);
                Assert.Equal(new[] { "当", "明", "○", "●", "●", "／", "" },
                    Enumerable.Range(4, 7).Select(c => ws.Cell(4, c).GetString()).ToArray());

                Assert.Equal("佐藤 花子", ws.Cell("C5").GetString());
                Assert.Equal("当", ws.Cell("E5").GetString());

                // 31日目(AH列)も対象、職員のいない行は空欄にする
                Assert.Equal("", ws.Cell("AH4").GetString());
                Assert.Equal("", ws.Cell("C6").GetString());
                Assert.Equal("", ws.Cell("F44").GetString());

                // 範囲外(45行目以降)は触らない
                Assert.True(ws.Cell("C45").HasFormula);
            }
            finally
            {
                File.Delete(output);
            }
        }

        [Fact]
        public void WriteDetaSheet_定員を超えるとエラー()
        {
            var staff = Enumerable.Range(1, ShiftChangeExcelWriter.Capacity + 1)
                .Select(i => (i, $"職員 {i}", Array.Empty<string>()))
                .ToArray();
            var rows = BuildRows(2026, 10, staff);

            var output = Path.Combine(Path.GetTempPath(), $"勤務変更_test_{Guid.NewGuid():N}.xlsm");
            Assert.Throws<InvalidOperationException>(() =>
                ShiftChangeExcelWriter.WriteDetaSheet(TemplatePath, output, rows));
            Assert.False(File.Exists(output));
        }
    }
}
