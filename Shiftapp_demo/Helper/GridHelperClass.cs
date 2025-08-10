using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            // ID列
            columns.Add(new DataGridTextColumn
            {
                Header = "ID",
                Binding = new Binding("EmployeeId"),
                Width = DataGridLength.Auto
            });

            // 名前列
            columns.Add(new DataGridTextColumn
            {
                Header = "名前",
                Binding = new Binding("EmployeeName"),
                Width = DataGridLength.Auto
            });

            // 日付列（1日〜末日）
            DateTime firstDay = new DateTime(month.Year, month.Month, 1);
            DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);

            for (DateTime d = firstDay; d <= lastDay; d = d.AddDays(1))
            {
                var col = new DataGridTemplateColumn();
                col.Header = d.Day.ToString();

                //var factory = new FrameworkElementFactory(typeof(TextBlock));
                //factory.SetBinding(TextBlock.TextProperty, new Binding($"{d.Date}"));

                //factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                //col.CellTemplate = new DataTemplate { VisualTree = factory };
                //col.Width = new DataGridLength(30);

                columns.Add(col);
            }


            return columns;
        }
    }
}
