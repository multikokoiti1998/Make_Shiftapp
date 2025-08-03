// MainViewModel.cs
using Shiftapp_demo.Business; // DataGridColumn, TextBlock を参照するため
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input; // ICommand用
using Shiftapp_demo.DataAccess; // DatabaseHelperを参照するため


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

        // カレンダーの選択範囲にバインド
        // ObservableCollection<DateTime> もしくは、Min/Maxをプロパティとして持つ
        private ObservableCollection<DateTime> _selectedDates;
        public ObservableCollection<DateTime> SelectedDates
        {
            get { return _selectedDates; }
            set
            {
                if (_selectedDates != null) _selectedDates.CollectionChanged -= SelectedDates_CollectionChanged;
                _selectedDates = value;
                if (_selectedDates != null) _selectedDates.CollectionChanged += SelectedDates_CollectionChanged;
                OnPropertyChanged(nameof(SelectedDates));
                UpdateHeaderAndLoadData(); // 日付変更時にデータロード
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

        // SelectedDatesコレクションの変更を監視するためのイベントハンドラ
        private void SelectedDates_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateHeaderAndLoadData();
        }

        // データをロードし、ヘッダーと列を更新するメインメソッド
        private void UpdateHeaderAndLoadData()
        {
            if (SelectedDates == null || SelectedDates.Count == 0)
            {
                ShiftDataCollection.Clear();
                ShiftGridColumns.Clear();
                ShiftGridColumns.Add(new DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding("EmployeeId"), Width = DataGridLength.Auto });
                ShiftGridColumns.Add(new DataGridTextColumn { Header = "従業員名", Binding = new System.Windows.Data.Binding("EmployeeName"), Width = DataGridLength.Auto });
                MonthHeaderText = "日付を選択してください";
                return;
            }

            DateTime currentStartDate = SelectedDates.Min().Date;
            DateTime currentEndDate = SelectedDates.Max().Date;

            // 月ヘッダーのテキストを設定
            if (currentStartDate.Month == currentEndDate.Month && currentStartDate.Year == currentEndDate.Year)
            {
                MonthHeaderText = $"{currentStartDate.Year}年 {currentStartDate.Month}月";
            }
            else
            {
                MonthHeaderText = $"{currentStartDate.Year}年 {currentStartDate.Month}月 - {currentEndDate.Month}月";
            }

            // 1. シフトデータを取得（DatabaseHelper を内部に持つようにしておく）
            var db = new DatabaseHelper();
            var shifts = db.GetShifts(currentStartDate, currentEndDate);

            // 2. 従業員ごとの辞書にまとめる
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
                        loader.Shifts[shift.ShiftDate] = shift.Symbol;
                    }

                    return loader;
                }).ToList();

            // 3. ViewModelに反映
            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>(grouped);

            // DataGridの列を生成

            var columns = new ObservableCollection<DataGridColumn>();
            columns.Add(new DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding("EmployeeId"), Width = DataGridLength.Auto });
            columns.Add(new DataGridTextColumn { Header = "従業員名", Binding = new System.Windows.Data.Binding("EmployeeName"), Width = DataGridLength.Auto });

            for (DateTime date = currentStartDate; date <= currentEndDate; date = date.AddDays(1))
            {
                DataGridTemplateColumn dateColumn = new DataGridTemplateColumn();
                dateColumn.Header = date.Day.ToString();
                
                // BindingOperations.SetBinding を使用してBindingを設定（UIスレッドで実行する必要がある場合など）
                // FrameworkElementFactory は XAML でのインスタンス化を模倣している
                System.Windows.FrameworkElementFactory textBlockFactory = new System.Windows.FrameworkElementFactory(typeof(TextBlock));
                textBlockFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding($"[{date.Date}]")); 

                textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
                textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);

                System.Windows.DataTemplate cellTemplate = new System.Windows.DataTemplate();
                cellTemplate.VisualTree = textBlockFactory;
                dateColumn.CellTemplate = cellTemplate;
                dateColumn.Width = new DataGridLength(30);

                columns.Add(dateColumn);
            }
            ShiftGridColumns = columns; // 生成した列コレクションをViewModelのプロパティに設定

      
        }

        // INotifyPropertyChanged の実装
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}