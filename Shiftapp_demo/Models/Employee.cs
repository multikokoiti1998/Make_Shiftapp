namespace Shiftapp_demo.Models
{
    public class Employee
    {
        // --- データベースの employee テーブルと対応 ---
        //ToDo　当直可能かなど対応させてない

        public int EmployeeId { get; set; }               // DB: employee_id
        public string EmployeeName { get; set; } = "";    // DB: employee_name

        public bool CanDoCatheterization { get; set; }    // DB: CanDoCatheterization（0 or 1 → bool）

        public String SaturdayClass { get; set; }            // DB: saturday_class（例: 0=A班, 1=B班）
        public int MonthlyDutyLimit { get; set; }         // DB: MonthlyDutyLimit（最大勤務数）

        public int CanDoNightDuty { get; set; }

        public int Role { get; set; }

        public int CanDayNightDuty { get; set; }

        public int is_active { get; set; }

        // --- シフト状況の辞書（動的データ） ---
        public Dictionary<DateTime, string> ShiftMap { get; set; } = new();

        // --- インデクサー：日付から直接シフト記号を取得・設定 ---
        public string this[DateTime date]
        {
            get => ShiftMap.TryGetValue(date, out var v) ? v : "";
            set => ShiftMap[date] = value;
        }

        // --- 月間の当直回数（A・B）を数える ---
        public int GetDutyCount(DateTime month)
        {
            return ShiftMap.Count(kv =>
                kv.Key.Year == month.Year &&
                kv.Key.Month == month.Month &&
                (kv.Value == "当")); // A or Bが当直とみなす
        }
    }
}
