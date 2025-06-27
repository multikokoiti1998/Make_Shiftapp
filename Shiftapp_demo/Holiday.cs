using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Shiftapp_demo
{
    public class Holiday : INotifyPropertyChanged
    {
        private DateTime _date;
        public DateTime Date { get { return _date; } set { _date = value; OnPropertyChanged(); } }

        private string _description;
        public string Description { get { return _description; } set { _description = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
