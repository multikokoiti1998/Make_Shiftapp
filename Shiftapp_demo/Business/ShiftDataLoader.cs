using System.ComponentModel;

namespace Shiftapp_demo.Business
{
    public class ShiftDataLoader : INotifyPropertyChanged
    {
        public int EmployeeId { get; set; }

        public int ShiftId { get; set; }
        public string EmployeeName { get; set; }

        public int Role { get; set; }

        private Dictionary<string, string> _shifts = new();
        public Dictionary<string, string> Shifts
        {
            get => _shifts;
            set
            {
                _shifts = value;
                OnPropertyChanged(nameof(Shifts));
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
    }
}
