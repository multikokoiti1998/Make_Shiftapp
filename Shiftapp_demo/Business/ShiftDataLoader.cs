using System.ComponentModel;

namespace Shiftapp_demo.Business
{
    public class ShiftDataLoader : INotifyPropertyChanged
    {
        public int EmployeeId { get; set; }
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
