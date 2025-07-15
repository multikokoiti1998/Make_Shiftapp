using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Shiftapp_demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click1(object sender, RoutedEventArgs e)
        {
            // 1. AdminWindowのインスタンス（実体）を作成します
            AdminWindow adminWindow = new AdminWindow();

            // 2. 作成したウィンドウを表示します
            adminWindow.Show();
        }
    }
}