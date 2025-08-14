// MainViewModel.cs
using Shiftapp_demo.Business; // DataGridColumn, TextBlock を参照するため
using Shiftapp_demo.DataAccess; // DatabaseHelperを参照するため
using Shiftapp_demo.Helper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input; // ICommand用


// モデルとデータアクセス層のNamespaceを追加

namespace Shiftapp_demo.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private ShiftDataLoader _dataLoader; // Model層 (データアクセス)
        
        // DataGridのItemsSourceにバインド グリッドの行データを保持
        private ObservableCollection<ShiftDataLoader> _shiftDataCollection;
        public ObservableCollection<ShiftDataLoader> ShiftDataCollection
        {
            get { return _shiftDataCollection; }
            set
            {
                _shiftDataCollection = value;
                OnPropertyChanged(nameof(ShiftDataCollection));
            }
        }

        private ObservableCollection<DataGridColumn> _shiftGridColumns;
        public ObservableCollection<DataGridColumn> ShiftGridColumns
        {
            get { return _shiftGridColumns; }
            set
            {
                _shiftGridColumns = value;
                OnPropertyChanged(nameof(ShiftGridColumns));
            }
        }



        // 月ヘッダーのテキストにバインド
        private string _monthHeaderText;
        public string MonthHeaderText
        {
            get { return _monthHeaderText; }
            set
            {
                _monthHeaderText = value;

                OnPropertyChanged(nameof(MonthHeaderText));
            }
        }

        public MainViewModel()
        {
            // DBパスはここでViewModelに渡すか、設定ファイルから読み込む
            _dataLoader = new ShiftDataLoader(); 

            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>();

            ShiftGridColumns = new ObservableCollection<DataGridColumn>(); // 列コレクションの初期化
        }

        public void LoadShiftDataForMonth(DateTime month)
        {
            DateTime firstDay = new DateTime(month.Year, month.Month, 1);
            DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);

            // ヘッダー表示
            MonthHeaderText = $"{month.Year}年 {month.Month}月";

            // DB取得
            var db = new DatabaseHelper();
            var shifts = db.GetShifts(firstDay, lastDay);

            //グループ化
           var grouped = shifts
               .GroupBy(s => new { s.EmployeeId, s.EmployeeName })
               .Select(g =>
               {
                   var loader = new ShiftDataLoader
                   {
                       EmployeeId = g.Key.EmployeeId,
                       EmployeeName = g.Key.EmployeeName
                   };

                   foreach (var shift in g)
                   {
                       loader.Shifts[shift.ShiftDate.ToString("yyyy-MM-dd")] = shift.Symbol;
                   }
                   return loader;
               }).ToList();

            //一人ひとりのシフトデータをShiftDataCollectionに設定
            //DataGrid.Columnsにaddしてセットする
            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>(grouped);

            //UIにバインディングされている、月の列を作成
            ShiftGridColumns = GridHelperClass.GenerateColumnsForMonth(month);// 月の列を生成

        }


        // INotifyPropertyChanged の実装
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}