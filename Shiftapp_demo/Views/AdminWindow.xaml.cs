using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;
using System;

namespace Shiftapp_demo
{
    public partial class AdminWindow : Window
    {
        private DatabaseHelper _dbHelper;
        public ObservableCollection<Employee> Employees { get; set; }
        public ObservableCollection<Employee> SearchResults { get; set; } // 検索結果用
        public ObservableCollection<Holiday> Holidays { get; set; }

        public AdminWindow()
        {
            InitializeComponent();

            Loaded += AdminWindow_Loaded;
        }
            private async void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _dbHelper = new DatabaseHelper();

                // 全従業員データをロードして表示
                Employees = await _dbHelper.GetAllEmployeesAsync();
                TechniciansDataGrid.ItemsSource = Employees;

                // 検索結果の初期化
                SearchResults = new ObservableCollection<Employee>();
                SearchResultsDataGrid.ItemsSource = SearchResults;

                // 休日データのロード (仮)
                Holidays = new ObservableCollection<Holiday>
        {
            new Holiday { Date = new DateTime(2025, 1, 1), Description = "元日" },
            new Holiday { Date = new DateTime(2025, 5, 5), Description = "こどもの日" }
        };
                // HolidaysDataGrid.ItemsSource = Holidays; // 必要に応じて
            }
            catch (Exception ex)
            {
                MessageBox.Show("初期化中にエラーが発生しました: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        

        private void SearchTechnicians_Click(object sender, RoutedEventArgs e)
        {
            string keyword = SearchKeywordTextBox.Text;
            SearchResults.Clear();

            var sourceList = Employees;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                foreach (var emp in sourceList)
                {
                    SearchResults.Add(emp);
                }
            }
            else
            {
                var results = sourceList.Where(e =>
                    (!string.IsNullOrEmpty(e.EmployeeName) && e.EmployeeName.Contains(keyword)) ||
                    (e.SaturdayClass?.ToString().Contains(keyword) == true) ||
                    (e.CanDoCatheterization.ToString().Contains(keyword))
                );
                foreach (var emp in results)
                {
                    SearchResults.Add(emp);
                }
            }
        }

        private void DeleteSelectedTechnician_Click(object sender, RoutedEventArgs e)
        {
            if (TechniciansDataGrid.SelectedItem is Employee selectedEmployee)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"{selectedEmployee.EmployeeName} を削除しますか？",
                    "確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _dbHelper.DeleteEmployee(selectedEmployee.EmployeeId);
                        Employees.Remove(selectedEmployee);
                        MessageBox.Show("従業員が削除されました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("削除中にエラーが発生しました: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("削除する従業員を選択してください。", "選択なし", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void EditSelectedTechnician_Click(object sender, RoutedEventArgs e)
        {
            if (TechniciansDataGrid.SelectedItem is Employee selectedEmployee)
            {
                MessageBox.Show($"{selectedEmployee.EmployeeName} の編集機能を実装してください。", "TODO", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("編集する従業員を選択してください。", "選択なし", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
