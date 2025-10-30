using System.Globalization;

namespace Shiftapp_demo.Models
{
    public static class ShiftCsvMapper
    {
        public static IEnumerable<ShiftCsvRow> ToCsvRows(IEnumerable<ShiftRow> rows)
        {
            foreach (var r in rows.OrderBy(x => x.EmployeeId).ThenBy(x => x.Date))
            {
                var (calendar, shiftKind) = MapCalendarAndShiftKind(r.ShiftSymbol, r.Date);

                yield return new ShiftCsvRow
                {
                    個人コード = r.EmployeeId,
                    氏名 = r.EmployeeName ?? string.Empty,
                    処理日 = r.Date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    カレンダー = calendar,                  // 勤務/休日/指定
                    勤怠区分 = "なし",
                    シフト区分 = shiftKind,                // 日勤/半日/当直/明け
                    出勤例外 = "なし",
                    退勤例外 = "なし",
                    修正処理日 = r.Date.ToString("yyyy/M/d", CultureInfo.InvariantCulture)
                };
            }
        }

        /// <summary>
        /// シンボルと日付(曜日)から カレンダー/シフト区分 を決定
        /// </summary>
        public static (string calendar, string shiftKind) MapCalendarAndShiftKind(string? symbol, DateTime date)
        {
            var s = (symbol ?? string.Empty).Trim();

            // なし → 勤務/日勤
            if (string.IsNullOrEmpty(s))
                return ("勤務", "日勤");

            // 代表的な記号
            switch (s)
            {
                case "当": return ("勤務", "当直");
                case "明": return ("勤務", "明け");
                case "●": return ("休日", "日勤");
                case "日": return ("勤務", "日勤");
                case "/": return ("勤務", "半日");
                case "〇":
                case "○": // 丸の別字
                    return (date.DayOfWeek == DayOfWeek.Saturday)
                            ? ("指定", "日勤")  // 土曜の〇 → カレンダー=指定、シフト区分=日勤
                            : ("休日", "日勤"); // それ以外の〇 → カレンダー=休日、シフト区分=日勤
                default:
                    // 未知は安全側で 勤務/日勤 にフォールバック
                    return ("勤務", "日勤");
            }
        }
    }
}
