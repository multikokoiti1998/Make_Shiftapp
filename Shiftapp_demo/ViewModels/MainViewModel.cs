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

        // 名前クリック（行選択）で表示する「その職員の今月の当直/明け/代休」一覧
        private ShiftDataLoader? _selectedEmployeeForSummary;
        public ShiftDataLoader? SelectedEmployeeForSummary
        {
            get => _selectedEmployeeForSummary;
            set
            {
                if (SetProperty(ref _selectedEmployeeForSummary, value))
                {
                    OnPropertyChanged(nameof(SelectedEmployeeSummaryHeader));
                    OnPropertyChanged(nameof(SelectedEmployeeDutyDates));
                    OnPropertyChanged(nameof(SelectedEmployeeAkeDates));
                    OnPropertyChanged(nameof(SelectedEmployeeCompOffDates));
                }
            }
        }

        public string SelectedEmployeeSummaryHeader =>
            SelectedEmployeeForSummary != null ? $"{SelectedEmployeeForSummary.EmployeeName} さんの今月の予定" : "職員を選択してください";

        public string SelectedEmployeeDutyDates => FormatDatesForSymbol(SelectedEmployeeForSummary, "当");
        public string SelectedEmployeeAkeDates => FormatDatesForSymbol(SelectedEmployeeForSummary, "明");

        // ●は当直/日勤の代休と、日曜/祝日の通常の休みマーカーの両方に使われている記号なので、
        // シンボル一致だけで集計すると普通の休みまで「代休」に含まれてしまう。
        // 当直/日勤に紐づく本物の代休だけがToolTip（元の当直/日勤日）を持つため、それで絞り込む。
        public string SelectedEmployeeCompOffDates => FormatDatesForSymbol(SelectedEmployeeForSummary, "●", requireOrigin: true);

        private static string FormatDatesForSymbol(ShiftDataLoader? loader, string symbol, bool requireOrigin = false)
        {
            if (loader == null) return "";

            var dates = loader.Shifts
                .Where(kv => kv.Value == symbol && (!requireOrigin || loader.Tooltip[kv.Key] != null))
                .Select(kv => DateTime.Parse(kv.Key))
                .OrderBy(d => d)
                .ToList();

            if (dates.Count == 0) return "なし";

            return string.Join("、", dates.Select(d =>
                $"{d.Month}/{d.Day}({d.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("ja-JP"))})"));
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
            catch (Exception ex)
            {
                // OrTools（ネイティブライブラリ）の読み込み失敗など、環境依存の予期しない失敗を
                // アプリ全体の汎用ハンドラに落とさずここで捕捉する。
                Log.Error(ex, "シフト自動生成中に予期しないエラーが発生しました。{targetMonth}", targetMonth);
                MessageBox.Show(
                    "シフトの自動生成中に予期しないエラーが発生しました。\n" +
                    "このPCの環境（Visual C++ ランタイム未導入など）が原因の可能性があります。\n" +
                    "ログを確認のうえ、管理者にご連絡ください。",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateShift(object? param)
        {
            // 末尾の当直/明け/代休 人数集計行は保存対象外（念のため二重に除外しておく）
            var dirty = ShiftDataCollection.Where(e => e.IsDirty && !e.IsSummaryRow).ToList();
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

            // 末尾の当直/明け/代休 人数集計行は職員ではない（EmployeeIdが全て既定値の0で重複する）ため除外する
            var loaderById = ShiftDataCollection.Where(l => !l.IsSummaryRow).ToDictionary(l => l.EmployeeId);
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
            // _symbolToId はLoadShiftDataForMonth経由でしか埋まらないため、参照前に明示的にロードする
            LoadShiftTypes();

            // 削除に影響されない読み取りは先に済ませておく
            var employees = db.GetActiveEmployeesForScheduling();
            var (first, last) = ShiftBusiness.GetMonthRange(month);
            var preloadStart = first.AddDays(-7);
            var preloadEnd = last.AddDays(21); // 週末代休を安全に見る
            var holidays = _business.GetHolidaysInMonth(month).Select(h => h.date).ToList();
            var preferences = db.GetAllActivePreferencesByEmployee();
            var historicalWeekendHolidayDutyCounts = db.GetHistoricalWeekendHolidayDutyCounts(month);

            // 削除〜ソルバー実行〜書き込みまでを単一トランザクションにまとめる。
            // ソルバー(ネイティブライブラリの読み込み失敗やINFEASIBLEを含む)が例外を投げた場合、
            // tx.Commit() に到達せず using の Dispose で自動的にロールバックされ、
            // 当月の既存データが削除されたままにならないようにする。
            using var con = db.OpenConnection();
            using var tx = con.BeginTransaction();

            db.DeleteMonthDutyAndDayParentsWithCascade(con, tx, month);

            var existingMap = db.GetShiftMap(con, tx, preloadStart, preloadEnd);

            var solver = new ShiftsSolver(month, employees, existingMap, holidays,
                _symbolToId["当"], _symbolToId["明"], _symbolToId["●"], _symbolToId["○"], _symbolToId["日"],
                baselineIsA: false, // UpdateSaturdayShifts(GenerateOffShift)の既定"B"に合わせる
                preferencesByEmployee: preferences,
                historicalWeekendHolidayDutyCounts: historicalWeekendHolidayDutyCounts);

            var writes = solver.Solve();
            db.BulkUpsert_Duty_Shifts(con, tx, writes, month);

            tx.Commit();

            LoadShiftDataForMonth(month);
        }

        // 手動で「●」を入力した際、紐づけ先の当直/日勤を選ばせるダイアログを表示する
        private static DateTime? ShowCompOffDutyPicker(List<(DateTime DutyDate, string Symbol)> candidates)
        {
            var dialog = new CompOffDutyPickerDialog(candidates)
            {
                Owner = Application.Current?.MainWindow
            };

            return dialog.ShowDialog() == true ? dialog.SelectedDutyDate : null;
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

            // 2-2) 祝日一覧（代休アテンション判定用）。GetHolidaysInMonthは表示月＋翌月の2ヶ月分を返す
            // 仕様のため、前月分（前月＋当月）も別途取得して合算し、前月〜翌月の3ヶ月分をカバーする。
            // これが無いと、前月末の「月〜木祝日当直」がGetCompWorkOffの判定で祝日と認識されず、
            // 代休不要と誤判定されてしまう（前月分の当直をアテンション判定に含める今回の変更で必要になった）。
            var holidaysPreviousAndCurrent = _business.GetHolidaysInMonth(month.AddMonths(-1))
                .Select(h => h.date)
                .ToList();
            var holidaysCurrentAndNext = _business.GetHolidaysInMonth(month)
                .Select(h => h.date)
                .ToList();
            var holidaysAcrossThreeMonths = holidaysPreviousAndCurrent
                .Union(holidaysCurrentAndNext)
                .ToList();

            // 表示月内で「本来休みのはずの日」（祝日＋日曜）の一覧
            var compOffCheckDates = new HashSet<string>(
                holidaysAcrossThreeMonths
                    .Where(d => d >= firstDay && d <= lastDay)
                    .Select(d => d.ToString("yyyy-MM-dd")));
            foreach (var sunday in ShiftBusiness.GetSundaysInMonth(month))
                compOffCheckDates.Add(sunday.ToString("yyyy-MM-dd"));

            // 2-3) 代休が翌月にずれ込むケース（金土日当直→翌月初め等）を検出するため、翌月分のシフトも取得しておく。
            // OriginDateも一緒に持たせ、翌月の代休がどの当直/日勤に対応するか1対1で判定できるようにする。
            var nextMonthFirst = firstDay.AddMonths(1);
            var nextMonthLast = nextMonthFirst.AddMonths(1).AddDays(-1);
            var nextMonthShiftsByEmployee = db.GetShiftsOnly(nextMonthFirst, nextMonthLast)
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(s => s.ShiftDate.ToString("yyyy-MM-dd"), s => (Symbol: s.Symbol ?? string.Empty, OriginDate: s.OriginDate)));

            // 2-4) 前月分のシフトを丸ごと取得しておく（nextMonthFirst/nextMonthLastと対称に、
            // 前月全体を対象にする。以前は「前月末10日分」に絞っていたが、代休を手動で紐づける際の
            // 候補（TryAutoLinkManualCompOff）が前月の当直をすべて拾えるようにするため、
            // 月全体に広げた）。
            var previousMonthFirst = firstDay.AddMonths(-1);
            var previousMonthLast = firstDay.AddDays(-1);
            var previousMonthShiftsRaw = db.GetShiftsOnly(previousMonthFirst, previousMonthLast);
            var previousMonthDutiesByEmployee = previousMonthShiftsRaw
                .Where(s => s.Symbol == "当" || s.Symbol == "日")
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(s => (DutyDate: s.ShiftDate, Symbol: s.Symbol ?? string.Empty)).ToList());

            // 今月の当直/日勤の代休が前月側（再分散で前倒しされた等）に付いているケースを
            // CountLinkedCompOffForが検出できるよう、nextMonthShiftsByEmployeeと同じ形で保持する。
            var previousMonthShiftsByEmployee = previousMonthShiftsRaw
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(s => s.ShiftDate.ToString("yyyy-MM-dd"), s => (Symbol: s.Symbol ?? string.Empty, OriginDate: s.OriginDate)));

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

                loader.SetAttentionContext(
                    compOffCheckDates,
                    holidaysAcrossThreeMonths,
                    nextMonthShiftsByEmployee.TryGetValue(e.EmployeeId, out var nextMonthShifts)
                        ? nextMonthShifts
                        : new Dictionary<string, (string Symbol, DateTime? OriginDate)>(),
                    previousMonthDutiesByEmployee.TryGetValue(e.EmployeeId, out var previousMonthDuties)
                        ? previousMonthDuties
                        : new List<(DateTime DutyDate, string Symbol)>(),
                    previousMonthShiftsByEmployee.TryGetValue(e.EmployeeId, out var previousMonthShifts)
                        ? previousMonthShifts
                        : new Dictionary<string, (string Symbol, DateTime? OriginDate)>());

                // 手動で「●」を入力した際、紐づけ先の当直/日勤を管理者に選ばせるダイアログを開く
                loader.RequestCompOffLinkSelection = candidates => ShowCompOffDutyPicker(candidates);

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
                    if (s.OriginDate.HasValue)
                        loader.SetOrigin(key, s.OriginDate.Value);
                }

                loaders.Add(loader);

                loader.AcceptChanges();

                // 読み込みループ中は記号とOriginDateのセット順序の都合で正しく計算できないため、
                // 全データが揃った状態で改めてアテンションを計算する
                loader.RefreshAttention();

                // 以降の手動編集から、当直/日勤の連動（明け・代休の自動セット/解除）を有効にする
                loader.EnableCascade();
            }

            var ordered = loaders
                   .OrderBy(x => x.Role)
                   .ThenBy(x => x.ShiftId)
                   .ToList();

            // 4) 末尾に、その日の当直/明け/代休の人数を集計した行を追加する。
            // 代休(●)は当直/日勤に紐づく本物の代休のみをカウントし、日曜/祝日の通常の休みマーカー
            // （●だがTooltipが無い＝紐づけ無し）は除く（SelectedEmployeeCompOffDatesと同じ判定基準）。
            var dutyCountRow = new ShiftDataLoader { EmployeeName = "当直人数", IsSummaryRow = true };
            var akeCountRow = new ShiftDataLoader { EmployeeName = "明け人数", IsSummaryRow = true };
            var compOffCountRow = new ShiftDataLoader { EmployeeName = "代休人数", IsSummaryRow = true };

            for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
            {
                var key = d.ToString("yyyy-MM-dd");
                dutyCountRow[key] = ordered.Count(l => l[key] == "当").ToString();
                akeCountRow[key] = ordered.Count(l => l[key] == "明").ToString();
                compOffCountRow[key] = ordered.Count(l => l[key] == "●" && l.Tooltip[key] != null).ToString();
            }

            // EmployeeNameのセッターはIsSummaryRowに関係なく無条件にIsDirty=trueにするため、
            // このままだと「シフト修正」保存時にダミーの集計行まで保存対象に紛れ込んでしまう
            // （行の値が"2"等の数字でシフト記号ではないため保存時に例外になる）。明示的にクリアする。
            dutyCountRow.AcceptChanges();
            akeCountRow.AcceptChanges();
            compOffCountRow.AcceptChanges();

            ShiftDataCollection = new ObservableCollection<ShiftDataLoader>(ordered)
            {
                dutyCountRow,
                akeCountRow,
                compOffCountRow
            };

            // 5) 列（ID/名前＋1..末日）
            // baselineIsA: UpdateSaturdayShifts(GenerateOffShift)の既定"B"に合わせる
            ShiftGridColumns = GridHelperClass.GenerateColumnsForMonth(month, baselineIsA: false);

            // ShiftDataCollectionを丸ごと差し替えたため、本日表示パネルにも再計算を通知する
            OnPropertyChanged(nameof(TodayDutyNames));
            OnPropertyChanged(nameof(TodayDayWorkNames));
        }
    }
}