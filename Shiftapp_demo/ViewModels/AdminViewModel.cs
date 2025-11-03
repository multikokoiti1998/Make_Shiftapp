using Shiftapp_demo.Business;
using Shiftapp_demo.DataAccess;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Shiftapp_demo.ViewModels
{
    public class AdminViewModel : INotifyPropertyChanged
    {
        private ShiftDataLoader _dataLoader;

        private readonly DatabaseHelper db;

        private readonly ShiftBusiness _business;

        // INotifyPropertyChanged の実装
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        // コマンドひな形


    }
}
