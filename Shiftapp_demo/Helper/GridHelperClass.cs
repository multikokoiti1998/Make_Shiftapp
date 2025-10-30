using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace Shiftapp_demo.Helper
{
    class GridHelperClass
    {
        public static ObservableCollection<DataGridColumn> GenerateColumnsForMonth(DateTime month)
        {
            var columns = new ObservableCollection<DataGridColumn>();

            // セル内TextBlockを確実に中央寄せにするスタイル
            var center = new Style(typeof(TextBlock));
            center.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            center.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            center.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            center.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(0)));
            center.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));

            var centerCell = new Style(typeof(DataGridCell));
            centerCell.Setters.Add(new Setter(DataGridCell.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            centerCell.Setters.Add(new Setter(DataGridCell.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            centerCell.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(0)));

            columns.Add(new DataGridTextColumn
            {
                Header = "ID",
                Binding = new Binding("EmployeeId"),
                Width = 100,
                MinWidth = 80,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell
            });

            // 名前列（広く）
            columns.Add(new DataGridTextColumn
            {
                Header = "名前",
                Binding = new Binding("EmployeeName"),
                Width = 180,
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell
            });


            // --- 日付列（TextBlock表示 / 編集時ComboBox）---
            var firstDay = new DateTime(month.Year, month.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);

            for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
            {
                var key = d.ToString("yyyy-MM-dd");

                var col = new DataGridTemplateColumn
                {
                    Header = d.Day.ToString(),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    MinWidth = 28,
                    CellStyle = centerCell
                };

                // 表示用: シンボルをそのまま表示（中央寄せ）
                {
                    var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
                    // セルの値とバインド
                    var b = new Binding($"Shifts[{key}]") { TargetNullValue = "", Mode = BindingMode.OneWay };
                    tbFactory.SetBinding(TextBlock.TextProperty, b);
                    tbFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                    col.CellTemplate = new DataTemplate { VisualTree = tbFactory };
                }

                // 編集用: DBマスタ(ShiftTypes)からプルダウン
                {
                    var cbFactory = new FrameworkElementFactory(typeof(ComboBox));

                    // DataContext.ShiftTypes を ItemsSource に（DataGridのDataContext = MainViewModel）
                    cbFactory.SetBinding(
                        ComboBox.ItemsSourceProperty,
                        new Binding("DataContext.ShiftTypes")
                        {
                            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
                        });

                    // 表示は Symbol、選択値も Symbol（現状 Shifts[...] が string シンボルのため）
                    cbFactory.SetValue(ComboBox.DisplayMemberPathProperty, "Symbol");
                    cbFactory.SetValue(ComboBox.SelectedValuePathProperty, "Symbol");

                    // セルの値と双方向バインド
                    cbFactory.SetBinding(
                        ComboBox.SelectedValueProperty,
                        new Binding($"Shifts[{key}]")
                        {
                            Mode = BindingMode.TwoWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                        });

                    cbFactory.SetValue(ComboBox.IsEditableProperty, false);
                    cbFactory.SetValue(ComboBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Center);

                    col.CellEditingTemplate = new DataTemplate { VisualTree = cbFactory };
                }

                columns.Add(col);
            }

            return columns;

        }
    }
}
