using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace Shiftapp_demo
{
    public partial class AdminWindow : Window
    {
        private DatabaseHelper _dbHelper;
        public ObservableCollection<Employee> Technicians { get; set; }
        public ObservableCollection<Employee> SearchResults { get; set; } // 検索結果用
        public ObservableCollection<Holiday> Holidays { get; set; }

        public AdminWindow()
        {
            InitializeComponent();

            try
            {
                _dbHelper = new DatabaseHelper();

                // 全技師データをロードして表示
                Technicians = _dbHelper.GetAllTechnicians();
                TechniciansDataGrid.ItemsSource = Technicians;

                // 検索結果の初期化
                SearchResults = new ObservableCollection<Employee>();
                SearchResultsDataGrid.ItemsSource = SearchResults;

                // 休日データのロード (仮)
                Holidays = new ObservableCollection<Holiday>
                {
                    new Holiday { Date = new DateTime(2025, 1, 1), Description = "元日" },
                    new Holiday { Date = new DateTime(2025, 5, 5), Description = "こどもの日" }
                };
                // HolidaysDataGridがXAMLに存在する場合、以下の行のコメントを解除
                // HolidaysDataGrid.ItemsSource = Holidays;
            }
            catch (Exception ex)
            {
                MessageBox.Show("初期化中にエラーが発生しました: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // this.DataContext = this; は、各DataGridに直接ItemsSourceを設定しているため、必ずしも必要ではありません。
            // データバインディングの仕組みを理解するまでは、混乱を避けるためにコメントアウトしても良いでしょう。
        }

        // 「検索」ボタンのクリックイベント
        private void SearchTechnicians_Click(object sender, RoutedEventArgs e)
        {
            string keyword = SearchKeywordTextBox.Text;
            SearchResults.Clear();

            var sourceList = Technicians;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                foreach (var tech in sourceList)
                {
                    SearchResults.Add(tech);
                }
            }
            else
            {
                // 検索ロジックを新しいプロパティに合わせて修正
                var results = sourceList.Where(t =>
                    t.Name.Contains(keyword) ||
                    (t.saturday_class != null && t.saturday_class.Contains(keyword)) ||
                    (t.catheterization != null && t.catheterization.Contains(keyword))
                );
                foreach (var tech in results)
                {
                    SearchResults.Add(tech);
                }
            }
        }

        // 「選択技師削除」ボタンのクリックイベント
        private void DeleteSelectedTechnician_Click(object sender, RoutedEventArgs e)
        {
            if (TechniciansDataGrid.SelectedItem is Employee selectedTechnician)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"{selectedTechnician.Name} を削除しますか？",
                    "確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _dbHelper.DeleteTechnician(selectedTechnician.Id);
                        Technicians.Remove(selectedTechnician); // ObservableCollectionからも削除
                        MessageBox.Show("技師が削除されました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("削除中にエラーが発生しました: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("削除する技師を選択してください。", "選択なし", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 「選択技師編集」ボタンのクリックイベント
        private void EditSelectedTechnician_Click(object sender, RoutedEventArgs e)
        {
            if (TechniciansDataGrid.SelectedItem is Employee selectedTechnician)
            {
                MessageBox.Show($"{selectedTechnician.Name} の編集機能を実装してください。", "TODO", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("編集する技師を選択してください。", "選択なし", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 不要になったAddTechnicianToDatabase_ClickとShowAddTechnicianForm_Clickメソッドは削除しました。
    }
}