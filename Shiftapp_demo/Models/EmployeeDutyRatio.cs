namespace Shiftapp_demo.Models
{
    // 管理者画面：職員ごとの全期間の当直回数と、うち週末(土日)・祝日当直の回数・比率
    public class EmployeeDutyRatio
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";
        public int TotalDutyCount { get; set; }
        public int WeekendHolidayDutyCount { get; set; }
        public double Ratio => TotalDutyCount == 0 ? 0.0 : (double)WeekendHolidayDutyCount / TotalDutyCount;
    }
}
