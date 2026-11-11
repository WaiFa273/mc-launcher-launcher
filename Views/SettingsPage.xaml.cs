using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LauncherLauncher.Models;
using LauncherLauncher.Services;

namespace LauncherLauncher.Views
{
    public partial class SettingsPage : UserControl, IRefreshable
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "LauncherLauncher";

        private bool _loading;

        public SettingsPage()
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
            var s = AppState.Db.Settings;

            _loading = true;

            AutoScanCheck.IsChecked = s.AutoScanOnStartup;
            DoubleClickCheck.IsChecked = s.DoubleClickToLaunch;
            ShowPathCheck.IsChecked = s.ShowPathInList;
            SkipConfirmCheck.IsChecked = s.SkipConfirmations;
            SimCheck.IsChecked = s.LaunchAllSimultaneous;
            MinimizeAfterCheck.IsChecked = s.MinimizeAfterLaunch;
            TrayCheck.IsChecked = s.MinimizeToTray;
            AutoStartCheck.IsChecked = s.AutoStartWithWindows;
            DelayBox.Text = s.LaunchDelayMs.ToString();
            JavaBox.Text = s.JavaPath ?? "";

            _loading = false;

            VersionChip.Text = "套娃版 " + AppState.VersionText;
            DataBox.Text = Store.DataDirectory;

            string size = "";
            try
            {
                if (File.Exists(Store.DataFile))
                {
                    var info = new FileInfo(Store.DataFile);
                    size = "，配置文件 " + Util.Format.HumanSize(info.Length);
                }
            }
            catch
            {
            }

            DataHint.Text = (Store.IsPortable ? "便携模式：数据就在程序旁边" : "数据存放在漫游目录")
                            + "，共 " + AppState.Db.Launchers.Count + " 个启动器" + size + "。";

            JavaHint.Text = string.IsNullOrEmpty(s.JavaPath)
                ? "留空时会在 JAVA_HOME、PATH、.minecraft\\runtime 和常见安装目录里自己找。找不到也不会怪你。"
                : "已指定：" + s.JavaPath;
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            var s = AppState.Db.Settings;
            s.AutoScanOnStartup = AutoScanCheck.IsChecked == true;
            s.DoubleClickToLaunch = DoubleClickCheck.IsChecked == true;
            s.ShowPathInList = ShowPathCheck.IsChecked == true;
            s.SkipConfirmations = SkipConfirmCheck.IsChecked == true;
            s.LaunchAllSimultaneous = SimCheck.IsChecked == true;
            s.MinimizeAfterLaunch = MinimizeAfterCheck.IsChecked == true;
            s.MinimizeToTray = TrayCheck.IsChecked == true;
            s.AutoStartWithWindows = AutoStartCheck.IsChecked == true;

            ApplyAutoStart(s.AutoStartWithWindows);

            Commit(false);
        }

        private void Delay_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;

            int value;
            if (!int.TryParse((DelayBox.Text ?? "").Trim(), out value) || value < 0 || value > 600000)
            {
                return;
            }

            AppState.Db.Settings.LaunchDelayMs = value;
            Commit(false);
        }

        private void Commit(bool toast)
        {
            AppState.RaiseChanged(true);
            if (Wnd != null) Wnd.UpdateStatus();
            if (toast && Wnd != null) Wnd.Toast("设置已保存。");
        }

        private static void ApplyAutoStart(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;

                    if (enabled)
                    {
                        string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                        key.SetValue(RunValue, "\"" + exe + "\"");
                    }
                    else
                    {
                        if (key.GetValue(RunValue) != null) key.DeleteValue(RunValue, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Store.TryLog("autostart: " + ex);
            }
        }

        private void BrowseJava_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "挑一个 Java",
                Filter = "Java 可执行文件|javaw.exe;java.exe|所有程序|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true) return;

            JavaBox.Text = dialog.FileName;
            AppState.Db.Settings.JavaPath = dialog.FileName;
            Commit(false);
            if (Wnd != null) Wnd.Toast("Java 记下了。它现在有编制了。");
        }

        private void DetectJava_Click(object sender, RoutedEventArgs e)
        {
            string java = null;
            try
            {
                java = LauncherRunner.FindJava(null);
            }
            catch (Exception ex)
            {
                Store.TryLog("findjava: " + ex);
            }

            if (string.IsNullOrEmpty(java))
            {
                if (Wnd != null) Wnd.Toast("没找到 Java。要么真没装，要么装在了非常有个性的地方。", "\uE7BA");
                return;
            }

            JavaBox.Text = java;
            AppState.Db.Settings.JavaPath = java;
            Commit(false);
            if (Wnd != null) Wnd.Toast("找到了：" + Util.Format.Elide(java, 48));
        }

        private void OpenData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", Store.DataDirectory);
            }
            catch (Exception ex)
            {
                Store.TryLog("open data: " + ex);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "导出配置副本",
                FileName = "launcher-launcher-backup.json",
                Filter = "JSON 配置|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                AppState.Save();
                File.Copy(Store.DataFile, dialog.FileName, true);
                if (Wnd != null) Wnd.Toast("导出了一份。备份是成年人的浪漫。");
            }
            catch (Exception ex)
            {
                Store.TryLog("export: " + ex);
                if (Wnd != null) Wnd.Toast("导出失败：" + ex.Message, "\uE7BA");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.Db.Launchers.Count == 0)
            {
                if (Wnd != null) Wnd.Toast("列表本来就是空的，清不动。");
                return;
            }

            if (Wnd == null) return;

            Wnd.Confirm("清空启动器列表？",
                "会删掉 " + AppState.Db.Launchers.Count + " 条记录和所有接力。文件本身不动，只是这场收藏到此为止。",
                "清空", "算了", ok =>
                {
                    if (!ok) return;

                    AppState.Db.Launchers.Clear();
                    AppState.Db.Chains.Clear();
                    AppState.Db.RecentIds.Clear();
                    AppState.Db.EnsureChain();
                    AppState.RaiseChanged(true);
                    Refresh();
                    if (Wnd != null)
                    {
                        Wnd.Toast("都清空了。你自由了，暂时。");
                        Wnd.RefreshCurrentPage();
                    }
                }, true);
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            if (Wnd == null) return;

            Wnd.Confirm("恢复默认设置？", "只重置设置，启动器记录不会动。你的收藏还在，只是规矩回到了出厂状态。",
                "恢复默认", "算了", ok =>
                {
                    if (!ok) return;

                    var fresh = new AppSettings();
                    fresh.JavaPath = AppState.Db.Settings.JavaPath;
                    AppState.Db.Settings = fresh;
                    ApplyAutoStart(fresh.AutoStartWithWindows);
                    AppState.RaiseChanged(true);
                    Refresh();
                    if (Wnd != null) Wnd.Toast("设置回到默认了。");
                });
        }
    }
}
