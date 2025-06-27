using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Shiftapp_demo
{
    /// <summary>
    /// AdminWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class AdminWindow : Window
    {
        private DatabaseHelper _dbHelper;
        public ObservableCollection<Technician> Technicians { get; set; }
        public ObservableCollection<Technician> SearchResults { get; set; } // 検索結果用
        public ObservableCollection<Holiday> Holidays { get; set; }
        public AdminWindow()
        {
            InitializeComponent();

            _dbHelper = new DatabaseHelper(); // DatabaseHelperのインスタンス化

            // 全技師データをロードして表示
            Technicians = _dbHelper.GetAllTechnicians();
            TechniciansDataGrid.ItemsSource = Technicians;

            // 検索結果の初期化
            SearchResults = new ObservableCollection<Technician>();
            SearchResultsDataGrid.ItemsSource = SearchResults;

            // 休日データのロード (仮)
            Holidays = new ObservableCollection<Holiday>
            {
                new Holiday { Date = new System.DateTime(2025, 1, 1), Description = "元日" },
                new Holiday { Date = new System.DateTime(2025, 5, 5), Description = "こどもの日" }
            };
            HolidaysDataGrid.ItemsSource = Holidays;

            this.DataContext = this;
        }

        // 「この技師をデータベースに追加」ボタンのクリックイベント
        private void AddTechnicianToDatabase_Click(object sender, RoutedEventArgs e)
        {
            string name = NewTechnicianNameTextBox.Text;
            string contact = NewTechnicianContactTextBox.Text;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("氏名は必須です。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Technician newTech = new Technician { Name = name, Contact = contact };
            _dbHelper.AddTechnician(newTech); // データベースに追加

            // DataGridを更新するために、改めて全技師をロードし直す
            Technicians.Clear(); // 既存データをクリア
            foreach (var tech in _dbHelper.GetAllTechnicians()) // 最新データを取得
            {
                Technicians.Add(tech);
            }

            // 入力フォームをクリア
            NewTechnicianNameTextBox.Clear();
            NewTechnicianContactTextBox.Clear();

            MessageBox.Show("技師が追加されました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 「検索」ボタンのクリックイベント
        private void SearchTechnicians_Click(object sender, RoutedEventArgs e)
        {
            string keyword = SearchKeywordTextBox.Text;
            SearchResults.Clear(); // 検索結果をクリア

            if (string.IsNullOrWhiteSpace(keyword))
            {
                // キーワードが空の場合は、全技師を表示しても良いし、何もしなくても良い
                foreach (var tech in _dbHelper.GetAllTechnicians())
                {
                    SearchResults.Add(tech);
                }
            }
            else
            {
                // 検索を実行
                foreach (var tech in _dbHelper.SearchTechnicians(keyword))
                {
                    SearchResults.Add(tech);
                }
            }
        }

        // 「選択技師削除」ボタンのクリックイベント (例)
        private void DeleteSelectedTechnician_Click(object sender, RoutedEventArgs e)
        {
            if (TechniciansDataGrid.SelectedItem is Technician selectedTechnician)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"{selectedTechnician.Name} を削除しますか？",
                    "確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _dbHelper.DeleteTechnician(selectedTechnician.Id);
                    Technicians.Remove(selectedTechnician); // ObservableCollectionからも削除
                    MessageBox.Show("技師が削除されました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("削除する技師を選択してください。", "選択なし", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 「選択技師編集」ボタンのクリックイベント (別のウィンドウ/ダイアログを開いて編集するパターンが多い)
        private void EditSelectedTechnician_Click(object sender, RoutedEventArgs e)
        {
            if (TechniciansDataGrid.SelectedItem is Technician selectedTechnician)
            {
                // 編集用のダイアログを開くか、既存の追加フォームを編集モードにするなど
                // 例: 新しい編集ウィンドウを開く
                // EditTechnicianWindow editWin = new EditTechnicianWindow(selectedTechnician);
                // if (editWin.ShowDialog() == true) { /* 更新後の処理 */ }
                MessageBox.Show($"{selectedTechnician.Name} の編集機能を実装してください。", "TODO", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("編集する技師を選択してください。", "選択なし", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 新規技師追加フォームの表示/非表示を切り替えるボタン（必要であれば）
        private void ShowAddTechnicianForm_Click(object sender, RoutedEventArgs e)
        {
            // 例えば、追加フォームのVisibilityをHidden/Visibleに切り替えるなど
            // 現在の例では常に表示されていますが、スペース節約のために切り替えることも可能
        }

        // TechnicianとHolidayクラスはAdminWindow.xaml.csの外部（例: Modelsフォルダ内の別ファイル）に定義するとより良いです
        // 例としてここに含めていますが、プロジェクト構造を整理する際は移動を検討してください。
    }
    // ここにTechnicianクラスとHolidayクラスの定義を置くか、別のファイルから参照する
    // public class Technician ...
    // public class Holiday ...
}

