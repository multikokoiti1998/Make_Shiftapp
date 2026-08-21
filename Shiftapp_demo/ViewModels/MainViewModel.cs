// MainViewModel.cs
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Serilog;
using Shiftapp_demo.Business; // DataGridColumn, TextBlock を参照するため
using Shiftapp_demo.Csv;
using Shiftapp_demo.DataAccess; // DatabaseHelperを参照するため
using Shiftapp_demo.Excel;
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

        public ICommand ImportExcelCommand { get; }

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

            ImportExcelCommand = new RelayCommand(_ => ImportExcel());

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
            var targetMonth = param is DateTime displayDate ? displayDate : DateTime.Today;

            try
            {
                Log.Warning("シフトの自動生成を開始します。{targetMonth}", targetMonth);
                MakeNightDuty(targetMonth);
                GenerateOffShift(targetMonth);
                _business.UpdateShortTimeShifts(targetMonth);
                LoadShiftDataForMonth(targetMonth);
            }
            catch (InvalidOperationException ex)
            {
                // ソルバーが条件を満たせなかった場合（例: シフト生成不可）。
                // アプリ全体の汎用エラーダイアログに落とさず、具体的な理由を表示する。
                Log.Warning(ex, "シフト自動生成に失敗しました。{targetMonth}", targetMonth);
                MessageBox.Show($"シフトの自動生成に失敗しました:\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ImportExcel()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Excelファイル (*.xlsx)|*.xlsx",
                Title = "完成した勤務表(Excel)を選択してください"
            };

            if (ofd.ShowDialog() != true) return;

            ShiftExcelImportResult result;
            try
            {
                result = ShiftExcelReader.Read(ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel読込中にエラーが発生しました:\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 1) ExcelにいるがDB未登録の職員を追加登録する
            //    （個人コード＝employee_idのため、そのままemployee_idとして使う。
            //      カテーテル対応など詳細は不明なため不可(false)で登録し、管理者画面で後から設定してもらう）
            var existingIds = new HashSet<int>(db.GetAllEmployees().Select(e => e.EmployeeId));
            var newlyRegistered = new List<int>();

            foreach (var g in result.Entries.GroupBy(e => e.EmployeeId))
            {
                if (existingIds.Contains(g.Key)) continue;

                db.InsertEmployeeIfMissing(g.Key, g.First().EmployeeName);
                newlyRegistered.Add(g.Key);
            }

            // 2) 実績の土曜日出勤/休みのパターンから、各職員の土曜日班(A/B)を判定ロジックの逆算で更新する
            //    （baselineIsA: false は UpdateSaturdayShifts(GenerateOffShift) の既定"B"に合わせる）
            var updatedClassCount = 0;
            foreach (var g in result.Entries.Where(e => e.Date.DayOfWeek == DayOfWeek.Saturday).GroupBy(e => e.EmployeeId))
            {
                var inferred = ShiftBusiness.InferSaturdayClass(
                    g.Select(e => (e.Date, e.Symbol)), baselineIsA: false);

                if (inferred != null)
                {
                    db.UpdateSaturdayClass(g.Key, inferred);
                    updatedClassCount++;
                }
            }

            // 対象月を表示（グリッドがDBの現状で作り直される。新規登録した職員もここで反映される）
            DisplayDate = new DateTime(result.Year, result.Month, 1);

            var loaderById = ShiftDataCollection.ToDictionary(l => l.EmployeeId);
            var unmatchedIds = new List<int>();

            foreach (var entry in result.Entries)
            {
                if (!loaderById.TryGetValue(entry.EmployeeId, out var loader))
                {
                    if (!unmatchedIds.Contains(entry.EmployeeId))
                        unmatchedIds.Add(entry.EmployeeId);
                    continue;
                }

                // シフト種別マスタに無い記号（例:「短」）は自動登録して取り込めるようにする
                if (!string.IsNullOrEmpty(entry.Symbol) && !_symbolToId.ContainsKey(entry.Symbol))
                {
                    db.EnsureShiftType(entry.Symbol, entry.Symbol);
                    LoadShiftTypes();
                }

                loader[entry.Date.ToString("yyyy-MM-dd")] = entry.Symbol;
            }

            var dirtyCount = ShiftDataCollection.Count(l => l.IsDirty);
            var msg = $"{result.Year}年{result.Month}月分を読み込みました（変更 {dirtyCount} 名）。\n" +
                       "内容を確認のうえ「シフト修正」で保存してください。";
            if (newlyRegistered.Count > 0)
                msg += $"\n新規登録した職員コード（カテーテル不可で登録。詳細は管理者画面で設定してください）: {string.Join(", ", newlyRegistered)}";
            if (updatedClassCount > 0)
                msg += $"\n実績から土曜日班(A/B)を再判定して更新した職員数: {updatedClassCount}";
            if (unmatchedIds.Count > 0)
                msg += $"\n未登録の職員コードのため取り込めなかった行: {string.Join(", ", unmatchedIds)}";

            MessageBox.Show(msg, "Excel読込", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // 代休/明けセルのツールチップ用テキストを組み立てる（例: "9/18(金)の当直の代休"）
        private static string? BuildOriginTooltip(DateTime? originDate, string? originSymbol)
        {
            if (!originDate.HasValue) return null;

            var kind = originSymbol switch
            {
                "当" => "当直",
                "日" => "日勤",
                _ => null
            };

            var youbi = originDate.Value.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("ja-JP"));
            var dateText = $"{originDate.Value.Month}/{originDate.Value.Day}({youbi})";

            return kind != null ? $"{dateText}の{kind}の代休" : dateText;
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
                    var key = s.ShiftDate.ToString("yyyy-MM-dd");
                    loader[key] = s.Symbol ?? string.Empty;
                    loader.SetTooltip(key, BuildOriginTooltip(s.OriginDate, s.OriginSymbol));
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
            // baselineIsA: UpdateSaturdayShifts(GenerateOffShift)の既定"B"に合わせる
            ShiftGridColumns = GridHelperClass.GenerateColumnsForMonth(month, baselineIsA: false);

            // ShiftDataCollectionを丸ごと差し替えたため、本日表示パネルにも再計算を通知する
            OnPropertyChanged(nameof(TodayDutyNames));
            OnPropertyChanged(nameof(TodayDayWorkNames));
        }
    }
}