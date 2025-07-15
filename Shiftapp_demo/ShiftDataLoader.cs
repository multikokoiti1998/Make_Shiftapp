using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftapp_demo
{
    public class ShiftData : INotifyPropertyChanged
    {
        public int EmployeeId { get; set; } // <-- これがID列に対応
        public string EmployeeName { get; set; } // <-- これが名前列に対応

        private Dictionary<DateTime, string> _shifts = new Dictionary<DateTime, string>();
        public Dictionary<DateTime, string> Shifts { /* ... 省略 ... */ }

        // インデクサーは日付列のデータバインディングに利用
        public string this[DateTime date] { /* ... 省略 ... */ }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) { /* ... 省略 ... */ }
    }
}
