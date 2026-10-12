using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Shiftapp_demo.Models;
using System.IO;

namespace Shiftapp_demo.Excel
{
    /// <summary>
    /// 勤務変更テンプレート(xlsm)の「deta」シートに月次の勤務記号を書き込み、
    /// 勤務変更YYYYMM.xlsm として別名保存する。
    /// xlsm はVBA・ボタン・拡張条件付き書式・外部リンクを含み、ClosedXML で保存し直すと
    /// それらが失われるため、OpenXML SDK で deta シートのセル値だけを書き換える。
    /// </summary>
    public static class ShiftChangeExcelWriter
    {
        public const string TemplateFileName = "勤務変更テンプレート.xlsm";
        private const string DetaSheetName = "deta";

        // deta シートは4行目〜44行目が職員行(C列=氏名, D列〜AH列=1日〜31日の記号)。
        // 行順は勤務表 (2) の3行目以降と同じ（デイリーデータに出てくる職員順）
        private const int FirstStaffRow = 4;
        private const int LastStaffRow = 44;
        private const int NameColumn = 3;     // C
        private const int FirstDayColumn = 4; // D
        private const int MaxDays = 31;

        public static int Capacity => LastStaffRow - FirstStaffRow + 1;

        /// <summary>
        /// 実際に使用する勤務変更テンプレートのフルパスを返す（ShiftExcelWriter.GetTemplatePath と同じ方針）。
        /// </summary>
        public static string GetTemplatePath()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(local, "ShiftApp", "Data");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var templatePath = Path.Combine(dir, TemplateFileName);

            if (File.Exists(templatePath))
            {
                return templatePath;
            }

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var initialTemplate = Path.Combine(appDir, "Data", TemplateFileName);

            if (File.Exists(initialTemplate))
            {
                File.Copy(initialTemplate, templatePath, overwrite: false);
            }
            else
            {
                throw new FileNotFoundException("勤務変更テンプレートが見つかりません。", initialTemplate);
            }

            return templatePath;
        }

        /// <summary>
        /// テンプレートをコピーし、deta シートに職員名と勤務記号を書き込む。
        /// </summary>
        public static void WriteDetaSheet(string templatePath, string outputPath, IReadOnlyList<ShiftCsvRow> rows)
        {
            var staff = rows
                .GroupBy(r => r.個人コード)
                .Select(g => (Name: g.First().氏名, Days: g.OrderBy(r => r.処理日).ToList()))
                .ToList();

            if (staff.Count > Capacity)
                throw new InvalidOperationException(
                    $"勤務変更テンプレートの deta シートは{Capacity}人分までです（対象職員: {staff.Count}人）。");

            File.Copy(templatePath, outputPath, overwrite: true);

            using var doc = SpreadsheetDocument.Open(outputPath, true);
            var wbPart = doc.WorkbookPart ?? throw new InvalidOperationException("ブックが読み込めません。");

            var sheet = wbPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault(s => s.Name == DetaSheetName)
                ?? throw new InvalidOperationException($"シート '{DetaSheetName}' が見つかりません。");
            var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
            var sheetData = wsPart.Worksheet.GetFirstChild<SheetData>()!;

            for (int i = 0; i < Capacity; i++)
            {
                var rowIndex = (uint)(FirstStaffRow + i);
                var hasStaff = i < staff.Count;

                SetText(sheetData, rowIndex, NameColumn, hasStaff ? staff[i].Name : "");

                for (int d = 0; d < MaxDays; d++)
                {
                    var symbol = hasStaff && d < staff[i].Days.Count ? ToSymbol(staff[i].Days[d]) : "";
                    SetText(sheetData, rowIndex, FirstDayColumn + d, symbol);
                }
            }

            // C列の数式を値に置き換えたため、計算チェーンを破棄して開いたときに全再計算させる
            if (wbPart.CalculationChainPart != null)
                wbPart.DeletePart(wbPart.CalculationChainPart);

            var calcPr = wbPart.Workbook.CalculationProperties ??= new CalculationProperties();
            calcPr.FullCalculationOnLoad = true;

            wbPart.Workbook.Save();
            wsPart.Worksheet.Save();
        }

        /// <summary>
        /// 勤務表テンプレートの「デイリーデータ (2)」E列と同じ規則で記号に変換する。
        /// 休日→●、指定→○、シフト区分が「日…」→空欄、「半…」→／、それ以外は先頭1文字（当・明 など）。
        /// </summary>
        internal static string ToSymbol(ShiftCsvRow r)
        {
            if (r.カレンダー == "休日") return "●";
            if (r.カレンダー == "指定") return "○";
            if (string.IsNullOrEmpty(r.シフト区分)) return "";

            var head = r.シフト区分.Substring(0, 1);
            return head switch
            {
                "日" => "",
                "半" => "／",
                _ => head
            };
        }

        private static void SetText(SheetData sheetData, uint rowIndex, int columnIndex, string text)
        {
            var cell = GetOrCreateCell(sheetData, rowIndex, columnIndex);

            cell.CellFormula = null;
            cell.CellValue = null;

            // 空欄も空文字列として書く（空セルだと Time Pro 入力フォームの VLOOKUP が 0 を返すため。
            // テンプレート元々の deta も空文字列で埋まっていた）
            cell.DataType = CellValues.InlineString;
            cell.InlineString = new InlineString(new Text(text));
        }

        private static Cell GetOrCreateCell(SheetData sheetData, uint rowIndex, int columnIndex)
        {
            var row = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == rowIndex);
            if (row == null)
            {
                row = new Row { RowIndex = rowIndex };
                var next = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value > rowIndex);
                if (next != null) sheetData.InsertBefore(row, next);
                else sheetData.Append(row);
            }

            var reference = ColumnName(columnIndex) + rowIndex;
            var cell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference?.Value == reference);
            if (cell != null) return cell;

            cell = new Cell { CellReference = reference };
            var after = row.Elements<Cell>().FirstOrDefault(c => ColumnIndex(c.CellReference?.Value) > columnIndex);
            if (after != null) row.InsertBefore(cell, after);
            else row.Append(cell);
            return cell;
        }

        private static string ColumnName(int columnIndex)
        {
            var name = "";
            while (columnIndex > 0)
            {
                var m = (columnIndex - 1) % 26;
                name = (char)('A' + m) + name;
                columnIndex = (columnIndex - 1) / 26;
            }
            return name;
        }

        private static int ColumnIndex(string? reference)
        {
            var index = 0;
            foreach (var ch in reference ?? "")
            {
                if (!char.IsLetter(ch)) break;
                index = index * 26 + (ch - 'A' + 1);
            }
            return index;
        }
    }
}
