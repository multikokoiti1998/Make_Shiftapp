using ClosedXML.Excel;
using Shiftapp_demo.Models;
using System.Globalization;
using System.IO;

namespace Shiftapp_demo.Excel
{
    /// <summary>
    /// 勤務表テンプレート(xlsx)の「デイリーデータ」シートに月次データを書き込み、
    /// 完成形の勤務表として別名保存する。
    /// レイアウトは ShiftExcelReader が読む「勤務表 (2)」シートとは別で、
    /// テンプレート内の数式（デイリーデータ (2) → 勤務表 (2) 等）が
    /// このシートを参照して他シートを自動再計算する。
    /// </summary>
    public static class ShiftExcelWriter
    {
        public const string DailySheetName = "デイリーデータ";

        private const int HeaderRow = 1;
        private const int DataStartRow = 2;
        private const int ColumnCount = 9; // 個人コード〜修正処理日

        /// <summary>
        /// 実際に使用するテンプレートファイルのフルパスを返す。
        /// ・基本は %LocalAppData%\ShiftApp\Data\勤務表テンプレート.xlsx
        /// ・存在しなければ実行ファイルと同じ場所の Data\勤務表テンプレート.xlsx からコピーする
        /// </summary>
        public static string GetTemplatePath()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(local, "ShiftApp", "Data");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var templatePath = Path.Combine(dir, "勤務表テンプレート.xlsx");

            if (File.Exists(templatePath))
            {
                return templatePath;
            }

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var initialTemplate = Path.Combine(appDir, "Data", "勤務表テンプレート.xlsx");

            if (File.Exists(initialTemplate))
            {
                File.Copy(initialTemplate, templatePath, overwrite: false);
            }
            else
            {
                throw new FileNotFoundException("初期テンプレートが見つかりません。", initialTemplate);
            }

            return templatePath;
        }

        /// <summary>
        /// テンプレートを開き、「デイリーデータ」シートを指定行で置き換えて別名保存する。
        /// </summary>
        public static void WriteDailyData(string templatePath, string outputPath, IReadOnlyList<ShiftCsvRow> rows)
        {
            using var wb = new XLWorkbook(templatePath);

            if (!wb.Worksheets.TryGetWorksheet(DailySheetName, out var ws))
                throw new InvalidOperationException($"シート '{DailySheetName}' が見つかりません。");

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? HeaderRow;
            if (lastRow >= DataStartRow)
            {
                ws.Range(DataStartRow, 1, lastRow, ColumnCount).Clear(XLClearOptions.Contents);
            }

            var row = DataStartRow;
            foreach (var r in rows)
            {
                ws.Cell(row, 1).Value = r.個人コード;
                ws.Cell(row, 2).Value = r.氏名;
                ws.Cell(row, 3).Value = ParseDate(r.処理日);
                ws.Cell(row, 4).Value = r.カレンダー;
                ws.Cell(row, 5).Value = r.勤怠区分;
                ws.Cell(row, 6).Value = r.シフト区分;
                ws.Cell(row, 7).Value = r.出勤例外;
                ws.Cell(row, 8).Value = r.退勤例外;
                ws.Cell(row, 9).Value = ParseDate(r.修正処理日);
                row++;
            }

            wb.SaveAs(outputPath);
        }

        private static DateTime ParseDate(string s) =>
            DateTime.Parse(s, CultureInfo.InvariantCulture);
    }
}
