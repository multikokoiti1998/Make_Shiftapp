using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Shiftapp_demo
{
    public class Technician : INotifyPropertyChanged
    {
        private int _id;
        public int Id { get { return _id; } set { _id = value; OnPropertyChanged(); } }

        private string _name;
        public string Name { get { return _name; } set { _name = value; OnPropertyChanged(); } }

        private string _contact;
        public string Contact { get { return _contact; } set { _contact = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
