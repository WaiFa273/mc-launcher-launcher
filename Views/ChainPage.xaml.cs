using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LauncherLauncher.Models;
using LauncherLauncher.Services;
using LauncherLauncher.Views.Dialogs;

namespace LauncherLauncher.Views
{
    public partial class ChainPage : UserControl, IRefreshable
    {
        private readonly ObservableCollection<ChainRow> _rows = new ObservableCollection<ChainRow>();
        private bool _running;
        private bool _stop;

        public ChainPage()
        {
            InitializeComponent();
            ChainList.ItemsSource = _rows;
            Loaded += (s, e) => Refresh();
        }

        private MainWindow Wnd
        {
            get { return Window.GetWindow(this) as MainWindow; }
        }

        public void Refresh()
        {
            _rows.Clear();
            foreach (var chain in AppState.Db.Chains) _rows.Add(new ChainRow(chain));

            bool empty = _rows.Count == 0;
            EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

            int launchers = AppState.Db.Chains.SelectMany(c => c.Steps).Select(s => s.LauncherId).Distinct().Count();
            SubtitleText.Text = empty
                ? "让一个启动器启动另一个启动器，直到你忘了最初想玩什么。"
                : AppState.Db.Chains.Count + " 条接力，用到了 " + launchers + " 个启动器。它们大概不知道自己在参与什么。";

            if (Wnd != null) Wnd.UpdateStatus();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.Db.Launchers.Count == 0)
            {
                if (Wnd != null) Wnd.Toast("先往列表里加几个启动器，才有东西可以接力。", "\uE7BA");
                return;
            }

            var chain = new LaunchChain
            {
                Name = "接力 " + (AppState.Db.Chains.Count + 1),
                Mode = LaunchChain.ModeWaitExit,
                DelayMs = 900,
                Steps = new List<ChainStep>()
            };

            OpenEditor(chain, true);
        }

        private void OpenEditor(LaunchChain chain, bool isNew)
        {
            if (Wnd == null) return;

            var editor = new ChainEditor(chain, isNew);
            editor.Saved += saved =>
            {
                if (isNew) AppState.Db.Chains.Add(saved);
                AppState.RaiseChanged(true);
                Wnd.CloseDialog();
                Refresh();
                Wnd.Toast(isNew ? "接力排好了。" : "接力改好了。");
            };
            editor.Cancelled += () => Wnd.CloseDialog();
            Wnd.ShowDialog(editor);
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) == null ? null : (sender as FrameworkElement).Tag as ChainRow;
            if (row == null) return;
            OpenEditor(row.Chain, false);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) == null ? null : (sender as FrameworkElement).Tag as ChainRow;
            if (row == null || Wnd == null) return;

            Wnd.Confirm("删掉这条接力？", "「" + row.Chain.Name + "」会被移除，但里面那些启动器本身不会受影响。它们还活着。",
                "删掉", "留着", ok =>
                {
                    if (!ok) return;
                    AppState.Db.Chains.Remove(row.Chain);
                    AppState.RaiseChanged(true);
                    Refresh();
                    Wnd.Toast("接力删了。启动器们松了一口气。");
                }, true);
        }

        private async void Run_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) == null ? null : (sender as FrameworkElement).Tag as ChainRow;
            if (row == null) return;
            await RunChain(row.Chain);
        }

        private async Task RunChain(LaunchChain chain)
        {
            if (_running || Wnd == null) return;

            var live = chain.Steps
                .Select(s => AppState.Db.Find(s.LauncherId))
                .Where(l => l != null && l.Exists)
                .ToList();

            if (live.Count == 0)
            {
                Wnd.Toast("这条接力里的启动器一个都找不到了。接力棒掉在地上。", "\uE7BA");
                return;
            }

            _running = true;
            _stop = false;
            RunningBanner.Visibility = Visibility.Visible;
            StopBtn.IsEnabled = true;
            StopBtn.Content = "停止接力";
            RunningTitle.Text = "接力进行中：" + chain.Name;

            int ok = 0;
            int fail = 0;

            for (int i = 0; i < live.Count; i++)
            {
                if (_stop) break;

                var entry = live[i];
                RunningDetail.Text = "第 " + (i + 1) + " / " + live.Count + " 棒：" + entry.DisplayName;

                var outcome = LauncherRunner.Start(entry, AppState.Db.Settings);
                if (outcome.Success)
                {
                    entry.MarkLaunched();
                    AppState.TouchRecent(entry.Id);
                    ok++;

                    if (chain.Mode == LaunchChain.ModeWaitExit && outcome.Process != null)
                    {
                        RunningDetail.Text = "第 " + (i + 1) + " / " + live.Count + " 棒：" + entry.DisplayName + " 已经起来了，等它退出…";
                        while (!_stop)
                        {
                            await Task.Delay(400);
                            bool exited;
                            try { exited = outcome.Process.HasExited; }
                            catch { exited = true; }
                            if (exited) break;
                        }
                    }
                    else if (chain.Mode == LaunchChain.ModeDelay)
                    {
                        await Task.Delay(Math.Max(0, chain.DelayMs));
                    }
                    else
                    {
                        await Task.Delay(120);
                    }
                }
                else
                {
                    fail++;
                    if (Wnd != null) Wnd.Toast("「" + entry.DisplayName + "」没起来：" + outcome.Message, "\uE7BA");
                    await Task.Delay(500);
                }
            }

            _running = false;
            RunningBanner.Visibility = Visibility.Collapsed;
            AppState.RaiseChanged(true);
            Refresh();

            if (Wnd == null) return;

            if (_stop)
            {
                Wnd.Toast("接力停了。已经起来的启动器还开着。");
            }
            else if (fail == 0)
            {
                Wnd.Toast("接力跑完 " + ok + " 棒。它们始终不知道自己被安排了。");
            }
            else
            {
                Wnd.Toast("接力跑完：" + ok + " 成功，" + fail + " 失败。", "\uE7BA");
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _stop = true;
            StopBtn.IsEnabled = false;
            StopBtn.Content = "正在停…";
            RunningDetail.Text = "正在停下。已经启动的不会自己关。";
        }
    }

    public class ChainRow
    {
        public ChainRow(LaunchChain chain)
        {
            Chain = chain;
        }

        public LaunchChain Chain { get; private set; }

        public string Name { get { return Chain.Name; } }

        public string ModeText
        {
            get
            {
                if (Chain.Mode == LaunchChain.ModeSimultaneous) return "同时启动";
                if (Chain.Mode == LaunchChain.ModeDelay) return "间隔 " + Chain.DelayMs + " 毫秒";
                return "等上一个退出";
            }
        }

        public string CountText
        {
            get { return Chain.Steps.Count + " 个启动器"; }
        }

        public string StepsText
        {
            get
            {
                if (Chain.Steps.Count == 0) return "这条接力是空的。";

                var names = Chain.Steps
                    .Select(s =>
                    {
                        var entry = AppState.Db.Find(s.LauncherId);
                        return entry != null ? entry.DisplayName : "（已失联）";
                    })
                    .ToList();

                return string.Join("  →  ", names);
            }
        }
    }
}
