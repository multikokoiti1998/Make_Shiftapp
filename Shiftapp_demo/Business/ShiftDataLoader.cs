using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Shiftapp_demo.Business
{
    public class ShiftDataLoader : INotifyPropertyChanged
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }

        private Dictionary<DateTime, string> _shifts = new();
        public Dictionary<DateTime, string> Shifts
        {
            get => _shifts;
            set
            {
                _shifts = value;
                OnPropertyChanged(nameof(Shifts));
            }
        }

        // インデクサー：DataGrid動的列のBindingに使う
        public string this[DateTime date]
        {
            get => _shifts.TryGetValue(date.Date, out var val) ? val : "";
            set
            {
                _shifts[date.Date] = value;
                OnPropertyChanged($"Item[{date.Date}]");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
