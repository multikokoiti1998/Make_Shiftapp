// MainViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input; // ICommand用
using System.Windows.Controls; // DataGridColumn, TextBlock を参照するため

// モデルとデータアクセス層のNamespaceを追加
using Shiftapp_demo.Models;
using Shiftapp_demo.DataAccess;

namespace Shiftapp_demo.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private ShiftDataLoader _dataLoader; // Model層 (データアクセス)
        
        // DataGridのItemsSourceにバインド
        private ObservableCollection<ShiftData> _shiftDataCollection;
        public ObservableCollection<ShiftData> ShiftDataCollection
        {
            get { return _shiftDataCollection; }
            set
            {
                _shiftDataCollection = value;
                OnPropertyChanged(nameof(ShiftDataCollection));
            }
        }

        // DataGridの列コレクションにバインド (UI固有の要素を含むため注意が必要)
        // 通常はViewModelにUI要素を含めないが、DataGridの動的列生成は特殊
        // もしくは、コードビハインドで列生成のみ行う
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

        // コマンド (今回は使わないが、ボタンクリックなどに利用)
        public ICommand LoadShiftCommand { get; private set; }

        public MainViewModel()
        {
            // DBパスはここでViewModelに渡すか、設定ファイルから読み込む
            _dataLoader = new ShiftDataLoader("C:\\path\\to\\your\\shift_db.sqlite"); 
            ShiftDataCollection = new ObservableCollection<ShiftData>();
            ShiftGridColumns = new ObservableCollection<DataGridColumn>(); // 列コレクションの初期化
            
            // カレンダーの初期選択範囲はView側で設定し、ViewModelのSelectedDatesにバインドする
            // その後、SelectedDates_CollectionChangedイベントでUpdateHeaderAndLoadDataが呼ばれる

            // LoadShiftCommand = new RelayCommand(param => LoadShiftData());
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

            // DataGridの列を生成
            // ViewModelからUI要素（DataGridColumn, TextBlock, DataTemplate, FrameworkElementFactory）を直接生成するのは、
            // MVVMの原則に厳密には反するが、DataGridの動的列生成はこのパターンがよく使われる。
            // もしくは、列生成はコードビハインドに残し、データコレクションのみViewModelから取得する。
            // ここではViewModelで全て生成する例を示す。
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

            // データをロード
            var loadedData = _dataLoader.LoadShiftData(currentStartDate, currentEndDate);
            ShiftDataCollection.Clear();
            foreach (var item in loadedData)
            {
                ShiftDataCollection.Add(item);
            }
        }

        // INotifyPropertyChanged の実装
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 必要に応じてRelayCommandなどのICommand実装クラスを定義 (例: MVVM Light Toolkitなどを使用しない場合)
    // public class RelayCommand : ICommand { /* ... */ }
}