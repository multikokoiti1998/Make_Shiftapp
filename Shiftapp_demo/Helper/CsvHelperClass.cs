using Shiftapp_demo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shiftapp_demo.DataAccess;


namespace Shiftapp_demo.Helper
{
     public class CsvHelperClass
    {
        // CSV行（列名そのまま）
        public sealed class ShiftCsvRow
        {
            public int 個人コード { get; init; }
            public string 氏名 { get; init; } = "";
            public string 処理日 { get; init; } = ""; // yyyy/MM/dd
            public string カレンダー { get; init; } = ""; // 勤務 or 休日
            public string 勤怠区分 { get; init; } = "なし";
            public string シフト区分 { get; init; } = ""; // 日勤/半日/当直 など
            public string 出勤例外 { get; init; } = "なし";
            public string 退勤例外 { get; init; } = "なし";
            public string 修正処理日 { get; init; } = ""; // yyyy/M/d
        }

        // DBから拾う“最新レコード”
        public sealed class LatestShiftRecord
        {
            public int EmployeeId { get; init; }
            public string EmployeeName { get; init; } = "";
            public DateTime Date { get; init; }
            public string? Symbol { get; init; } // "/", "半", "当", "〇" など
            public DateTime? RegisteredAt { get; init; } // null許容（無い環境もあるため）
        }

        // 祝日判定（差し替え可能）
        public interface IHolidayService
        {
            bool IsHoliday(DateTime d);
        }

        // CSV書き出し（CsvHelper実装を後述）
        public interface IShiftCsvExporter
        {
            Task ExportAsync(IEnumerable<ShiftCsvRow> rows, string filePath, CancellationToken ct = default);
        }



    }
}
