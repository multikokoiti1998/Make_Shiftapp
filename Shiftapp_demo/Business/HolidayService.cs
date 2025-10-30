namespace Shiftapp_demo.Business
{
    internal class HolidayService
    {
        //週末の休日
        public sealed class WeekendHolidayService
        {
            private readonly bool _treatSaturdayAsHoliday;

            public WeekendHolidayService(bool treatSaturdayAsHoliday = false)
            {
                _treatSaturdayAsHoliday = treatSaturdayAsHoliday;
            }

            public bool IsHoliday(DateTime d)
            {
                if (d.DayOfWeek == DayOfWeek.Sunday) return true;
                if (_treatSaturdayAsHoliday && d.DayOfWeek == DayOfWeek.Saturday) return true;
                return false;
            }
        }

        // 祝日テーブルがある場合の例
        public sealed class TableHolidayService
        {
            private readonly HashSet<DateTime> _holidays; // yyyy-MM-dd 日付のみ
            private readonly bool _includeWeekends;

            public TableHolidayService(IEnumerable<DateTime> holidays, bool includeWeekends = true)
            {
                _holidays = holidays.Select(d => d.Date).ToHashSet();
                _includeWeekends = includeWeekends;
            }

            public bool IsHoliday(DateTime d)
            {
                if (_includeWeekends && (d.DayOfWeek == DayOfWeek.Sunday)) return true;
                return _holidays.Contains(d.Date);
            }
        }
    }
}
