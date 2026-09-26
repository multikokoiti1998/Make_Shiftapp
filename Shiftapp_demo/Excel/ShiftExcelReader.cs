using ClosedXML.Excel;

namespace Shiftapp_demo.Excel
{
    public sealed class ShiftExcelEntry
    {
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = "";
        public DateTime Date { get; init; }
        public string Symbol { get; init; } = "";
    }

    public sealed class ShiftExcelImportResult
    {
        public int Year { get; init; }
        public int Month { get; init; }
        public IReadOnlyList<ShiftExcelEntry> Entries { get; init; } = Array.Empty<ShiftExcelEntry>();
    }

    /// <summary>
    /// 完成形の勤務表(xlsx)「勤務表 (2)」シートを読み込む。
    /// レイアウト: 1行目=日付ヘッダー(D列以降)、3行目以降=職員(B列=個人コード, C列=氏名, D列以降=シフト記号)
    /// </summary>
    public static class ShiftExcelReader
    {
        public const string DefaultSheetName = "勤務表 (2)";

        private const int HeaderRow = 1;
        private const int DataStartRow = 3;
        private const int EmployeeIdColumn = 2;
        private const int EmployeeNameColumn = 3;
        private const int FirstDateColumn = 4;

        // 全角スラッシュ(／)はDB側のシンボル("/")と揃える
        private const char FullWidthSlash = '／';

        public static ShiftExcelImportResult Read(string filePath, string sheetName = DefaultSheetName)
        {
            using var wb = new XLWorkbook(filePath);

            if (!wb.Worksheets.TryGetWorksheet(sheetName, out var ws))
                throw new InvalidOperationException($"シート '{sheetName}' が見つかりません。");

            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

            var dateColumns = new List<(int Column, DateTime Date)>();
            for (int col = FirstDateColumn; col <= lastCol; col++)
            {
                var cell = ws.Cell(HeaderRow, col);
                if (cell.DataType == XLDataType.DateTime)
                    dateColumns.Add((col, cell.GetDateTime().Date));
            }

            if (dateColumns.Count == 0)
                throw new InvalidOperationException("日付列が見つかりませんでした。");

            var year = dateColumns[0].Date.Year;
            var month = dateColumns[0].Date.Month;

            var entries = new List<ShiftExcelEntry>();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

            for (int row = DataStartRow; row <= lastRow; row++)
            {
                var idCell = ws.Cell(row, EmployeeIdColumn);
                if (!idCell.TryGetValue<int>(out var employeeId) || employeeId <= 0)
                    continue;

                var name = ws.Cell(row, EmployeeNameColumn).GetString().Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                foreach (var (col, date) in dateColumns)
                {
                    var symbol = NormalizeSymbol(ws.Cell(row, col).GetString());

                    entries.Add(new ShiftExcelEntry
                    {
                        EmployeeId = employeeId,
                        EmployeeName = name,
                        Date = date,
                        Symbol = symbol
                    });
                }
            }

            return new ShiftExcelImportResult { Year = year, Month = month, Entries = entries };
        }

        private static string NormalizeSymbol(string raw)
        {
            var s = raw.Trim();
            return s.Replace(FullWidthSlash, '/');
        }
    }
}
