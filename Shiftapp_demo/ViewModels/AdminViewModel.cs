using Shiftapp_demo.Business;
using Shiftapp_demo.Csv;
using Shiftapp_demo.DataAccess;
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
                    // Delete ボタンの活性切り替え
                    (DeleteEmployeeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _newHolidayName;
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
                    // 月が変わったらロードし直す（非同期 fire-and-forget を避ける場合は外から LoadAsync を呼ぶ）
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
            _ => SelectedEmployee != null   // 選択されているときだけ押せる
                 );
            SaveEmployeeCommand = new RelayCommand(_ => SaveEmployees());

            AddHolidaysCommand = new RelayCommand(_ => AddHolidays());

            DeleteHolidayCommand = new RelayCommand(
            _ => DeleteHoliday(SelectedHoliday),
            _ => SelectedHoliday != null
                );

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

            // --- 祝日（当月） ---
            Holidays.Clear();
            var first = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            var next = first.AddMonths(1);

            var holidays = _db.GetAllHolidays(CurrentMonth); // List<Holiday>（小文字プロパティ版）
            foreach (var h in holidays)
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
            var emp = new Employee
            {
                EmployeeId = 0,              // 新規
                EmployeeName = "",
                CanDoCatheterization = false,
                SaturdayClass = "A",
                MonthlyDutyLimit = 0,
                CanDoNightDuty = false,
                Role = 0,
                CanDayDuty = false,
                is_active = 1
            };

            Employees.Add(emp);

            // 選択中にするなら
            SelectedEmployee = emp;
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
                if (emp.EmployeeId != 0)
                {
                    // 既にDBにいる人 → DBから削除
                    _db.DeleteEmployee(emp.EmployeeId);
                }
                // 新規行(EmployeeId == 0) は DB呼ばずにコレクションだけ消す

                // UI上からも削除
                Employees.Remove(emp);

                // 選択解除
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
            // 変更があった行だけ対象
            var dirtyOnes = Employees.Where(e => e.IsDirty).ToList();
            if (dirtyOnes.Count == 0)
            {
                MessageBox.Show("保存する変更がありません。", "情報",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                foreach (var e in dirtyOnes)
                {
                    // 最低限のバリデーション
                    if (string.IsNullOrWhiteSpace(e.EmployeeName))
                    {
                        MessageBox.Show("名前が空の職員があります。", "入力不足",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (e.EmployeeId == 0)
                    {
                        // 新規 → INSERT
                        var newId = _db.InsertEmployee(e);
                        e.EmployeeId = newId;
                    }
                    else
                    {
                        // 既存 → UPDATE
                        _db.UpdateEmployee(e);
                    }

                    // 保存完了したので Dirty フラグをクリア
                    e.AcceptChanges();
                }

                MessageBox.Show("職員情報を保存しました。", "完了",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"職員情報の保存中にエラーが発生しました:\n{ex.Message}",
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

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

    }
}
