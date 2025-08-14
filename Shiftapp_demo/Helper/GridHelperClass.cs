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

            // ID列（広く）
            columns.Add(new DataGridTextColumn
            {
                Header = "ID",
                Binding = new Binding("EmployeeId"),
                Width = 100, // ←広く
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
                Width = 180, // ←広く
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell
            });

            // 日付列（1日〜末日）
            DateTime firstDay = new DateTime(month.Year, month.Month, 1);
            DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);

            for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
            {
                var b = new Binding($"Shifts[{d:yyyy-MM-dd}]") { TargetNullValue = "" };

                columns.Add(new DataGridTextColumn
                {
                    Header = d.Day.ToString(),
                    Binding = b,
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    MinWidth = 28,
                    ElementStyle = center,
                    EditingElementStyle = center
                });
            }


            return columns;
        }
    }
}
