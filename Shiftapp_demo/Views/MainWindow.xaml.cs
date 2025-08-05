// MainWindow.xaml.cs
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
            // Calendarの初期選択範囲を設定
            ShiftCalendar.SelectedDates.Clear();
            DateTime today = DateTime.Today;
            DateTime firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            for (DateTime date = firstDayOfMonth; date <= lastDayOfMonth; date = date.AddDays(1))
            {
                ShiftCalendar.SelectedDates.Add(date);
            }

            // ViewModelとの同期
            ViewModel.SelectedDates = ShiftCalendar.SelectedDates;

            ShiftDataGrid.Columns.Clear();
            foreach (var column in ViewModel.ShiftGridColumns)
            {
                ShiftDataGrid.Columns.Add(column);
            }

            // ★ DataGrid.Columns を ViewModel から追加（xamlには Columns={} は不要！
            var columns = GridHelperClass.GenerateColumnsForMonth(firstDayOfMonth);
            ViewModel.ShiftGridColumns = columns;
            ShiftDataGrid.Columns.Clear();
            foreach (var col in ViewModel.ShiftGridColumns)
            {
                ShiftDataGrid.Columns.Add(col);
            }

        }

        private void ShiftCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            // Calendar.SelectedDates の変更をViewModelに通知
            // ObservableCollection<DateTime> SelectedDates プロパティを持つViewModelと同期
            ViewModel.SelectedDates = ShiftCalendar.SelectedDates;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // 勤務表修正ボタンのクリックイベント (ViewModelにコマンドとして公開することも検討)
        }

        private void Button_Click1(object sender, RoutedEventArgs e)
        {
            // 管理者画面ボタンのクリックイベント (ViewModelにコマンドとして公開することも検討)
        }

        private void ShiftCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            // 例：表示された月の全日付を自動で選択する処理
            ShiftCalendar.SelectedDates.Clear();

            DateTime firstDay = new DateTime(ShiftCalendar.DisplayDate.Year, ShiftCalendar.DisplayDate.Month, 1);
            DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);

            for (DateTime date = firstDay; date <= lastDay; date = date.AddDays(1))
            {
                ShiftCalendar.SelectedDates.Add(date);
            }

            // ViewModelと同期させる（DataGrid等を更新する）
            ViewModel.SelectedDates = ShiftCalendar.SelectedDates;
        }

    }
}