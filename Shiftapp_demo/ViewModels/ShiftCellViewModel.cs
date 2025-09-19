using System;
using System.ComponentModel;

namespace Shiftapp_demo.ViewModels
{
    /// <summary>
    /// 1セル（1人×1日）の表示・編集・Dirty管理を担当
    /// </summary>
    public sealed class ShiftCellViewModel : INotifyPropertyChanged
    {
        public int EmployeeId { get; }
        public DateTime Date { get; }

        private string _symbol;            // 現在値（"/","○","当","明","●","日" など）
        public string Symbol
        {
            get => _symbol;
            set
            {
                if (_symbol == value) return;
                _symbol = value ?? string.Empty;
                IsDirty = _symbol != OriginalSymbol;
                OnPropertyChanged(nameof(Symbol));
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        /// <summary>DB読込時の元値（保存成功時にこれへ更新）</summary>
        public string OriginalSymbol { get; private set; }

        /// <summary>編集未保存かどうか</summary>
        public bool IsDirty { get; private set; }

        public ShiftCellViewModel(int employeeId, DateTime date, string symbol)
        {
            EmployeeId = employeeId;
            Date = date.Date;
            _symbol = symbol ?? string.Empty;
            OriginalSymbol = _symbol;
            IsDirty = false;
        }

        /// <summary>保存成功後に呼ぶ：Dirty解除＆元値を現在値へ</summary>
        public void Accept()
        {
            OriginalSymbol = _symbol;
            IsDirty = false;
            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>変更破棄：元値へ戻す</summary>
        public void Reject()
        {
            _symbol = OriginalSymbol;
            IsDirty = false;
            OnPropertyChanged(nameof(Symbol));
            OnPropertyChanged(nameof(IsDirty));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
