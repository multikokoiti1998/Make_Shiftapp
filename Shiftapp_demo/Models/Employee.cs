using System.ComponentModel;

namespace Shiftapp_demo.Models
{
    public class Employee : INotifyPropertyChanged
    {
        // --- データベースの employee テーブルと対応 ---

        private int _employeeId;
        public int EmployeeId { get => _employeeId; set { if (_employeeId != value) { _employeeId = value; Raise(nameof(EmployeeId)); IsDirty = true; } } }

        private string _employeeName = "";
        public string EmployeeName { get => _employeeName; set { if (_employeeName != value) { _employeeName = value; Raise(nameof(EmployeeName)); IsDirty = true; } } }

        private bool _canDoCatheterization;
        public bool CanDoCatheterization { get => _canDoCatheterization; set { if (_canDoCatheterization != value) { _canDoCatheterization = value; Raise(nameof(CanDoCatheterization)); IsDirty = true; } } }

        private string _saturdayClass = "A"; // DBはTEXTで "A"/"B" 推奨
        public string SaturdayClass { get => _saturdayClass; set { if (_saturdayClass != value) { _saturdayClass = value; Raise(nameof(SaturdayClass)); IsDirty = true; } } }

        private int _monthlyDutyLimit;
        public int MonthlyDutyLimit { get => _monthlyDutyLimit; set { if (_monthlyDutyLimit != value) { _monthlyDutyLimit = value; Raise(nameof(MonthlyDutyLimit)); IsDirty = true; } } }

        private bool _canDoNightDuty;
        public bool CanDoNightDuty { get => _canDoNightDuty; set { if (_canDoNightDuty != value) { _canDoNightDuty = value; Raise(nameof(CanDoNightDuty)); IsDirty = true; } } }

        private int _role;
        public int Role { get => _role; set { if (_role != value) { _role = value; Raise(nameof(Role)); IsDirty = true; } } }

        private bool _canDayDuty;
        public bool CanDayDuty { get => _canDayDuty; set { if (_canDayDuty != value) { _canDayDuty = value; Raise(nameof(CanDayDuty)); IsDirty = true; } } }

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
        //UI更新フラグ
        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); } }
        }


        public virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
