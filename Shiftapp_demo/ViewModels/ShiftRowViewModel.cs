using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Shiftapp_demo.ViewModels
{
    /// <summary>
    /// 1行（1人の1ヶ月分セル群）を束ねるVM
    /// DataGridのItemsSourceは通常これのコレクション（ObservableCollection&lt;ShiftRowViewModel&gt;）
    /// </summary>
    public sealed class ShiftRowViewModel : INotifyPropertyChanged
    {
        public int EmployeeId { get; }
        public string EmployeeName { get; }

        /// <summary>
        /// 日付→セルVM（UIからは Cells[date].Symbol にバインド）
        /// </summary>
        public Dictionary<DateTime, ShiftCellViewModel> Cells { get; } = new();

        /// <summary>
        /// XAMLのBindingで便利なインデクサ（例: Cells[2025-09-01] のように評価される）
        /// </summary>
        public ShiftCellViewModel this[DateTime date]
            => Cells[date.Date];

        public ShiftRowViewModel(int employeeId, string employeeName)
        {
            EmployeeId = employeeId;
            EmployeeName = employeeName;
        }

        /// <summary>
        /// 指定日のセルを作成or上書き（読込時/再読込時に使用）
        /// </summary>
        public ShiftCellViewModel SetCell(DateTime date, string symbol)
        {
            date = date.Date;
            if (Cells.TryGetValue(date, out var cell))
            {
                // 再読込や手動リセット時は強制的に元値も更新したい場合がある
                cell = Cells[date] = new ShiftCellViewModel(EmployeeId, date, symbol);
                OnPropertyChanged(nameof(Cells));
                return cell;
            }
            else
            {
                var created = new ShiftCellViewModel(EmployeeId, date, symbol);
                Cells.Add(date, created);
                OnPropertyChanged(nameof(Cells));
                return created;
            }
        }

        /// <summary>未保存セルの列挙</summary>
        public IEnumerable<ShiftCellViewModel> DirtyCells
            => Cells.Values.Where(c => c.IsDirty);

        /// <summary>保存成功時に全セルをAccept</summary>
        public void AcceptAll()
        {
            foreach (var c in Cells.Values) c.Accept();
        }

        /// <summary>変更破棄（DB状態に戻す）</summary>
        public void RejectAll()
        {
            foreach (var c in Cells.Values) c.Reject();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
