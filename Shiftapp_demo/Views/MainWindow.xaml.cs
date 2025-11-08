// MainWindow.xaml.cs
using Shiftapp_demo.ViewModels;
using System.Windows;
using System.Windows.Controls;

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


            ViewModel.LoadShiftDataForMonth(today);

            // 列の更新
            ShiftDataGrid.Columns.Clear();

            foreach (var col in ViewModel.ShiftGridColumns)
            {
                ShiftDataGrid.Columns.Add(col);
            }

        }

        private void ShiftCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            if (!IsLoaded) return; // ← ロード前に走る初期イベントを無視

            if (ViewModel == null) return;

            //未タッチ時、西暦１年になる対策
            DateTime today = DateTime.Today;
            ShiftCalendar.DisplayDate = today;
            ShiftCalendar.SelectedDate = today;


            DateTime currentMonth = ShiftCalendar.DisplayDate;

            ViewModel.LoadShiftDataForMonth(currentMonth);

            // DataGrid更新
            ShiftDataGrid.Columns.Clear();
            foreach (var col in ViewModel.ShiftGridColumns)
            {
                ShiftDataGrid.Columns.Add(col);
            }
        }

    }
}