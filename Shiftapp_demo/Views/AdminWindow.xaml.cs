using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Shiftapp_demo.ViewModels;
using System.Linq;                 // ← これ忘れがち
using System.Threading.Tasks;
using System;


namespace Shiftapp_demo.Views
{
    public partial class AdminWindow : Window
    {
        private readonly AdminViewModel _vm;
        public ObservableCollection<Employee> Employees { get; set; } = new();
        public ObservableCollection<Holiday> Holidays { get; set; } = new();       // 祝日一覧（当月）
        public AdminWindow()
        {
            InitializeComponent();

            _vm = new AdminViewModel();

            DataContext = _vm;

            Loaded += async (_, __) =>
            {
                await _vm.LoadAsync();

                TechniciansDataGrid.AutoGenerateColumns = false;
                TechniciansDataGrid.Columns.Clear();

                foreach (var c in _vm.TechniciansDataGridColumns)
                    TechniciansDataGrid.Columns.Add(c);
            };

        }

     
    }
}
