// MainWindow.xaml.cs
using Shiftapp_demo.Business;
using Shiftapp_demo.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Shiftapp_demo.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext; // ViewModelにアクセスするヘルパープロパティ

        public bool IsCalendarTouched { get; set; } = false;

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

        // GridHelperClass.GenerateColumnsForMonth が生成する先頭の非日付列（ID・名前）の数。
        // Columns内の並び順（DisplayIndexではない）はこの生成順から変わらないため、
        // 「列インデックス - この値」を月初日に加算すれば、その列が表す日付を復元できる。
        private const int LeadingNonDateColumnCount = 2;

        private void ShiftDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete) return;
            if (ShiftDataGrid.SelectedCells.Count == 0) return;

            var firstOfMonth = new DateTime(ViewModel.DisplayDate.Year, ViewModel.DisplayDate.Month, 1);

            foreach (var cellInfo in ShiftDataGrid.SelectedCells)
            {
                int colIndex = ShiftDataGrid.Columns.IndexOf(cellInfo.Column);
                if (colIndex < LeadingNonDateColumnCount) continue; // ID・名前列はスキップ

                if (cellInfo.Item is not ShiftDataLoader row) continue;

                var date = firstOfMonth.AddDays(colIndex - LeadingNonDateColumnCount);
                row[date.ToString("yyyy-MM-dd")] = "";
            }

            e.Handled = true;
        }

        private void ShiftCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            if (!IsLoaded) return; // ← ロード前に走る初期イベントを無視

            if (ViewModel == null) return;


            if (ShiftCalendar.DisplayDate== new DateTime(2020, 1, 1))
            {
                ShiftCalendar.DisplayDate = DateTime.Today;
            }

            // DataGrid更新
            ShiftDataGrid.Columns.Clear();
            foreach (var col in ViewModel.ShiftGridColumns)
            {
                ShiftDataGrid.Columns.Add(col);
            }
        }

    }
}