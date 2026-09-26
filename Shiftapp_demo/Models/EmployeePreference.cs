using System.ComponentModel;

namespace Shiftapp_demo.Models
{
    public enum PreferencePolarity
    {
        Prefer = 1,
        Avoid = -1,
    }

    // 技師ごとの勤務希望（複数登録可）。DayOfWeekとIsWeekendは排他（IsWeekend=trueならDayOfWeekはnull）。
    public class EmployeePreference : INotifyPropertyChanged
    {
        private int _preferenceId;
        public int PreferenceId { get => _preferenceId; set { if (_preferenceId != value) { _preferenceId = value; Raise(nameof(PreferenceId)); IsDirty = true; } } }

        private int _employeeId;
        public int EmployeeId { get => _employeeId; set { if (_employeeId != value) { _employeeId = value; Raise(nameof(EmployeeId)); IsDirty = true; } } }

        private DayOfWeek? _dayOfWeek;
        public DayOfWeek? DayOfWeek { get => _dayOfWeek; set { if (_dayOfWeek != value) { _dayOfWeek = value; Raise(nameof(DayOfWeek)); IsDirty = true; } } }

        private bool _isWeekend;
        public bool IsWeekend { get => _isWeekend; set { if (_isWeekend != value) { _isWeekend = value; Raise(nameof(IsWeekend)); IsDirty = true; } } }

        private PreferencePolarity _polarity = PreferencePolarity.Prefer;
        public PreferencePolarity Polarity { get => _polarity; set { if (_polarity != value) { _polarity = value; Raise(nameof(Polarity)); IsDirty = true; } } }

        private int _weight = 1;
        public int Weight { get => _weight; set { if (_weight != value) { _weight = value; Raise(nameof(Weight)); IsDirty = true; } } }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set { if (_isActive != value) { _isActive = value; Raise(nameof(IsActive)); IsDirty = true; } } }

        // 画面表示用（例: "水曜日" / "週末"）
        public string DisplayLabel => IsWeekend ? "週末" : (DayOfWeek?.ToString() ?? "");

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); } }
        }

        public void AcceptChanges()
        {
            IsDirty = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
