using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Shiftapp_demo
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
            Loaded += AdminWindow_Loaded;
        }

        // ===== 起動時ロード =====
        private async void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _dbHelper = new DatabaseHelper();

                // --- 技師一覧 ---
                // 非同期版がある想定：
                // Employees = await _dbHelper.GetAllEmployeesAsync();
                // 同期版しかなければ：
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

        // ====== 技師：検索 ======
        private void SearchTechnicians_Click(object sender, RoutedEventArgs e)
        {
            string keyword = (SearchKeywordTextBox.Text ?? "").Trim();
            SearchResults.Clear();

            var sourceList = Employees ?? new ObservableCollection<Employee>();
            if (string.IsNullOrEmpty(keyword))
            {
                foreach (var emp in sourceList) SearchResults.Add(emp);
                return;
            }

            var results = sourceList.Where(emp =>
                (!string.IsNullOrEmpty(emp.EmployeeName) &&
                    emp.EmployeeName.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(emp.SaturdayClass) &&
                    emp.SaturdayClass.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0) ||
                (emp.CanDoCatheterization.ToString().IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0)
            );

            foreach (var emp in results) SearchResults.Add(emp);
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchKeywordTextBox.Text = "";
            SearchResults.Clear();
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

        private void DeleteSelectedTechnician_Click(object sender, RoutedEventArgs e)
        {
            if (TechniciansDataGrid.SelectedItem is Employee selectedEmployee)
            {
                var result = MessageBox.Show(
                    $"{selectedEmployee.EmployeeName} を削除しますか？",
                    "確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 同期版
                        _dbHelper.DeleteEmployee(selectedEmployee.EmployeeId);

                        // 非同期版があるなら：
                        // await _dbHelper.DeleteEmployeeAsync(selectedEmployee.EmployeeId);

                        Employees.Remove(selectedEmployee);
                        MessageBox.Show("従業員が削除されました。", "成功",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("削除中にエラーが発生しました: " + ex.Message, "エラー",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("削除する従業員を選択してください。", "選択なし",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void SaveTechnicians_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 編集コミット（未確定のセル値を反映）
                TechniciansDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                TechniciansDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                // 重複/必須チェックなど簡易バリデーション
                var dup = Employees.Where(x => !string.IsNullOrWhiteSpace(x.EmployeeName))
                                   .GroupBy(x => x.EmployeeName.Trim())
                                   .FirstOrDefault(g => g.Count() > 1);
                if (dup != null)
                {
                    MessageBox.Show($"同一氏名が重複しています: {dup.Key}", "入力エラー",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Upsert
                foreach (var emp in Employees)
                {
                    if (string.IsNullOrWhiteSpace(emp.EmployeeName)) continue;

                    //// 非同期版がある場合
                    //if (_dbHelper.UpsertEmployeeAsync != null)
                    //{
                    //    await _dbHelper.UpsertEmployeeAsync(emp);
                    //}
                    //else
                    //{
                    //    // 同期版のみの場合
                    //    _dbHelper.UpsertEmployee(emp);
                    //}
                }

                // 再読込（採番反映など）
                // Employees = await _dbHelper.GetAllEmployeesAsync();
                Employees = new ObservableCollection<Employee>(_dbHelper.GetAllEmployees());
                TechniciansDataGrid.ItemsSource = Employees;

                MessageBox.Show("技師情報を保存しました。", "成功",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存中にエラーが発生しました: " + ex.Message, "エラー",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                // foreach (var h in list) Holidays.Add(h);

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

        private async void ReloadHolidaysThisMonth_Click(object sender, RoutedEventArgs e)
        {
            await LoadHolidaysThisMonthAsync();
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

        private async void SaveHolidays_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 編集コミット
                HolidaysDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                HolidaysDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                var first = MonthFirst(DateTime.Today);
                var next = first.AddMonths(1);

                // 「当月分を置換」パターン
                // 同期版
                //_dbHelper.ReplaceHolidaysForRange(first, next, Holidays.Select(h => (h.Date, h.Name)));

                // 非同期版があるなら：
                // await _dbHelper.ReplaceHolidaysForRangeAsync(first, next, Holidays.Select(h => (h.Date, h.Name)));

                MessageBox.Show("祝日を保存しました。", "成功",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadHolidaysThisMonthAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("祝日保存でエラー: " + ex.Message, "エラー",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== 既存の編集ボタン（フォーム起動のフック） =====
        private void EditSelectedTechnician_Click(object sender, RoutedEventArgs e)
        {
            if (TechniciansDataGrid.SelectedItem is Employee selectedEmployee)
            {
                // TODO: ここで編集用ダイアログを開くなど
                MessageBox.Show($"{selectedEmployee.EmployeeName} の編集ダイアログは未実装です。", "TODO",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("編集する従業員を選択してください。", "選択なし",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
