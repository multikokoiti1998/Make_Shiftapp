// MainViewModel.cs
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Shiftapp_demo.Business; // DataGridColumn, TextBlock を参照するため
using Shiftapp_demo.Csv;
using Shiftapp_demo.DataAccess; // DatabaseHelperを参照するため
using Shiftapp_demo.Helper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using Shiftapp_demo.Models;

// モデルとデータアクセス層のNamespaceを追加

namespace Shiftapp_demo.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private ShiftDataLoader _dataLoader; // Model層 (データアクセス)

        private readonly DatabaseHelper db;

        private readonly ShiftBusiness _business;

        // DataGridのItemsSourceにバインド グリッドの行データを保持
        private ObservableCollection<ShiftDataLoader> _shiftDataCollection;

        // ★ CSV出力用（行形式）
        private readonly CsvBusiness _csvBiz;

        private readonly IShiftCsvExporter _exporter;

        public ICommand OpenAdminCommand { get; }

        //private readonly DatabaseHelper db;
        public ObservableCollection<ShiftTypeM> ShiftTypes { get; } = new(); // プルダウン用マスタ

        private readonly Dictionary<string, int> _symbolToId = new();  // "当"→1 など
        private readonly HashSet<int> _parentTypeIds = new();         // 親（当/●/日）
        private readonly HashSet<string> _parentSymbols = new(new[] { "当", "●", "日" });

        // 差分検出用のスナップショット: (eid, date) → symbol
        private Dictionary<(int Eid, DateTime Date), string> _originalSymbolMap = new();

        //行
        public ObservableCollection<ShiftDataLoader> ShiftDataCollection
        {
            get { return _shiftDataCollection; }
            set
            {
                _shiftDataCollection = value;
                OnPropertyChanged(nameof(ShiftDataCollection));
            }
        }

        //列
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

            ShiftGridColumns = new ObservableCollection<DataGridColumn>();

            db = new DatabaseHelper();

            _business = new ShiftBusiness(db);

            // ★ CSV出力に必要
            _exporter = new CsvHelperExporter();                 // UTF-8 BOM / 区切り「,」
            _csvBiz = new CsvBusiness(db, _exporter);

            ExportCsvRowsCommand = new RelayCommand(async p => await ExportCsvRowsAsync(p));

            OpenAdminCommand = new RelayCommand(OpenAdmin);
        }

        private void OpenAdmin(object? _)
        {
            var admin = new AdminWindow();
            admin.ShowDialog();
        }


        public ICommand ExportCsvRowsCommand { get; }

        private async Task ExportCsvRowsAsync(object? param)
        {
            if (param is not DateTime displayDate)
                return;

            int year = displayDate.Year;
            int month = displayDate.Month;

            var sfd = new SaveFileDialog
            {
                Filter = "CSVファイル (*.csv)|*.csv",
                FileName = $"shifts_{year:0000}{month:00}_rows.csv"
            };

            if (sfd.ShowDialog() == true)
            {
                // 「その月に1件でも記録がある人」について、全日を補完して行形式で保存
                await _csvBiz.ExportMonthAsRowsAsync(year, month, sfd.FileName);
            }
        }

        private void LoadShiftTypes()
        {
            ShiftTypes.Clear();
            _symbolToId.Clear();
            _parentTypeIds.Clear();

            foreach (var m in db.GetShiftTypeMaster()) // (ShiftTypeId, Symbol, Name, IsParent) を返す想定
            {
                ShiftTypes.Add(new ShiftTypeM
                {
                    ShiftTypeId = m.ShiftTypeId,
                    Symbol = m.Symbol,
                    Name = m.Name,
                });

                _symbolToId[m.Symbol] = m.ShiftTypeId;

            }
        }

        public void GenerateOffShift(DateTime month)
        {
            // 例えば8/16を基準に、B班からスタート
            _business.UpdateSaturdayShifts(month);

            //日曜日や祭日のシフト作成
            _business.UpdateSundayShifts(month);
        }
        public void MakeNightDuty(DateTime month)
        {
            db.DeleteMonthDutyAndDayParentsWithCascade(month,1,0);

            _business.GenerateNightDutiesForMonth(month);

            LoadShiftDataForMonth(month);
        }

        public void LoadShiftDataForMonth(DateTime month)
        {
            var db = new DatabaseHelper();

            LoadShiftTypes();

            _business.CleanOrphanNightChildrenForMonth(month);

            _shiftGridColumns.Clear();
            //日付取得
            var firstDay = new DateTime(month.Year, month.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);

            // ヘッダー
            MonthHeaderText = $"{month.Year}年 {month.Month}月";

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


        // コマンドひな形
        public sealed class RelayCommand : ICommand
        {
            private readonly Func<object?, Task>? _execAsync;
            private readonly Action<object?>? _exec;
            private readonly Func<object?, bool>? _can;

            public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null)
            { _exec = exec; _can = can; }

            public RelayCommand(Func<object?, Task> execAsync, Func<object?, bool>? can = null)
            { _execAsync = execAsync; _can = can; }

            public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;

            public async void Execute(object? parameter)
            {
                if (_execAsync != null) await _execAsync(parameter);
                else _exec?.Invoke(parameter);
            }

            public event EventHandler? CanExecuteChanged
            { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        }
    }
}