using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace Shiftapp_demo.Helper
{
    class AdminGridHelperClass
    {
        public static ObservableCollection<DataGridColumn>GenerateColumnsForAdminEmployee
            (IEnumerable<OptionItem> saturdayClassOptions,
             IEnumerable<OptionItem>  RoleClassOptions)
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
                Binding = new Binding("EmployeeId")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit
                },
                Width = 100,
                MinWidth = 80,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridTextColumn
            {
                Header = "名前",
                Binding = new Binding("EmployeeName")
                { 
                    Mode = BindingMode.TwoWay, 
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit
                },
                Width = 180,
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridCheckBoxColumn
            {
                Header = "当直カテーテル対応可能",
                Binding = new Binding("CanDoCatheterization")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit
                },
                Width = 180,
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridComboBoxColumn
            {
                Header = "土曜日班",
                ItemsSource = saturdayClassOptions,
                DisplayMemberPath = "Label",
                SelectedValuePath = "Code",
                Width = 180,
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridCheckBoxColumn
            {
                Header = "当直対応可能",
                Binding = new Binding("CanDoNightDuty")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit
                },
                Width = 180,
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridCheckBoxColumn
            {
                Header = "日勤対応可能",
                Binding = new Binding("CanDayDuty")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit
                },
                Width = 180,
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridComboBoxColumn
            {
                Header = "月最大当直回数",
                ItemsSource = new List<int> { 0, 1, 2, 3, 4 },
                Width = 180,
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridComboBoxColumn
            {
                Header = "役職",
                ItemsSource = RoleClassOptions,
                DisplayMemberPath = "Label",
                SelectedValuePath = "Code",
                Width = 180,
                MinWidth = 120,
                ElementStyle = center,
                EditingElementStyle = center,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            return columns;

        }
    }
}
