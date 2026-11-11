using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LauncherLauncher.Services;

namespace LauncherLauncher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandled;
            AppDomain.CurrentDomain.UnhandledException += (s, a) =>
            {
                Store.TryLog("fatal: " + a.ExceptionObject);
            };

            try
            {
                AppState.Initialize();
            }
            catch (Exception ex)
            {
                Store.TryLog("init failed: " + ex);
                MessageBox.Show("数据库读不出来了，已改用空数据启动。\n\n" + ex.Message,
                    "启动器启动器", MessageBoxButton.OK, MessageBoxImage.Warning);
                AppState.Reset();
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }

        private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Store.TryLog("ui exception: " + e.Exception);
            MessageBox.Show("出了点小状况（这不重要，重要的是启动器还能启动）：\n\n" + e.Exception.Message,
                "启动器启动器", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }
    }
}
