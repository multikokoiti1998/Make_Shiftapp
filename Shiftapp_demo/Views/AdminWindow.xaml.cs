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


        private void AddHoliday_Click(object sender, RoutedEventArgs e)
        {
            // XAML に x:Name="HolidayDatePicker" / "HolidayNameTextBox" を付けておく前提
            if (HolidayDatePicker.SelectedDate is not DateTime sel)
            {
                MessageBox.Show("日付を選択してください。", "入力不足",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var d = sel.Date;
            var name = (HolidayNameTextBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) name = "休日";

            if (Holidays.Any(x => x.date.Date == d))
            {
                MessageBox.Show("同じ日付の休日が既にあります。", "重複",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Holidays.Add(new Holiday { date = d, name = name });
            HolidayNameTextBox.Clear();
        }

        private void DeleteSelectedHoliday_Click(object sender, RoutedEventArgs e)
        {
            if (HolidaysDataGrid.SelectedItem is Holiday sel)
            {
                Holidays.Remove(sel);
            }
            else
            {
                MessageBox.Show("削除する休日を選択してください。", "選択なし",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AdminCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            if (!IsLoaded) return; // ← ロード前に走る初期イベントを無視

            if (_vm == null) return;

            //未タッチ時、西暦１年になる対策
            DateTime today = DateTime.Today;
            AdminCalendar.DisplayDate = today;
            AdminCalendar.SelectedDate = today;

            DateTime currentYear = AdminCalendar.DisplayDate;
             
        }

    }
}
