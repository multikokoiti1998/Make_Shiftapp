using Shiftapp_demo.Business;
using Shiftapp_demo.Csv;
using Shiftapp_demo.DataAccess;
using Shiftapp_demo.FrameWork;
using Shiftapp_demo.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
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

        public AdminViewModel()
        {
            _dataLoader = new ShiftDataLoader();

            db = new DatabaseHelper();

            _business = new ShiftBusiness(db);

            //OpenAdminCommand = new RelayCommand(OpenAdmin);
        }


        private void OpenAdmin(object? _)
        {
            var admin = new AdminWindow();
            admin.ShowDialog();
        }
    }
}
