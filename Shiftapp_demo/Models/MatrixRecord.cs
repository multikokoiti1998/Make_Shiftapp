namespace Shiftapp_demo.Models
{
    public sealed class MatrixRecord
    {
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = "";
        public Dictionary<DateTime, string> Map { get; } = new();
    }
}
