namespace Shiftapp_demo.Models
{
    public class Shift
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public DateTime ShiftDate { get; set; }
        public string Symbol { get; set; }

        // 代休/明けの場合、元になった当直/日勤の日付とその記号（GUIのツールチップ表示用）
        public DateTime? OriginDate { get; set; }
        public string? OriginSymbol { get; set; }
    }
}
