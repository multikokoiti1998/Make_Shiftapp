// MainViewModel.cs
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Serilog;
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
    public class MainViewModel : ViewModelBase
    {
        private ShiftDataLoader _dataLoader;

        private readonly MainDatabaseHelper db;

        private readonly ShiftBusiness _business;


        // DataGridのItemsSourceにバインド グリッドの行データを保持
        private ObservableCollection<ShiftDataLoader> _shiftDataCollection;

        private readonly CsvBusiness _csvBiz;

        private readonly IShiftCsvExporter _exporter;

        public ICommand OpenAdminCommand { get; }

        public ICommand ExportCsvRowsCommand { get; }

        public ICommand GenerateShiftCommand { get; }

        public ICommand UpdateShiftCommand { get; }

        public ObservableCollection<ShiftTypeM> ShiftTypes { get; } = new(); // プルダウン用マスタ

        private readonly Dictionary<string, int> _symbolToId = new();

        private readonly HashSet<int> _parentTypeIds = new();

        //private readonly HashSet<string> _parentSymbols = new(new[] { "当", "●", "日" });

        //private Dictionary<(int Eid, DateTime Date), string> _originalSymbolMap = new();

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
        //カレンダー初期化用バインディング
        private DateTime? _selectedDate = DateTime.Today;
        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    OnPropertyChanged(nameof(TodayDutyNames));
                    OnPropertyChanged(nameof(TodayDayWorkNames));
                }
            }
        }

        // 画面右下「本日の当直/日勤」表示用（SelectedDateが未選択ならシステムの今日にフォールバック）
        public string TodayDutyNames => string.Join("、", GetNamesForSymbolOnSelectedDate("当"));
        public string TodayDayWorkNames => string.Join("、", GetNamesForSymbolOnSelectedDate("日"));

        private IEnumerable<string> GetNamesForSymbolOnSelectedDate(string symbol)
        {
            var date = SelectedDate ?? DateTime.Today;
            var key = date.ToString("yyyy-MM-dd");

            return ShiftDataCollection
                .Where(loader => loader[key] == symbol)
                .Select(loader => loader.EmployeeName ?? string.Empty);
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
                    OnPropertyChanged(nameof(DisplayDate));
                   
                }
                LoadShiftDataForMonth(_displayDate);
            }
        }


        public MainViewModel()
        {
            _dataLoader = new ShiftDataLoader();

            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>();

            ShiftGridColumns = new ObservableCollection<DataGridColumn>();

            db = new MainDatabaseHelper();

            _business = new ShiftBusiness(db);

            _exporter = new CsvHelperExporter();

            _csvBiz = new CsvBusiness(db, _exporter);

            ExportCsvRowsCommand = new RelayCommand(async p => await ExportCsvRowsAsync(p));

            OpenAdminCommand = new RelayCommand(OpenAdmin);

            GenerateShiftCommand = new RelayCommand(p => GenerateShift(p));

            UpdateShiftCommand = new RelayCommand(p => UpdateShift(p));
        }

        private void OpenAdmin(object? _)
        {
            var admin = new AdminWindow();
            admin.ShowDialog();
        }

        private void GenerateShift(object? param)
        {
            if (param is DateTime displayDate)
            {
                Log.Warning("シフトの自動生成を開始します。{displayDate}", displayDate);
                MakeNightDuty(displayDate);
                GenerateOffShift(displayDate);
                LoadShiftDataForMonth(displayDate);
            }
            else
            {
                // パラメータが null の場合のフォールバック（例: 今日の月）
                MakeNightDuty(DateTime.Today);
                GenerateOffShift(DateTime.Today);
                LoadShiftDataForMonth(DateTime.Today);
            }
        }

        private void UpdateShift(object? param)
        {
            var dirty = ShiftDataCollection.Where(e => e.IsDirty).ToList();
            if (dirty.Count == 0)
            {
                MessageBox.Show("保存する変更がありません。",
                    "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _business.SaveShifts(DisplayDate, dirty, _symbolToId);

                // 保存成功したら Dirty クリア
                foreach (var row in dirty)
                {
                    row.AcceptChanges();
                }

                MessageBox.Show("シフトを保存しました。", "完了",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"シフト保存中にエラーが発生しました:\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }

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
            db.DeleteMonthDutyAndDayParentsWithCascade(month);

            // _symbolToId はLoadShiftDataForMonth経由でしか埋まらないため、参照前に明示的にロードする
            LoadShiftTypes();

            var employees = db.GetActiveEmployeesForScheduling();
            var (first, last) = ShiftBusiness.GetMonthRange(month);
            var preloadStart = first.AddDays(-7);
            var preloadEnd = last.AddDays(21); // 週末代休を安全に見る
            var existingMap = db.GetShiftMap(preloadStart, preloadEnd);
            var holidays = _business.GetHolidaysInMonth(month).Select(h => h.date).ToList();
            var preferences = db.GetAllActivePreferencesByEmployee();

            var solver = new ShiftsSolver(month, employees, existingMap, holidays,
                _symbolToId["当"], _symbolToId["明"], _symbolToId["●"], _symbolToId["○"], _symbolToId["日"],
                baselineIsA: false, // UpdateSaturdayShifts(GenerateOffShift)の既定"B"に合わせる
                preferencesByEmployee: preferences);

            var writes = solver.Solve();
            db.BulkUpsert_Duty_Shifts(writes, month);

            LoadShiftDataForMonth(month);
        }

        public void LoadShiftDataForMonth(DateTime month)
        {
            var db = new MainDatabaseHelper();

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

            foreach (var e in employees.OrderBy(x => x.ShiftId))
            {
                var loader = new ShiftDataLoader
                {
                    EmployeeId = e.EmployeeId,
                    ShiftId = e.ShiftId,
                    EmployeeName = e.EmployeeName,
                    Role = e.Role
                };

                // その月の全日付キーを空で用意
                for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
                {
                    loader[d.ToString("yyyy-MM-dd")] = string.Empty;
                }

                // 3-2) 実シフトを上書き
                foreach (var s in shifts.Where(s => s.EmployeeId == e.EmployeeId))
                {
                    loader[s.ShiftDate.ToString("yyyy-MM-dd")] = s.Symbol ?? string.Empty;
                }

                loaders.Add(loader);

                loader.AcceptChanges();
            }

            var ordered = loaders
                   .OrderBy(x => x.Role)
                   .ThenBy(x => x.ShiftId)
                   .ToList();

            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>(ordered);

            // 5) 列（ID/名前＋1..末日）
            ShiftGridColumns = GridHelperClass.GenerateColumnsForMonth(month);

            // ShiftDataCollectionを丸ごと差し替えたため、本日表示パネルにも再計算を通知する
            OnPropertyChanged(nameof(TodayDutyNames));
            OnPropertyChanged(nameof(TodayDayWorkNames));
        }
    }
}