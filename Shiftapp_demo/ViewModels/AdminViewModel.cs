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
using System.Windows.Controls;
using System.Windows.Input;


namespace Shiftapp_demo.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {
        private ShiftDataLoader _dataLoader;

        private readonly DatabaseHelper _db;

        private readonly ShiftBusiness _business;

        // ====== 画面バインド用コレクション ======
        public ObservableCollection<Employee> Employees { get; } = new();
        public ObservableCollection<Holiday> Holidays { get; } = new();

        //カレンダー用
        public DateTime? SelectedDate { get; set; } = DateTime.Today;

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
            }
        }

        // DataGrid の列を VM から流す用
        private ObservableCollection<DataGridColumn> _techniciansDataGridColumns = new();
        public ObservableCollection<DataGridColumn> TechniciansDataGridColumns
        {
            get => _techniciansDataGridColumns;
            private set => SetProperty(ref _techniciansDataGridColumns, value);
        }

        // 表示中の月（初期値＝今月）
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

        public AdminViewModel()
        {
            _dataLoader = new ShiftDataLoader();

            _db = new DatabaseHelper();

            _business = new ShiftBusiness(_db);

            // ★ 列をここで生成（Helper を呼ぶ）
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
            foreach (var e in _db.GetAllEmployees()) // 同期I/Oなら Task.Run に包む必要はない
            {
                if (ct.IsCancellationRequested) return;
                Employees.Add(e);
            }

            // --- 祝日（当月） ---
            Holidays.Clear();
            var first = new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
            var next = first.AddMonths(1);

            var holidays = _db.GetHolidays(first, next); // List<Holiday>（小文字プロパティ版）
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

        //// INotifyPropertyChanged 実装
        //public event PropertyChangedEventHandler? PropertyChanged;
        //public virtual void OnPropertyChanged(string propertyName)
        //{
        //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        //}

        // SetProperty ヘルパー
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

    }
}
