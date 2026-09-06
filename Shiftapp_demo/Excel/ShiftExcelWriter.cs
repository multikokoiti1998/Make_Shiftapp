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
        private const string StaffSheetName = "スタッフ";
        private const string MonthDisplaySheetName = "月表示";

        private const int HeaderRow = 1;
        private const int DataStartRow = 2;
        private const int ColumnCount = 9; // 個人コード〜修正処理日

        // スタッフシートは2行目(先頭職員)〜100行目まで職員行を持つ
        private const int StaffFirstDataRow = 3;
        private const int StaffLastRow = 100;

        // 月表示シートは3行目=1日目、以降2行おきに1日ずつ進む。C列が休日フラグ(1=休日/0=平日)
        private const int MonthDisplayFirstDayRow = 3;
        private const int MonthDisplayHolidayColumn = 3;

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
        /// テンプレートを開き、月次データ・休日フラグを書き込んで別名保存する。
        /// </summary>
        public static void WriteMonthlyData(
            string templatePath,
            string outputPath,
            int year,
            int month,
            IReadOnlyList<ShiftCsvRow> rows,
            IEnumerable<DateTime> holidayDates)
        {
            using var wb = new XLWorkbook(templatePath);

            WriteDailySheet(wb, rows);
            FixStaffDisplayNameFormulas(wb);
            WriteMonthDisplayHolidayFlags(wb, year, month, holidayDates);

            wb.SaveAs(outputPath);
        }

        private static void WriteDailySheet(XLWorkbook wb, IReadOnlyList<ShiftCsvRow> rows)
        {
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
        }

        /// <summary>
        /// 「スタッフ」シートのD列(氏)/E列(名)/F列(表示名)は共有数式(shared formula)で
        /// 組まれているが、ClosedXMLで開いて保存し直すと、F列の共有数式内にある
        /// 列全体参照 "D:D" が2行目以降のコピー先セルで "D" に壊れてしまう既知の問題があり、
        /// 結果としてF4以降の表示名が正しく計算されなくなる（当直者/明/●等の各カレンダーシートは
        /// この表示名をVLOOKUPで参照しているため、月表示の氏名表示も崩れる）。
        /// 共有数式に頼らず、各行に個別の数式を明示的に書き込むことでこれを回避する。
        /// </summary>
        private static void FixStaffDisplayNameFormulas(XLWorkbook wb)
        {
            if (!wb.Worksheets.TryGetWorksheet(StaffSheetName, out var ws))
                return;

            for (int r = StaffFirstDataRow; r <= StaffLastRow; r++)
            {
                ws.Cell(r, 4).FormulaA1 = $"IFERROR(LEFT(C{r},FIND(\" \",C{r})-1),\"\")";
                ws.Cell(r, 5).FormulaA1 = $"IFERROR(RIGHT(C{r},LEN(C{r})-FIND(\" \",C{r})),\"\")";
                ws.Cell(r, 6).FormulaA1 = $"IF(COUNTIF(D:D,D{r})=1,D{r},D{r}&LEFT(E{r},1))";
            }
        }

        /// <summary>
        /// 「月表示」シートC列(休日フラグ)は毎月手入力する運用になっているため、
        /// 日曜日とDB登録済みの祝日を1、それ以外を0として出力時に埋める。
        /// </summary>
        private static void WriteMonthDisplayHolidayFlags(
            XLWorkbook wb,
            int year,
            int month,
            IEnumerable<DateTime> holidayDates)
        {
            if (!wb.Worksheets.TryGetWorksheet(MonthDisplaySheetName, out var ws))
                return;

            var holidaySet = new HashSet<DateTime>(holidayDates.Select(d => d.Date));
            var daysInMonth = DateTime.DaysInMonth(year, month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day);
                var isHoliday = date.DayOfWeek == DayOfWeek.Sunday || holidaySet.Contains(date);
                var row = MonthDisplayFirstDayRow + 2 * (day - 1);
                ws.Cell(row, MonthDisplayHolidayColumn).Value = isHoliday ? 1 : 0;
            }
        }

        private static DateTime ParseDate(string s) =>
            DateTime.Parse(s, CultureInfo.InvariantCulture);
    }
}
