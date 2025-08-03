// MainWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Linq; // For Min/Max on SelectedDates

using Shiftapp_demo.ViewModels;

namespace Shiftapp_demo.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext; // ViewModelにアクセスするヘルパープロパティ

        public MainWindow()
        {
            InitializeComponent();
            // DataContextはXAMLで設定済み

            // Calendarの初期選択範囲を設定
            ShiftCalendar.SelectedDates.Clear();
            DateTime today = DateTime.Today;
            DateTime firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            for (DateTime date = firstDayOfMonth; date <= lastDayOfMonth; date = date.AddDays(1))
            {
                ShiftCalendar.SelectedDates.Add(date);
            }

            // ViewModelのSelectedDatesをCalendarのSelectedDatesと同期させる
            // Calendar.SelectedDatesChanged イベントハンドラでこれを実行
            // ViewModel.SelectedDates = ShiftCalendar.SelectedDates; // Read-Only なので直接代入できない

            // 初期ロードをトリガー
            // ViewModelがCalendarのSelectedDatesChangedイベントを直接受け取れないため、
            // コードビハインドからViewModelの更新メソッドを呼び出す。
            // もしくは、ViewModelでCalendarのSelectedDatesを監視するBehaviorを実装する。
            // ここではシンプルにコードビハインドから。
            ViewModel.SelectedDates = ShiftCalendar.SelectedDates; // コレクション自体を渡す
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
    }
}