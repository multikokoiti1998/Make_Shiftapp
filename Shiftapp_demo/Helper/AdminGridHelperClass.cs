using Shiftapp_demo.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace Shiftapp_demo.Helper
{
    class AdminGridHelperClass
    {
        private static Binding b(string path) =>
            new(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit };

        public static ObservableCollection<DataGridColumn> GenerateColumnsForAdminEmployee
            (IEnumerable<OptionItem> saturdayClassOptions,
             IEnumerable<OptionItem> RoleClassOptions)
        {
            var columns = new ObservableCollection<DataGridColumn>();

            // TextBlock用（TextColumn向け）
            var centerText = new Style(typeof(TextBlock));
            centerText.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            centerText.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            centerText.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            centerText.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(0)));
            centerText.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));

            // DataGridCell は Cell の見た目用
            var centerCell = new Style(typeof(DataGridCell));
            centerCell.Setters.Add(new Setter(DataGridCell.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            centerCell.Setters.Add(new Setter(DataGridCell.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            centerCell.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(0)));

            // TextBox 用スタイル
            var centerTextBox = new Style(typeof(TextBox));
            centerTextBox.Setters.Add(new Setter(TextBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            centerTextBox.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            centerTextBox.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(0)));

            // CheckBox 用
            var centerCheck = new Style(typeof(CheckBox));
            centerCheck.Setters.Add(new Setter(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            centerCheck.Setters.Add(new Setter(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center));

            // ComboBox 用（編集時）
            var centerCombo = new Style(typeof(ComboBox));
            centerCombo.Setters.Add(new Setter(ComboBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            centerCombo.Setters.Add(new Setter(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            columns.Add(new DataGridTextColumn
            {
                Header = "ID",
                Binding = b("EmployeeId"),
                Width = 100,
                MinWidth = 80,
                ElementStyle = centerText,
                EditingElementStyle = centerTextBox,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridTextColumn
            {
                Header = "名前",
                Binding = b("EmployeeName"),
                Width = 180,
                MinWidth = 120,
                ElementStyle = centerText,
                EditingElementStyle = centerTextBox,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridCheckBoxColumn
            {
                Header = "当直カテーテル対応可能",
                Binding = b("CanDoCatheterization"),
                Width = 180,
                MinWidth = 120,
                ElementStyle = centerCheck,
                EditingElementStyle = centerCheck,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridComboBoxColumn
            {
                Header = "土曜日班",
                ItemsSource = saturdayClassOptions,
                DisplayMemberPath = "Label",
                SelectedValuePath = "Code",
                SelectedValueBinding = b("SaturdayClass"),
                Width = 180,
                MinWidth = 120,
                ElementStyle = centerCombo,
                EditingElementStyle = centerCombo,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridCheckBoxColumn
            {
                Header = "当直対応可能",
                Binding = b("CanDoNightDuty"),
                Width = 180,
                MinWidth = 120,
                ElementStyle = centerCheck,
                EditingElementStyle = centerCheck,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridCheckBoxColumn
            {
                Header = "日勤対応可能",
                Binding = b("CanDayDuty"),
                Width = 180,
                MinWidth = 120,
                ElementStyle = centerCheck,
                EditingElementStyle = centerCheck,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridComboBoxColumn
            {
                Header = "月最大当直回数",
                ItemsSource = new List<int> { 0, 1, 2, 3, 4 },
                SelectedItemBinding = b("MonthlyDutyLimit"),
                Width = 180,
                MinWidth = 120,
                ElementStyle = centerCombo,
                EditingElementStyle = centerCombo,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            columns.Add(new DataGridComboBoxColumn
            {
                Header = "役職",
                ItemsSource = RoleClassOptions,
                DisplayMemberPath = "Label",
                SelectedValuePath = "Code",
                SelectedValueBinding = b("Role"),
                Width = 180,
                MinWidth = 120,
                ElementStyle = centerCombo,
                EditingElementStyle = centerCombo,
                CellStyle = centerCell,
                IsReadOnly = false
            });

            return columns;

        }
    }
}
