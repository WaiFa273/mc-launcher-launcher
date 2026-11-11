using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LauncherLauncher.Models;
using LauncherLauncher.Services;
using LauncherLauncher.Views.Dialogs;

namespace LauncherLauncher.Views
{
    public partial class HomePage : UserControl, IRefreshable
    {
        private readonly ObservableCollection<LauncherEntry> _view = new ObservableCollection<LauncherEntry>();
        private LauncherEntry _selected;
        private bool _suppressSort;
        private bool _ready;

        public HomePage()
        {
            InitializeComponent();
            List.ItemsSource = _view;
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            _ready = true;
        }

        private MainWindow Wnd
        {
            get { return Window.GetWindow(this) as MainWindow; }
        }

        // ── 数据 ────────────────────────────────────────────────────

        public void Refresh()
        {
            var settings = AppState.Db.Settings;

            _suppressSort = true;
            SortRecent.IsChecked = settings.SortMode == 0;
            SortName.IsChecked = settings.SortMode == 1;
            SortCount.IsChecked = settings.SortMode == 2;
            _suppressSort = false;

            List.Tag = settings.ShowPathInList;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (!_ready) return;

            var query = (SearchBox.Text ?? "").Trim();
            IEnumerable<LauncherEntry> items = AppState.Db.Launchers ?? new List<LauncherEntry>();

            if (query.Length > 0)
            {
                items = items.Where(l => Matches(l, query));
            }

            switch (AppState.Db.Settings.SortMode)
            {
                case 1:
                    items = items.OrderBy(l => l.DisplayName, StringComparer.CurrentCulture);
                    break;
                case 2:
                    items = items.OrderByDescending(l => l.LaunchCount).ThenByDescending(l => l.LastLaunchUtcTicks);
                    break;
                default:
                    items = items.OrderByDescending(l => l.LastLaunchUtcTicks).ThenByDescending(l => l.AddedUtcTicks);
                    break;
            }

            var list = items.ToList();
            var previousId = _selected == null ? null : _selected.Id;

            foreach (var entry in AppState.Db.Launchers) entry.IsSelected = false;
            _selected = null;

            _view.Clear();
            foreach (var entry in list)
            {
                if (previousId != null && entry.Id == previousId)
                {
                    entry.IsSelected = true;
                    _selected = entry;
                }
                _view.Add(entry);
            }

            UpdateHeader();
            UpdateEmptyState(query);

            foreach (var entry in list)
            {
                if (entry.IconSource == null && entry.Exists) LoadIcon(entry);
            }
        }

        private static bool Matches(LauncherEntry entry, string query)
        {
            return Contains(entry.DisplayName, query)
                   || Contains(entry.Path, query)
                   || Contains(entry.TagsRaw, query)
                   || Contains(entry.Notes, query);
        }

        private static bool Contains(string text, string query)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LoadIcon(LauncherEntry entry)
        {
            var path = entry.Path;
            Task.Run(() =>
            {
                var icon = IconLoader.Get(path);
                if (icon == null) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { entry.IconSource = icon; } catch { }
                }), DispatcherPriority.Background);
            });
        }

        private void UpdateHeader()
        {
            int total = AppState.Count;
            int alive = AppState.AliveCount;

            CountChip.Text = total + " 个";

            if (total == 0)
            {
                SubtitleText.Text = "还没有启动器。这个软件此刻正在怀疑自己的人生。";
            }
            else if (total == 1)
            {
                SubtitleText.Text = "只有一个启动器。你很专一，或者你刚下载这个软件。";
            }
            else if (alive == total)
            {
                SubtitleText.Text = "共 " + total + " 个启动器，全部健在。双击卡片也能启动。";
            }
            else
            {
                SubtitleText.Text = "共 " + total + " 个启动器，其中 " + alive + " 个文件还在，" + (total - alive) + " 个已经跑路了。";
            }
        }

        private void UpdateEmptyState(string query)
        {
            var empty = _view.Count == 0;
            EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            ListScroll.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

            if (!empty) return;

            if (AppState.Count == 0)
            {
                EmptyTitle.Text = "一个启动器都没有";
                EmptyText.Text = "没有启动器，就没有启动器可以启动。这是个哲学问题。\n先扫描一下本机，它会自己老实交代。";
            }
            else if (query.Length > 0)
            {
                EmptyTitle.Text = "没有找到匹配的启动器";
                EmptyText.Text = "「" + query + "」这个名字听起来很耳熟，但在你的收藏里确实不存在。";
            }
            else
            {
                EmptyTitle.Text = "列表是空的";
                EmptyText.Text = "数据好像出了点问题，不过这不影响你继续收藏启动器。";
            }
        }

        // ── 交互 ────────────────────────────────────────────────────

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchHint == null) return;
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ApplyFilter();
        }

        private void Sort_Checked(object sender, RoutedEventArgs e)
        {
            if (!_ready || _suppressSort || AppState.Db == null) return;

            if (sender == SortName) AppState.Db.Settings.SortMode = 1;
            else if (sender == SortCount) AppState.Db.Settings.SortMode = 2;
            else AppState.Db.Settings.SortMode = 0;

            AppState.RaiseChanged(true);
            ApplyFilter();
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            var entry = element.DataContext as LauncherEntry;
            if (entry == null) return;
            if (IsInsideButton(e.OriginalSource)) return;

            Select(entry);

            if (e.ClickCount == 2 && AppState.Db.Settings.DoubleClickToLaunch)
            {
                Launch(entry, false);
                e.Handled = true;
            }
        }

        private void Card_RightUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            var entry = element.DataContext as LauncherEntry;
            if (entry == null) return;

            Select(entry);
            ShowMenu(entry, element);
            e.Handled = true;
        }

        private void Select(LauncherEntry entry)
        {
            if (_selected == entry) return;
            if (_selected != null) _selected.IsSelected = false;
            _selected = entry;
            if (entry != null) entry.IsSelected = true;
        }

        private static bool IsInsideButton(object source)
        {
            var current = source as DependencyObject;
            while (current != null)
            {
                if (current is ButtonBase) return true;
                current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
            }
            return false;
        }

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            var entry = (sender as FrameworkElement).DataContext as LauncherEntry;
            if (entry != null) Launch(entry, false);
        }

        private void More_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var entry = element.DataContext as LauncherEntry;
            if (entry != null) ShowMenu(entry, element);
        }

        public void Launch(LauncherEntry entry, bool asAdmin)
        {
            if (entry == null) return;

            var settings = AppState.Db.Settings;
            var result = LauncherRunner.Start(entry, settings, asAdmin ? (bool?)true : null);

            if (result.Success)
            {
                entry.MarkLaunched();
                AppState.TouchRecent(entry.Id);
                AppState.RaiseChanged(true);
                Refresh();

                if (Wnd != null) Wnd.Toast("已启动：" + entry.DisplayName, "\uE768");
                if (settings.MinimizeAfterLaunch && Wnd != null) Wnd.WindowState = WindowState.Minimized;
                return;
            }

            if (Wnd == null) return;

            if (entry.Missing)
            {
                Wnd.Choose("打不开这个启动器", result.Message, "重新定位", "从列表移除", "算了", choice =>
                {
                    if (choice == DialogChoice.Primary) OpenEditor(entry);
                    else if (choice == DialogChoice.Secondary) Delete(entry);
                }, true);
            }
            else
            {
                Wnd.Confirm("启动失败", result.Message, "好", null, null, true);
            }
        }

        private void ShowMenu(LauncherEntry entry, UIElement target)
        {
            var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };

            menu.Items.Add(MenuItemFor("启动", "\uE768", () => Launch(entry, false)));
            menu.Items.Add(MenuItemFor("以管理员身份启动", "\uE7EF", () => Launch(entry, true)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor("打开所在文件夹", "\uE838", () => Reveal(entry)));
            menu.Items.Add(MenuItemFor("编辑…", "\uE70F", () => OpenEditor(entry)));
            menu.Items.Add(MenuItemFor("复制路径", "\uE8C8", () => CopyPath(entry)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor("从列表中移除", "\uE74D", () => Delete(entry), true));

            menu.IsOpen = true;
        }

        private MenuItem MenuItemFor(string header, string glyph, Action action, bool danger = false)
        {
            var item = new MenuItem { Header = header };
            item.Icon = new TextBlock
            {
                Text = glyph,
                FontFamily = (FontFamily)FindResource("Font.Icons"),
                FontSize = 12,
                Foreground = danger ? (Brush)FindResource("Brush.Danger") : (Brush)FindResource("Brush.TextSecondary")
            };
            if (danger) item.Foreground = (Brush)FindResource("Brush.Danger");
            item.Click += (s, e) => action();
            return item;
        }

        private void Reveal(LauncherEntry entry)
        {
            if (entry == null || !entry.Exists) return;
            LauncherRunner.RevealInExplorer(entry.Path);
        }

        private void CopyPath(LauncherEntry entry)
        {
            if (entry == null) return;
            try
            {
                Clipboard.SetText(entry.Path ?? "");
                if (Wnd != null) Wnd.Toast("路径已复制。虽然你大概不会用到它。", "\uE8C8");
            }
            catch { }
        }

        public void AddNew()
        {
            OpenEditor(null);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            AddNew();
        }

        public void OpenEditor(LauncherEntry entry)
        {
            if (Wnd == null) return;

            var editor = new EntryEditor(entry);
            var isNew = entry == null;

            editor.Saved += saved =>
            {
                if (isNew) AppState.Db.Launchers.Add(saved);
                AppState.RaiseChanged(true);
                Wnd.CloseDialog();
                Refresh();
                Wnd.Toast(isNew ? "收下了：" + saved.DisplayName : "改好了：" + saved.DisplayName, "\uE73E");
            };

            editor.Cancelled += () => Wnd.CloseDialog();

            Wnd.ShowDialog(editor);
        }

        private void Delete(LauncherEntry entry)
        {
            if (Wnd == null || entry == null) return;

            Wnd.Confirm("移除这个启动器？",
                "「" + entry.DisplayName + "」会从列表里消失，但文件还在硬盘上。\n要不要顺便删掉文件，由你自己决定。",
                "移除", "留着", ok =>
                {
                    if (!ok) return;
                    AppState.Db.Launchers.Remove(entry);
                    AppState.RaiseChanged(true);
                    Refresh();
                    Wnd.Toast("已移除：" + entry.DisplayName, "\uE74D");
                }, true);
        }

        // ── 扫描 ────────────────────────────────────────────────────

        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            StartScan();
        }

        public void StartScan()
        {
            if (Wnd == null) return;

            var dialog = new ScanDialog();
            dialog.Finished += found =>
            {
                Wnd.CloseDialog();
                if (found == null || found.Count == 0) return;

                int added = 0;
                foreach (var item in found)
                {
                    if (AppState.Db.IsKnownPath(item.Path)) continue;
                    AppState.Db.Launchers.Add(new LauncherEntry
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = item.SuggestedName,
                        Path = item.Path,
                        Notes = "扫描发现：" + item.Reason,
                        AddedUtcTicks = DateTime.UtcNow.Ticks
                    });
                    added++;
                }

                if (added > 0)
                {
                    AppState.RaiseChanged(true);
                    Refresh();
                    Wnd.Toast("收录了 " + added + " 个启动器。它们现在归你管了。", "\uE73E");
                }
            };
            dialog.Cancelled += () => Wnd.CloseDialog();

            Wnd.ShowDialog(dialog);
        }

        // ── 一键启动全部 ────────────────────────────────────────────

        private void LaunchAll_Click(object sender, RoutedEventArgs e)
        {
            LaunchAll();
        }

        // ── 千万别点 ────────────────────────────────────────────────

        private void Nest_Click(object sender, RoutedEventArgs e)
        {
            if (Wnd == null) return;

            if (Nesting.Depth >= Nesting.MaxDepth)
            {
                Wnd.Toast("已经套到底了。再点就真的要 StackOverflow 了。", "\uE7BA");
                return;
            }

            Wnd.Choose(
                "千万别点",
                "点了之后，启动器启动器 会启动一个 启动器启动器。\n" +
                "那个 启动器启动器 又会启动一个 启动器启动器。\n\n" +
                "最多 " + (Nesting.MaxDepth + 1) + " 层，然后它自己会打住。\n" +
                "数据不会坏，但你的任务栏会。",
                "我就要点",
                "算了，我怕",
                "",
                choice =>
                {
                    if (choice != DialogChoice.Primary) return;
                    if (Nesting.Spawn())
                    {
                        Wnd.Toast("启动器启动器 启动了 启动器启动器（第 " + (Nesting.Depth + 2) + " 层）", "\uE7BA");
                    }
                    else
                    {
                        Wnd.Toast("它没能启动自己。也许是它也不想。", "\uE7BA");
                    }
                },
                true);
        }

        public void LaunchAll()
        {
            if (Wnd == null) return;

            if (AppState.Count == 0)
            {
                Wnd.Toast("没有启动器可以启动。这就是本软件的日常困境。", "\uE7BA");
                return;
            }

            var live = AppState.Db.Launchers.Where(l => l.Exists).ToList();
            if (live.Count == 0)
            {
                Wnd.Toast("所有启动器都失联了。列表还在，人没了。", "\uE7BA");
                return;
            }

            var settings = AppState.Db.Settings;
            if (settings.SkipConfirmations)
            {
                RunAll(live);
                return;
            }

            Wnd.Choose("一次性启动全部启动器？",
                "即将启动 " + live.Count + " 个启动器。它们会争先恐后地出现在你的屏幕上，互相不知道对方的存在。",
                "启动吧", null, "算了", choice =>
                {
                    if (choice == DialogChoice.Primary) RunAll(live);
                }, true);
        }

        private void RunAll(List<LauncherEntry> live)
        {
            if (Wnd == null) return;

            var settings = AppState.Db.Settings;
            var dialog = new ProgressRunDialog(live, "全部启动", settings.LaunchAllSimultaneous, settings.LaunchDelayMs);
            dialog.Finished += () =>
            {
                AppState.RaiseChanged(true);
                Wnd.CloseDialog();
                Refresh();
            };
            Wnd.ShowDialog(dialog, false);
        }

        public void FocusSearch()
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }
}
