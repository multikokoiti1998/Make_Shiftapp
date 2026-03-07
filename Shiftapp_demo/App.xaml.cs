using Serilog;
using Shiftapp_demo.Utils;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

namespace Shiftapp_demo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            App_Logger.Initialize();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            Log.Information("Application started");

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application exited");
            App_Logger.Shutdown();
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled UI exception occurred");
            MessageBox.Show(
                "予期しないエラーが発生しました。ログを確認してください。",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled non-UI exception occurred");
            }
            else
            {
                Log.Fatal("Unhandled non-UI exception occurred: {ExceptionObject}", e.ExceptionObject);
            }
        }
    }

}
