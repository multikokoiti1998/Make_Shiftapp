using Shiftapp_demo.Business;
using Shiftapp_demo.Csv;
using Shiftapp_demo.DataAccess;
using Shiftapp_demo.FrameWork;
using Shiftapp_demo.Helper;
using Shiftapp_demo.Models;
using Shiftapp_demo.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace Shiftapp_demo.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {

        private readonly AdminDatabaseHelper _db;


        // ====== 画面バインド用コレクション ======
        public ObservableCollection<Employee> Employees { get; } = new();
        public ObservableCollection<Holiday> Holidays { get; } = new();

        // 入力用：日付
        private DateTime? _newHolidayDate = DateTime.Today;
        public DateTime? NewHolidayDate
        {
            get => _newHolidayDate;
            set => SetProperty(ref _newHolidayDate, value);
        }

        // 入力用：名称
        private Employee? _selectedEmployee;
        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (_selectedEmployee != value)
                {
                    _selectedEmployee = value;
                    OnPropertyChanged(nameof(SelectedEmployee));
                    (DeleteEmployeeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    LoadPreferencesForSelectedEmployee();
                }
            }
        }

        // ====== 選択技師の勤務希望 ======
        public ObservableCollection<EmployeePreference> SelectedEmployeePreferences { get; } = new();

        public ObservableCollection<OptionItem> PreferenceDayOptions { get; } = new(new[]
        {
            new OptionItem("weekend", "週末（土日）"),
            new OptionItem(((int)DayOfWeek.Sunday).ToString(), "日曜日"),
            new OptionItem(((int)DayOfWeek.Monday).ToString(), "月曜日"),
            new OptionItem(((int)DayOfWeek.Tuesday).ToString(), "火曜日"),
            new OptionItem(((int)DayOfWeek.Wednesday).ToString(), "水曜日"),
            new OptionItem(((int)DayOfWeek.Thursday).ToString(), "木曜日"),
            new OptionItem(((int)DayOfWeek.Friday).ToString(), "金曜日"),
            new OptionItem(((int)DayOfWeek.Saturday).ToString(), "土曜日"),
        });

        public ObservableCollection<OptionItem> PreferencePolarityOptions { get; } = new(new[]
        {
            new OptionItem(((int)PreferencePolarity.Prefer).ToString(), "希望する"),
            new OptionItem(((int)PreferencePolarity.Avoid).ToString(), "避けたい"),
        });

        private OptionItem? _newPreferenceDay;
        public OptionItem? NewPreferenceDay
        {
            get => _newPreferenceDay;
            set => SetProperty(ref _newPreferenceDay, value);
        }

        private OptionItem? _newPreferencePolarity;
        public OptionItem? NewPreferencePolarity
        {
            get => _newPreferencePolarity;
            set => SetProperty(ref _newPreferencePolarity, value);
        }

        public ICommand AddPreferenceCommand { get; }
        public ICommand RemovePreferenceCommand { get; }

        private string _newHolidayName= string.Empty;
        public string NewHolidayName
        {
            get => _newHolidayName;
            set => SetProperty(ref _newHolidayName, value);
        }

        private Holiday? _selectedHoliday;
        public Holiday? SelectedHoliday
        {
            get => _selectedHoliday;
            set => SetProperty(ref _selectedHoliday, value);
        }

        // ====== バインド用コマンド ======
        public ICommand AddEmployeeCommand { get; }

        public ICommand DeleteEmployeeCommand { get; }

        public ICommand SaveEmployeeCommand { get; }

        public ICommand AddHolidaysCommand { get; }

        public ICommand DeleteHolidayCommand { get; }

        //選択用

        public DateTime? SelectedDate { get; set; } = DateTime.Today;

        private DateTime _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        public DateTime CurrentMonth
        {
            get => _currentMonth;
            set
            {
                if (SetProperty(ref _currentMonth, new DateTime(value.Year, value.Month, 1)))
                {
                    _ = LoadAsync();
                }
            }
        }

        // DataGrid の列を VM から流す用
        private ObservableCollection<DataGridColumn> _techniciansDataGridColumns = new();

        public ObservableCollection<DataGridColumn> TechniciansDataGridColumns
        {
            get => _techniciansDataGridColumns;
            private set => SetProperty(ref _techniciansDataGridColumns, value);
        }



        public AdminViewModel()
        {

            _db = new AdminDatabaseHelper();

            AddEmployeeCommand = new RelayCommand(_ => AddEmployee());

            DeleteEmployeeCommand = new RelayCommand(
            _ => DeleteEmployee(),
            _ => SelectedEmployee != null  
                 );

            SaveEmployeeCommand = new RelayCommand(_ => SaveEmployees());

            AddHolidaysCommand = new RelayCommand(_ => AddHolidays());

            DeleteHolidayCommand = new RelayCommand(
            _ => DeleteHoliday(SelectedHoliday),
            _ => SelectedHoliday != null
                );

            AddPreferenceCommand = new RelayCommand(
            _ => AddPreference(),
            _ => SelectedEmployee != null && NewPreferenceDay != null && NewPreferencePolarity != null
                );

            RemovePreferenceCommand = new RelayCommand(
            p => RemovePreference(p as EmployeePreference)
                );

            NewPreferencePolarity = PreferencePolarityOptions[0];

            TechniciansDataGridColumns =
                AdminGridHelperClass.GenerateColumnsForAdminEmployee(SaturdayClassOptions, RoleClassOptions);

        }

        /// <summary>
        /// 初期表示や月変更時に呼ぶ非同期ロード
        /// </summary>
        public async Task LoadAsync(CancellationToken ct = default)
        {
            // --- 技師一覧 ---
            Employees.Clear();
            foreach (var e in _db.GetAllEmployees())
            {
                if (ct.IsCancellationRequested) return;
                Employees.Add(e);
            }

            // 週末・祝日当直比率（全期間）を技師一覧にマージする（表示専用、DB保存対象外のプロパティ）
            var ratios = _db.GetWeekendHolidayDutyRatios().ToDictionary(r => r.EmployeeId);
            foreach (var e in Employees)
            {
                if (ratios.TryGetValue(e.EmployeeId, out var r))
                {
                    e.WeekendHolidayDutyCount = r.WeekendHolidayDutyCount;
                    e.TotalDutyCountAllTime = r.TotalDutyCount;
                }
            }

            foreach (var e in Employees)
            {
                e.AcceptChanges();
            }

            // --- 祝日（当月） ---
            Holidays.Clear();
            var first = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            var next = first.AddMonths(1);

            var holidays = _db.GetAllHolidays(CurrentMonth);
            var holidays_ordered = holidays
                            .OrderBy(x => x.date)
                            .ToList();
            foreach (var h in holidays_ordered)
            {
                if (ct.IsCancellationRequested) return;
                Holidays.Add(new Holiday
                {
                    holiday_id = h.holiday_id,
                    date = h.date.Date,
                    name = h.name
                });
            }

            await Task.CompletedTask;
        }

        // ====== コマンドの関数 ======
        private void AddEmployee()
        {
            int newId = _db.InsertBlankEmployee();

            var emp = new Employee
            {
                EmployeeId = newId,
                ShiftId = 0,
                EmployeeName = "",
                CanDoCatheterization = false,
                SaturdayClass = "A",
                MonthlyDutyLimit = 0,
                CanDoNightDuty = false,
                Role = 0,
                CanDayDuty = false,
            };

            InsertEmployeeSorted(emp);

            // 選択中にするなら
            SelectedEmployee = emp;
        }

        /// <summary>
        /// GetAllEmployees()と同じ並び順（Role昇順→employee_id昇順）を保つ位置に挿入する。
        /// 末尾にAddするだけだと、新規追加した職員が役職順を無視して一番下に表示されてしまうため。
        /// </summary>
        private void InsertEmployeeSorted(Employee emp)
        {
            int index = 0;
            while (index < Employees.Count &&
                   (Employees[index].Role < emp.Role ||
                    (Employees[index].Role == emp.Role && Employees[index].EmployeeId <= emp.EmployeeId)))
            {
                index++;
            }
            Employees.Insert(index, emp);
        }

        private void DeleteEmployee()
        {
            if (SelectedEmployee is null) return;

            var emp = SelectedEmployee;

            // 確認ダイアログ（お好みで）
            var result = MessageBox.Show(
                $"{emp.EmployeeName} さんを削除しますか？",
                "確認",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.OK)
                return;

            try
            {

                _db.DeleteEmployee(emp.EmployeeId);

                Employees.Remove(emp);

                SelectedEmployee = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"職員削除中にエラーが発生しました:\n{ex.Message}",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void SaveEmployees()
        {
            var dirty = Employees.Where(e => e.IsDirty).ToList();
            if (dirty.Count == 0)
            {
                MessageBox.Show("保存する変更がありません。",
                    "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                foreach (var e in dirty)
                {
                    if (string.IsNullOrWhiteSpace(e.EmployeeName))
                    {
                        MessageBox.Show("名前が空の職員があります。",
                            "入力不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // すべて UPDATE でよい（Add時点で DB に INSERT されているため）
                    _db.UpdateEmployee(e);

                    // 完了したら Dirty フラグをクリア
                    e.AcceptChanges();
                }

                MessageBox.Show("職員情報を保存しました。",
                    "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"職員情報の保存中にエラーが発生しました:\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private void AddHolidays()
        {
            if (NewHolidayDate is not DateTime sel)
            {
                MessageBox.Show("日付を選択してください。", "入力不足",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var d = sel.Date;
            var name = (NewHolidayName ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                name = "休日";

            // 同じ日付が既にあるかチェック
            if (Holidays.Any(x => x.date.Date == d))
            {
                MessageBox.Show("同じ日付の休日が既にあります。", "重複",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newHoliday = new Holiday
            {
                date = d,
                name = name
            };

            try
            {
                _db.InsertHoliday(newHoliday);

                Holidays.Add(newHoliday);

                NewHolidayName = string.Empty;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"祝日の追加中にエラーが発生しました:\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteHoliday(Holiday? target)
        {
            if (target is null)
                return;

            // 削除
            if (MessageBox.Show(
                    $"「{target.date:yyyy/MM/dd} {target.name}」を削除しますか？",
                    "確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            // DB から削除
            _db.DeleteHoliday(target.date.Date);

            // コレクションから削除
            Holidays.Remove(target);
        }

        // ====== 勤務希望 ======
        private void LoadPreferencesForSelectedEmployee()
        {
            SelectedEmployeePreferences.Clear();
            if (_selectedEmployee is null) return;

            foreach (var p in _db.GetPreferencesForEmployee(_selectedEmployee.EmployeeId))
                SelectedEmployeePreferences.Add(p);
        }

        private void AddPreference()
        {
            if (SelectedEmployee is null || NewPreferenceDay is null || NewPreferencePolarity is null)
                return;

            bool isWeekend = NewPreferenceDay.Code == "weekend";

            var pref = new EmployeePreference
            {
                EmployeeId = SelectedEmployee.EmployeeId,
                IsWeekend = isWeekend,
                DayOfWeek = isWeekend ? null : (DayOfWeek)int.Parse(NewPreferenceDay.Code),
                Polarity = (PreferencePolarity)int.Parse(NewPreferencePolarity.Code),
            };

            try
            {
                pref.PreferenceId = _db.InsertPreference(pref);
                SelectedEmployeePreferences.Add(pref);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"希望の追加中にエラーが発生しました:\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemovePreference(EmployeePreference? target)
        {
            if (target is null) return;

            try
            {
                _db.DeletePreference(target.PreferenceId);
                SelectedEmployeePreferences.Remove(target);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"希望の削除中にエラーが発生しました:\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // ====== 土曜日班オプション ======
        public ObservableCollection<OptionItem> SaturdayClassOptions { get; } =
            new ObservableCollection<OptionItem>
            {
                new OptionItem("A", "A 班"),
                new OptionItem("B", "B 班"),
            };

        // ====== 役職オプション ======
        public ObservableCollection<OptionItem> RoleClassOptions { get; } =
            new ObservableCollection<OptionItem>
            {
                new OptionItem("0", "技師長"),
                new OptionItem("1", "副技師長"),
                new OptionItem("2", "主任"),
                new OptionItem("3", "正規技師"),
                new OptionItem("4", "非正規技師"),
            };

        //protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        //{
        //    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        //    field = value;
        //    OnPropertyChanged(name);
        //    return true;
        //}

    }
}
