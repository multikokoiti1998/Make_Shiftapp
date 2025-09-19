// MainWindow.xaml.cs
using Microsoft.Win32;
using Shiftapp_demo.Csv;
using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Helper;
using Shiftapp_demo.ViewModels;
using System.Linq; // For Min/Max on SelectedDates
using System.Windows;
using System.Windows.Controls;
using static MaterialDesignThemes.Wpf.Theme;

namespace Shiftapp_demo.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext; // ViewModelにアクセスするヘルパープロパティ

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainViewModel();

            this.Loaded += MainWindow_Loaded;

        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DateTime today = DateTime.Today;

            //ViewModel.GenerateOffShift(today);

            ViewModel.LoadShiftDataForMonth(today);

            // 列の更新
            ShiftDataGrid.Columns.Clear();

            foreach (var col in ViewModel.ShiftGridColumns)
            {
                ShiftDataGrid.Columns.Add(col);
            }

            // カレンダーの表示月を今日に（初期表示用）
            ShiftCalendar.DisplayDate = today;
        }

        private void ShiftCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            DateTime currentMonth = ShiftCalendar.DisplayDate;

            //ViewModel.GenerateOffShift(currentMonth);

            ViewModel.LoadShiftDataForMonth(currentMonth);

            // DataGrid更新
            ShiftDataGrid.Columns.Clear();
            foreach (var col in ViewModel.ShiftGridColumns)
            {
                ShiftDataGrid.Columns.Add(col);
            }
        }


        private void Button_Click1(object sender, RoutedEventArgs e)
        {
            // 管理者画面ボタンのクリックイベント (ViewModelにコマンドとして公開することも検討)

        }



        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DateTime today = ShiftCalendar.DisplayDate;

            ShiftCalendar.DisplayDate = today;

            ViewModel.MakeNightDuty(today);

            ViewModel.GenerateOffShift(today);

            ViewModel.LoadShiftDataForMonth(today);

        }
    }
}