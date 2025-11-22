using System.ComponentModel;

namespace Shiftapp_demo.Business
{
    public class ShiftDataLoader : INotifyPropertyChanged
    {
        private int _employeeId { get; set; }

        public int EmployeeId { get => _employeeId; set { if (_shiftId != value) { _employeeId = value; Raise(nameof(EmployeeId)); IsDirty = true; } } }

        private int _shiftId { get; set; }
        public int ShiftId { get => _shiftId; set { if (_shiftId != value) { _shiftId = value; Raise(nameof(ShiftId)); IsDirty = true; } } }

        private string _employeeName { get; set; }
        public string EmployeeName { get => _employeeName; set { if (_employeeName != value) { _employeeName = value; Raise(nameof(EmployeeId)); IsDirty = true; } } }

        private int _role;
        public int Role { get => _role; set { if (_role!= value) {_role= value; Raise(nameof(Role)); IsDirty = true; } } }

        private Dictionary<string, string> _shifts = new();
        public Dictionary<string, string> Shifts
        {
            get => _shifts;
            set
            {
                _shifts = value;

                OnPropertyChanged(nameof(Shifts));
                
                IsDirty=true;
            }
        }

        //UI更新フラグ
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
