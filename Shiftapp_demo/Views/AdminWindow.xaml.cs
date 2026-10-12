using Shiftapp_demo.DataAccess;
using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Shiftapp_demo.ViewModels;
using System.Linq;                 // ← これ忘れがち
using System.Threading.Tasks;
using System;
using System.Windows.Input;
using System.Windows.Media;


namespace Shiftapp_demo.Views
{
    public partial class AdminWindow : Window
    {
        private readonly AdminViewModel _vm;
        public ObservableCollection<Employee> Employees { get; set; } = new();
        public ObservableCollection<Holiday> Holidays { get; set; } = new();       // 祝日一覧（当月）
        public AdminWindow(int? preselectEmployeeId = null)
        {
            InitializeComponent();

            _vm = new AdminViewModel();

            DataContext = _vm;

            Loaded += async (_, __) =>
            {
                await _vm.LoadAsync();

                TechniciansDataGrid.AutoGenerateColumns = false;
                TechniciansDataGrid.Columns.Clear();

                TechniciansDataGrid.Columns.Add(CreateDragHandleColumn());
                foreach (var c in _vm.TechniciansDataGridColumns)
                    TechniciansDataGrid.Columns.Add(c);

                if (preselectEmployeeId.HasValue)
                {
                    var match = _vm.Employees.FirstOrDefault(x => x.EmployeeId == preselectEmployeeId.Value);
                    if (match != null)
                    {
                        _vm.SelectedEmployee = match;
                        TechniciansDataGrid.SelectedItem = match;
                        TechniciansDataGrid.ScrollIntoView(match);
                        TechniciansDataGrid.Focus();
                    }
                }
            };



        }

        // ====== 職員の並び替え（ドラッグ＆ドロップ） ======
        // セル編集やチェックボックス操作と干渉しないよう、左端の「≡」列からのみドラッグを開始する。

        private DataGridColumn? _dragHandleColumn;
        private Point _dragStartPoint;
        private Employee? _dragSource;

        private DataGridColumn CreateDragHandleColumn()
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, "≡");
            text.SetValue(TextBlock.FontSizeProperty, 16.0);
            text.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            text.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetValue(CursorProperty, Cursors.SizeNS);
            text.SetValue(ToolTipProperty, "ドラッグして並び順を変更");

            _dragHandleColumn = new DataGridTemplateColumn
            {
                Header = "",
                Width = 32,
                IsReadOnly = true,
                CanUserResize = false,
                CellTemplate = new DataTemplate { VisualTree = text },
            };
            return _dragHandleColumn;
        }

        private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
        {
            while (d != null && d is not T)
                d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            return d as T;
        }

        private void TechniciansDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSource = null;
            var cell = FindAncestor<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell == null || cell.Column != _dragHandleColumn) return;

            _dragSource = cell.DataContext as Employee;
            _dragStartPoint = e.GetPosition(TechniciansDataGrid);
        }

        private void TechniciansDataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSource == null || e.LeftButton != MouseButtonState.Pressed) return;

            var diff = e.GetPosition(TechniciansDataGrid) - _dragStartPoint;
            if (Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance &&
                Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance)
                return;

            var source = _dragSource;
            _dragSource = null;
            TechniciansDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            DragDrop.DoDragDrop(TechniciansDataGrid, source, DragDropEffects.Move);
        }

        private void TechniciansDataGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(Employee)) ? DragDropEffects.Move : DragDropEffects.None;

            // ドロップ先の行を選択表示して、どこに入るか分かるようにする
            if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is Employee over)
                TechniciansDataGrid.SelectedItem = over;

            e.Handled = true;
        }

        private void TechniciansDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(Employee)) is not Employee source) return;
            if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not Employee target) return;

            _vm.MoveEmployee(source, target);
            TechniciansDataGrid.SelectedItem = source;
            TechniciansDataGrid.ScrollIntoView(source);
        }

        private void AdminCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (DataContext is not AdminViewModel vm) return;

            // 表示月を ViewModel に反映させるだけ
            if (AdminCalendar.DisplayDate == new DateTime(2020, 1, 1))
            {
                AdminCalendar.DisplayDate = DateTime.Today;
            }
        }
    }
}
