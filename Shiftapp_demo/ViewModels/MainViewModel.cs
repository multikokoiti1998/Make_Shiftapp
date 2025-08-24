// MainViewModel.cs
using Shiftapp_demo.Business; // DataGridColumn, TextBlock を参照するため
using Shiftapp_demo.DataAccess; // DatabaseHelperを参照するため
using Shiftapp_demo.Helper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;

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

        private readonly ShiftBusiness _business;

        public MainViewModel()
        {
            // DBパスはここでViewModelに渡すか、設定ファイルから読み込む
            _dataLoader = new ShiftDataLoader();

            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>();

            ShiftGridColumns = new ObservableCollection<DataGridColumn>();

            var db = new DatabaseHelper();

            _business = new ShiftBusiness(db);
        }

        public void GenerateOffShift(DateTime month)
        {
            // 例えば8/16を基準に、B班からスタート
            _business.UpdateSaturdayShifts(month);

            //日曜日や祭日のシフト作成
            _business.UpdateSundayShifts(month);
        }

        public void LoadShiftDataForMonth(DateTime month)
        {
            //日付取得
            var firstDay = new DateTime(month.Year, month.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);

            // ヘッダー
            MonthHeaderText = $"{month.Year}年 {month.Month}月";

            var db = new DatabaseHelper();

            // 1) 全社員（ID／名前）を取得
            var employees = db.GetAllEmployees(); // List<Employee> { EmployeeId, EmployeeName }

            // 2) 期間内の実シフトだけ取得（無い日は返らない）
            var shifts = db.GetShiftsOnly(firstDay, lastDay); // List<Shift> { EmployeeId, ShiftDate, Symbol }

            // 3) 社員ごとにまとめて、全日を空で初期化→存在するシフトだけ上書き
            var loaders = new List<ShiftDataLoader>(employees.Count);
            foreach (var e in employees.OrderBy(x => x.EmployeeId))
            {
                var loader = new ShiftDataLoader
                {
                    EmployeeId = e.EmployeeId,
                    EmployeeName = e.EmployeeName,
                    Role = e.Role
                };

                // 3-1) その月の全日付キーを空で用意
                for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
                {
                    loader.Shifts[d.ToString("yyyy-MM-dd")] = string.Empty;
                }

                // 3-2) 実シフトを上書き
                foreach (var s in shifts.Where(s => s.EmployeeId == e.EmployeeId))
                {
                    loader.Shifts[s.ShiftDate.ToString("yyyy-MM-dd")] = s.Symbol ?? string.Empty;
                }

                loaders.Add(loader);
            }

            var ordered = loaders
                   .OrderBy(x => x.Role)
                   .ThenBy(x => x.EmployeeId)
                   .ToList();

            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>(ordered);

            // 5) 列（ID/名前＋1..末日）
            ShiftGridColumns = GridHelperClass.GenerateColumnsForMonth(month);
        }


        // INotifyPropertyChanged の実装
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}