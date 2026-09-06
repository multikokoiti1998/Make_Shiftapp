using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace Shiftapp_demo.Views
{
    // 手動で「●」（代休）を入力した際、管理者にどの当直/日勤に紐づけるかを選ばせるダイアログ
    public partial class CompOffDutyPickerDialog : Window
    {
        private sealed class CandidateItem
        {
            public DateTime DutyDate { get; init; }
            public string Label { get; init; } = "";
        }

        public DateTime? SelectedDutyDate { get; private set; }

        public CompOffDutyPickerDialog(List<(DateTime DutyDate, string Symbol)> candidates)
        {
            InitializeComponent();

            var items = candidates
                .Select(c => new CandidateItem
                {
                    DutyDate = c.DutyDate,
                    Label = $"{c.DutyDate.Month}/{c.DutyDate.Day}" +
                            $"({c.DutyDate.ToString("ddd", CultureInfo.GetCultureInfo("ja-JP"))}) " +
                            (c.Symbol == "当" ? "当直" : "日勤"),
                })
                .ToList();

            CandidateListBox.ItemsSource = items;
            if (items.Count > 0)
                CandidateListBox.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e) => Confirm();

        private void CandidateListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Confirm();

        private void Confirm()
        {
            if (CandidateListBox.SelectedItem is CandidateItem item)
            {
                SelectedDutyDate = item.DutyDate;
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedDutyDate = null;
            DialogResult = false;
        }
    }
}
