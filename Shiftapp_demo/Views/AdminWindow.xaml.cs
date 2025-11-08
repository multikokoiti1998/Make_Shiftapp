using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Shiftapp_demo.ViewModels;


namespace Shiftapp_demo.Views
{
    public partial class AdminWindow : Window
    {
        private DatabaseHelper _dbHelper;
        public ObservableCollection<Employee> Employees { get; set; } = new();
        public ObservableCollection<Employee> SearchResults { get; set; } = new(); // 検索結果用
        public ObservableCollection<Holiday> Holidays { get; set; } = new();       // 祝日一覧（当月）

        public AdminWindow()
        {
            InitializeComponent();

            DataContext = new AdminViewModel();

            Loaded += AdminWindow_Loaded;
        }

        // ===== 起動時ロード =====
        private async void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _dbHelper = new DatabaseHelper();

                // --- 技師一覧 ---
                Employees = new ObservableCollection<Employee>(_dbHelper.GetAllEmployees());
                TechniciansDataGrid.ItemsSource = Employees;

                // --- 当月祝日 ---
                await LoadHolidaysThisMonthAsync();
                HolidaysDataGrid.ItemsSource = Holidays;
            }
            catch (Exception ex)
            {
                MessageBox.Show("初期化中にエラーが発生しました: " + ex.Message, "エラー",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====== 技師：追加/削除/保存 ======
        private void AddTechnician_Click(object sender, RoutedEventArgs e)
        {
            var newbie = new Employee
            {
                EmployeeId = 0,                 // 0=未採番（保存時にINSERT）
                EmployeeName = "新規技師",
                SaturdayClass = "B",
                CanDoCatheterization = false,
            };
            Employees.Add(newbie);
            TechniciansDataGrid.SelectedItem = newbie;
            TechniciansDataGrid.ScrollIntoView(newbie);
        }


        // ====== 祝日：当月ロード/追加/削除/保存 ======
        private DateTime MonthFirst(DateTime d) => new DateTime(d.Year, d.Month, 1);

        private async Task LoadHolidaysThisMonthAsync(DateTime? month = null)
        {
            try
            {
                var first = MonthFirst(month ?? DateTime.Today);
                var next = first.AddMonths(1);
                Holidays.Clear();

                // 同期版
                var list = _dbHelper.GetHolidays(first, next);
                foreach (var h in list) Holidays.Add(h);

                // 非同期版があるなら：
                // var list = await _dbHelper.GetHolidaysAsync(first, next);
                // foreach (var h in list) Holidays.Add(h);
            }
            catch (Exception ex)
            {
                MessageBox.Show("祝日読込でエラー: " + ex.Message, "エラー",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

  

        private void AddHoliday_Click(object sender, RoutedEventArgs e)
        {
            // XAML側の DatePicker/TextBox 名前に合わせてください（例：HolidayDatePicker / HolidayNameTextBox）
            if (FindName("HolidayDatePicker") is not DatePicker datePicker ||
                FindName("HolidayNameTextBox") is not TextBox nameBox)
            {
                MessageBox.Show("HolidayDatePicker / HolidayNameTextBox が見つかりません。XAML 名称をご確認ください。");
                return;
            }

            var d = datePicker.SelectedDate?.Date;
            if (d == null)
            {
                MessageBox.Show("日付を選択してください。", "入力不足",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var name = (nameBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) name = "休日";

            if (Holidays.Any(x => x.date == d.Value))
            {
                MessageBox.Show("同じ日付の休日が既にあります。", "重複",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Holidays.Add(new Holiday { date = d.Value });
            nameBox.Text = "";
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

    }
}
