// MainViewModel.cs
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Shiftapp_demo.Business; // DataGridColumn, TextBlock を参照するため
using Shiftapp_demo.Csv;
using Shiftapp_demo.DataAccess; // DatabaseHelperを参照するため
using Shiftapp_demo.FrameWork;
using Shiftapp_demo.Helper;
using Shiftapp_demo.Models;
using Shiftapp_demo.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

// モデルとデータアクセス層のNamespaceを追加

namespace Shiftapp_demo.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private ShiftDataLoader _dataLoader;

        private readonly DatabaseHelper db;

        private readonly ShiftBusiness _business;

        // DataGridのItemsSourceにバインド グリッドの行データを保持
        private ObservableCollection<ShiftDataLoader> _shiftDataCollection;

        private readonly CsvBusiness _csvBiz;

        private readonly IShiftCsvExporter _exporter;

        public ICommand OpenAdminCommand { get; }

        public ICommand ExportCsvRowsCommand { get; }

        public ICommand GenerateShiftCommand { get; }

        public ObservableCollection<ShiftTypeM> ShiftTypes { get; } = new(); // プルダウン用マスタ

        private readonly Dictionary<string, int> _symbolToId = new();

        private readonly HashSet<int> _parentTypeIds = new();    
        
        private readonly HashSet<string> _parentSymbols = new(new[] { "当", "●", "日" });

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

        private DateTime _displayDate = DateTime.Today;
        public DateTime DisplayDate
        {
            get => _displayDate;
            set
            {
                if (_displayDate != value)
                {
                    _displayDate = value;
                    OnPropertyChanged(nameof(_displayDate));
                }
            }
        }


        public MainViewModel()
        {
            _dataLoader = new ShiftDataLoader();

            DisplayDate = DateTime.Today;

            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>();

            ShiftGridColumns = new ObservableCollection<DataGridColumn>();

            db = new DatabaseHelper();

            _business = new ShiftBusiness(db);

            _exporter = new CsvHelperExporter(); 
            
            _csvBiz = new CsvBusiness(db, _exporter);

            ExportCsvRowsCommand = new RelayCommand(async p => await ExportCsvRowsAsync(p));

            OpenAdminCommand = new RelayCommand(OpenAdmin);

            GenerateShiftCommand = new RelayCommand(GenerateShift);
        }

        private void OpenAdmin(object? _)
        {
            var admin = new AdminWindow();
            admin.ShowDialog();
        }

        private void GenerateShift(object? _)
        {

            MakeNightDuty(DisplayDate);

            GenerateOffShift(DisplayDate);

            LoadShiftDataForMonth(DisplayDate);

        }


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
            var employees = db.GetAllEmployees(); 

            // 2) 期間内の実シフトだけ取得（無い日は返らない）
            var shifts = db.GetShiftsOnly(firstDay, lastDay); 

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

                // その月の全日付キーを空で用意
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

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}