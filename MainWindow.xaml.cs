using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LauncherLauncher.Models;
using LauncherLauncher.Services;
using LauncherLauncher.Views;
using LauncherLauncher.Views.Dialogs;
using WinForms = System.Windows.Forms;

namespace LauncherLauncher
{
    public partial class MainWindow : Window
    {
        private static readonly SolidColorBrush ToastBackground = Frozen(Color.FromArgb(0xF2, 0x0B, 0x12, 0x20));

        private readonly Dictionary<string, UserControl> _pages = new Dictionary<string, UserControl>();
        private readonly NotifyIconHolder _tray = new NotifyIconHolder();
        private bool _overlayDismissible = true;
        private bool _reallyClose;
        private string _currentPage = "home";

        public MainWindow()
        {
            InitializeComponent();

            FitToWorkArea();

            TitleIcon.Source = IconLoader.AppIcon;
            Icon = IconLoader.AppIcon;
            VersionChip.Text = "套娃版 " + AppState.VersionText;

            if (Nesting.IsNested) PlaceNested();

            AppState.Changed += OnStateChanged;
            Loaded += OnLoaded;
            Closing += OnClosing;
            StateChanged += OnWindowStateChanged;

            _tray.Create(this);
        }

        // ── 生命周期 ────────────────────────────────────────────────

        /// <summary>窗口比屏幕还高的时候（小屏幕 + 高 DPI），先把自己塞进工作区。</summary>
        private void FitToWorkArea()
        {
            try
            {
                var work = SystemParameters.WorkArea;
                if (work.Width <= 0 || work.Height <= 0) return;

                double maxWidth = Math.Max(720, work.Width - 24);
                double maxHeight = Math.Max(480, work.Height - 24);

                if (Width > maxWidth) Width = maxWidth;
                if (Height > maxHeight) Height = maxHeight;

                MinWidth = Math.Min(MinWidth, maxWidth);
                MinHeight = Math.Min(MinHeight, maxHeight);
            }
            catch { }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Navigate(StartPage());
            UpdateStatus();

            if (Nesting.IsNested)
            {
                AutoNest();
                return;
            }

            var settings = AppState.Db.Settings;
            if (settings.AutoScanOnStartup && AppState.Count == 0)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var home = PageHost.Content as HomePage;
                    if (home != null) home.StartScan();
                }), DispatcherPriority.Background);
            }
        }

        // ── 套娃 ────────────────────────────────────────────────────

        /// <summary>第 N 层把自己往右下挪一点，看起来像一叠窗口。</summary>
        private void PlaceNested()
        {
            try
            {
                var work = SystemParameters.WorkArea;
                WindowStartupLocation = WindowStartupLocation.Manual;

                int depth = Nesting.Depth;
                Width = Math.Max(MinWidth, Width - depth * 16);
                Height = Math.Max(MinHeight, Height - depth * 12);

                Left = work.Left + 24 + depth * 30;
                Top = work.Top + 20 + depth * 26;

                VersionChip.Text = "第 " + (depth + 1) + " 层套娃";
                VersionChip.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Danger");
                VersionChip.Background = (System.Windows.Media.Brush)FindResource("Brush.DangerSoft");
            }
            catch { }
        }

        /// <summary>被自己启动起来的那一层：等一会儿，再启动下一层；实在套不下去了就收尾。</summary>
        private async void AutoNest()
        {
            try
            {
                SetStatus("第 " + (Nesting.Depth + 1) + " 层套娃 · 正在准备再启动一次自己");
                await Task.Delay(700);

                if (Nesting.Depth >= Nesting.MaxDepth)
                {
                    ShowStackOverflow();
                    return;
                }

                if (!Nesting.Spawn())
                {
                    ShowStackOverflow();
                    return;
                }

                Toast("启动器启动器 启动了 启动器启动器（第 " + (Nesting.Depth + 1) + " 层）", "\uE7BA");
                SetStatus("第 " + (Nesting.Depth + 1) + " 层套娃 · 已经把下一层叫起来了");
            }
            catch (Exception ex)
            {
                Store.TryLog("autonest: " + ex);
            }
        }

        /// <summary>最深的那一层：体面地打住。</summary>
        private void ShowStackOverflow()
        {
            SetStatus("StackOverflow · 第 " + (Nesting.Depth + 1) + " 层，到此为止");

            Choose(
                "StackOverflowException",
                "启动器启动器 已经套了 " + (Nesting.Depth + 1) + " 层。\n" +
                "这一层没有再启动下一层，因为再往下就不是玩笑，是内存。\n\n" +
                "（真正的 StackOverflow 会让进程当场消失。这里给你一个体面的终点。）",
                "结束这场闹剧",
                "留着它们",
                "",
                choice =>
                {
                    if (choice == DialogChoice.Primary)
                    {
                        Nesting.CloseAll();
                        ForceQuit();
                    }
                    else if (choice == DialogChoice.Secondary)
                    {
                        ForceQuit();
                    }
                },
                true);
        }

        /// <summary>不管设置里是不是「关到托盘」，这次真的关。</summary>
        public void ForceQuit()
        {
            _reallyClose = true;
            Close();
        }

        /// <summary>调试用：--page=chain / report / settings / about 可以直接落在某一页。</summary>
        private static string StartPage()
        {
            try
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (arg.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
                    {
                        string key = arg.Substring(7).Trim().ToLowerInvariant();
                        if (key == "home" || key == "chain" || key == "report" || key == "settings" || key == "about")
                        {
                            return key;
                        }
                    }
                }
            }
            catch { }
            return "home";
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (!_reallyClose && AppState.Db != null && AppState.Db.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                _tray.Balloon("我还在托盘里。随时准备启动启动器，是一种生活方式。");
                return;
            }

            // 套娃里的那些副本没改过任何数据，十只一起写 data.json 反而容易打架。
            if (!Nesting.IsNested)
            {
                try
                {
                    AppState.Save();
                }
                catch { }
            }

            _tray.Dispose();
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            MaxBtn.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";

            if (WindowState == WindowState.Minimized && AppState.Db != null && AppState.Db.Settings.MinimizeToTray)
            {
                Hide();
            }
        }

        public void Quit()
        {
            _reallyClose = true;
            Close();
        }

        public void RestoreFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
        }

        // ── 导航 ────────────────────────────────────────────────────

        private void Navigate(string key)
        {
            UserControl page;
            if (!_pages.TryGetValue(key, out page))
            {
                switch (key)
                {
                    case "chain": page = new ChainPage(); break;
                    case "report": page = new ReportPage(); break;
                    case "settings": page = new SettingsPage(); break;
                    case "about": page = new AboutPage(); break;
                    default: page = new HomePage(); break;
                }
                _pages[key] = page;
            }

            _currentPage = key;
            PageHost.Content = page;

            var nav = key == "chain" ? NavChain
                : key == "report" ? NavReport
                : key == "settings" ? NavSettings
                : key == "about" ? NavAbout
                : NavHome;
            if (nav != null && nav.IsChecked != true) nav.IsChecked = true;

            var refreshable = page as IRefreshable;
            if (refreshable != null) refreshable.Refresh();

            UpdateStatus();
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (PageHost == null) return;

            string key;
            if (sender == NavChain) key = "chain";
            else if (sender == NavReport) key = "report";
            else if (sender == NavSettings) key = "settings";
            else if (sender == NavAbout) key = "about";
            else key = "home";

            if (key != _currentPage) Navigate(key);
        }

        private void OnStateChanged()
        {
            UpdateStatus();
            var page = PageHost == null ? null : PageHost.Content as IRefreshable;
            if (page != null && _currentPage != "home") page.Refresh();
        }

        public void UpdateStatus()
        {
            if (StatusText == null) return;

            int total = AppState.Count;
            int alive = AppState.AliveCount;
            int used = AppState.UsedCount;

            if (total == 0)
            {
                StatusText.Text = "空 · 一个启动器都没有，本软件目前没有启动对象";
                SidebarStat.Text = "0 个启动器";
                SidebarHint.Text = "还没有启动器";
            }
            else
            {
                StatusText.Text = string.Format("共 {0} 个启动器 · 文件还在 {1} 个 · 真正启动过 {2} 个", total, alive, used);
                SidebarStat.Text = total + " 个启动器";
                SidebarHint.Text = alive == total
                    ? "全部健在，一个都没跑"
                    : (total - alive) + " 个已经跑路了";
            }

            SidebarSize.Text = AppState.VersionText + " · " + AppState.SizeText;
        }

        public void SetStatus(string text)
        {
            if (StatusText != null) StatusText.Text = text;
        }

        public void RefreshCurrentPage()
        {
            var page = PageHost == null ? null : PageHost.Content as IRefreshable;
            if (page != null) page.Refresh();
        }

        // ── 窗口按钮 ────────────────────────────────────────────────

        private void Min_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Max_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DataDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(Store.DataDirectory);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + Store.DataDirectory + "\"");
            }
            catch (Exception ex)
            {
                Toast("打不开目录：" + ex.Message, "\uE7BA");
            }
        }

        // ── 弹层 ────────────────────────────────────────────────────

        public bool IsOverlayOpen
        {
            get { return OverlayLayer.Visibility == Visibility.Visible; }
        }

        public void ShowDialog(UIElement content, bool dismissible = true)
        {
            _overlayDismissible = dismissible;
            OverlayContent.Content = content;
            OverlayLayer.Visibility = Visibility.Visible;
            OverlayLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));

            var element = content as FrameworkElement;
            if (element != null)
            {
                element.Opacity = 0;
                element.RenderTransform = new TranslateTransform(0, 8);
                element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)) { BeginTime = TimeSpan.FromMilliseconds(60) });
                var t = element.RenderTransform as TranslateTransform;
                if (t != null)
                {
                    t.BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(200))
                        {
                            BeginTime = TimeSpan.FromMilliseconds(60),
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                        });
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    element.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                }), DispatcherPriority.Input);
            }
        }

        public void CloseDialog()
        {
            if (!IsOverlayOpen) return;

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(110));
            fade.Completed += (s, e) =>
            {
                OverlayLayer.Visibility = Visibility.Collapsed;
                OverlayContent.Content = null;
                OverlayLayer.Opacity = 0;
            };
            OverlayLayer.BeginAnimation(OpacityProperty, fade);
        }

        private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == OverlayLayer && _overlayDismissible) CloseDialog();
        }

        public void Confirm(string title, string message, string okText, string cancelText, Action<bool> done, bool danger = false)
        {
            var dialog = new MessageDialog(title, message, okText, cancelText, null, danger);
            dialog.Chosen += choice =>
            {
                CloseDialog();
                if (done != null) done(choice == DialogChoice.Primary);
            };
            ShowDialog(dialog);
        }

        public void Choose(string title, string message, string primary, string secondary, string tertiary,
            Action<DialogChoice> done, bool danger = false)
        {
            var dialog = new MessageDialog(title, message, primary, secondary, tertiary, danger);
            dialog.Chosen += choice =>
            {
                CloseDialog();
                if (done != null) done(choice);
            };
            ShowDialog(dialog);
        }

        // ── Toast ───────────────────────────────────────────────────

        public void Toast(string message, string glyph = "\uE73E")
        {
            var icon = new TextBlock
            {
                Text = glyph,
                FontFamily = (FontFamily)FindResource("Font.Icons"),
                FontSize = 12,
                Foreground = Brushes.White,
                Opacity = 0.92,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 9, 0)
            };

            var text = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(icon);
            panel.Children.Add(text);

            var border = new Border
            {
                Background = ToastBackground,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 10, 17, 10),
                Margin = new Thickness(0, 6, 0, 0),
                Child = panel,
                Opacity = 0,
                RenderTransform = new TranslateTransform(0, 10)
            };

            ToastPanel.Children.Add(border);
            while (ToastPanel.Children.Count > 3) ToastPanel.Children.RemoveAt(0);

            border.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            ((TranslateTransform)border.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(190))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2600) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                fade.Completed += (s2, e2) => ToastPanel.Children.Remove(border);
                border.BeginAnimation(OpacityProperty, fade);
            };
            timer.Start();
        }

        // ── 拖拽与快捷键 ────────────────────────────────────────────

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;

            int added = 0;
            int skipped = 0;

            foreach (var file in files)
            {
                if (!File.Exists(file)) { skipped++; continue; }
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".exe" && ext != ".jar" && ext != ".lnk" && ext != ".bat" && ext != ".cmd") { skipped++; continue; }
                if (AppState.Db.IsKnownPath(file)) { skipped++; continue; }

                var entry = new LauncherEntry
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Path = file,
                    AddedUtcTicks = DateTime.UtcNow.Ticks
                };
                AppState.Db.Launchers.Add(entry);
                added++;
            }

            if (added > 0)
            {
                AppState.RaiseChanged(true);
                RefreshCurrentPage();
                Toast(added == 1 ? "收下了 1 个启动器。" : "一股脑收下了 " + added + " 个启动器。", "\uE710");
            }
            else if (skipped > 0)
            {
                Toast("这些东西不能启动，或者已经在列表里了。", "\uE7BA");
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && IsOverlayOpen)
            {
                if (_overlayDismissible) CloseDialog();
                e.Handled = true;
                return;
            }

            if (IsOverlayOpen) return;

            if (e.Key == Key.F5)
            {
                var home = PageHost.Content as HomePage;
                if (home != null) home.StartScan();
                e.Handled = true;
            }
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var home = PageHost.Content as HomePage;
                if (home == null)
                {
                    NavHome.IsChecked = true;
                    home = PageHost.Content as HomePage;
                }
                if (home != null) home.AddNew();
                e.Handled = true;
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var home = PageHost.Content as HomePage;
                if (home != null) { NavHome.IsChecked = true; home.FocusSearch(); }
                e.Handled = true;
            }
        }

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>托盘图标，单独一层是因为 NotifyIcon 来自 WinForms，得自己收拾干净。</summary>
        private sealed class NotifyIconHolder : IDisposable
        {
            private WinForms.NotifyIcon _icon;
            private System.Drawing.Icon _drawingIcon;

            public void Create(MainWindow owner)
            {
                try
                {
                    var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
                    if (resource != null) _drawingIcon = new System.Drawing.Icon(resource.Stream);

                    var menu = new WinForms.ContextMenuStrip();
                    menu.Items.Add("显示主界面", null, (s, e) => owner.RestoreFromTray());
                    menu.Items.Add("一键启动全部", null, (s, e) =>
                    {
                        var home = owner.PageHost.Content as HomePage;
                        if (home == null)
                        {
                            owner.NavHome.IsChecked = true;
                            owner.RestoreFromTray();
                            home = owner.PageHost.Content as HomePage;
                        }
                        if (home != null) home.LaunchAll();
                    });
                    menu.Items.Add(new WinForms.ToolStripSeparator());
                    menu.Items.Add("退出", null, (s, e) => owner.Quit());

                    _icon = new WinForms.NotifyIcon
                    {
                        Icon = _drawingIcon,
                        Text = "启动器启动器",
                        Visible = true,
                        ContextMenuStrip = menu
                    };
                    _icon.DoubleClick += (s, e) => owner.RestoreFromTray();
                }
                catch (Exception ex)
                {
                    Store.TryLog("tray failed: " + ex);
                }
            }

            public void Balloon(string text)
            {
                try
                {
                    if (_icon != null) _icon.ShowBalloonTip(1600, "启动器启动器", text, WinForms.ToolTipIcon.None);
                }
                catch { }
            }

            public void Dispose()
            {
                try
                {
                    if (_icon != null) { _icon.Visible = false; _icon.Dispose(); _icon = null; }
                    if (_drawingIcon != null) { _drawingIcon.Dispose(); _drawingIcon = null; }
                }
                catch { }
            }
        }
    }
}
