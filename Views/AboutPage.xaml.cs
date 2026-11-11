using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LauncherLauncher.Services;

namespace LauncherLauncher.Views
{
    public partial class AboutPage : UserControl, IRefreshable
    {
        public AboutPage()
        {
            InitializeComponent();
            Loaded += (s, e) => Refresh();
        }

        private MainWindow Wnd
        {
            get { return Window.GetWindow(this) as MainWindow; }
        }

        public void Refresh()
        {
            VersionText.Text = AppState.VersionText;

            StatLaunchers.Text = AppState.Db.Launchers.Count.ToString();
            StatLaunches.Text = AppState.Db.Launchers.Count == 0
                ? "0"
                : AppState.Db.Launchers.Sum(l => l.LaunchCount).ToString();

            long size = Store.ExeSize();
            StatSize.Text = size > 0 ? Util.Format.HumanSize(size) : "—";

            StackText.Text = "WPF / .NET Framework 4.8 / 单文件。"
                             + "在 Windows 10 和 11 上不需要额外安装运行时，双击就能开。"
                             + "整个程序压在一个 exe 里，体积 " + (size > 0 ? Util.Format.HumanSize(size) : "很小") + "。";

            if (Wnd != null) Wnd.UpdateStatus();
        }

        private void OpenData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", Store.DataDirectory);
            }
            catch (Exception ex)
            {
                Store.TryLog("about data: " + ex);
            }
        }

        private void CopyVersion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText("启动器启动器 " + AppState.VersionText + " · 一个专门用来启动启动器的启动器");
                if (Wnd != null) Wnd.Toast("版本信息复制好了。");
            }
            catch (Exception ex)
            {
                Store.TryLog("about clipboard: " + ex);
            }
        }
    }
}
