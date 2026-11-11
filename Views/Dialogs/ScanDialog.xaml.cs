using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LauncherLauncher.Services;

namespace LauncherLauncher.Views.Dialogs
{
    public partial class ScanDialog : UserControl
    {
        public event Action<List<FoundLauncher>> Finished;
        public event Action Cancelled;

        private readonly ObservableCollection<ScanRow> _rows = new ObservableCollection<ScanRow>();
        private CancellationTokenSource _cts;
        private bool _running;
        private bool _closedByUser;

        public ScanDialog()
        {
            InitializeComponent();
            ResultList.ItemsSource = _rows;
            _rows.CollectionChanged += (s, e) => UpdateCount();
            Loaded += OnLoaded;
            Unloaded += (s, e) =>
            {
                if (_cts != null) _cts.Cancel();
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            RunScan();
        }

        private async void RunScan()
        {
            ShowPanel(ScanningPanel);
            ProgressText.Text = "正在翻你的硬盘，看看藏了多少启动器…";

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _running = true;

            List<FoundLauncher> found = null;
            try
            {
                var progress = new Progress<string>(p =>
                {
                    if (!_running) return;
                    ProgressText.Text = Util.Format.Elide(p, 74);
                });

                found = await Task.Run(
                    () => Detector.Scan(text => ((IProgress<string>)progress).Report(text), token), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Store.TryLog("scan failed: " + ex);
            }

            _running = false;
            if (_closedByUser) return;

            ShowResults(found ?? new List<FoundLauncher>());
        }

        private void ShowResults(List<FoundLauncher> found)
        {
            var fresh = found.Where(f => !AppState.Db.IsKnownPath(f.Path)).ToList();

            if (fresh.Count == 0)
            {
                ShowPanel(EmptyPanel);
                EmptyText.Text = found.Count > 0
                    ? "扫到了 " + found.Count + " 个，但它们都已经在你的列表里了。你比扫描器还了解你自己。"
                    : "整台机器翻了一遍，一个像样的启动器都没有。这可能是好事，也可能说明你才刚开始收藏。";
                return;
            }

            _rows.Clear();
            foreach (var item in fresh) _rows.Add(new ScanRow(item) { IsChecked = true });

            ShowPanel(ResultPanel);
            ResultTitle.Text = "找到 " + fresh.Count + " 个疑似启动器";
            ResultSubtitle.Text = found.Count > fresh.Count
                ? "另外 " + (found.Count - fresh.Count) + " 个已经在列表里了，不重复收录。"
                : "勾选你想收进列表的启动器，剩下的当作没看见。";
            SelectAll.IsChecked = true;
            UpdateCount();
        }

        private void ShowPanel(UIElement panel)
        {
            ScanningPanel.Visibility = panel == ScanningPanel ? Visibility.Visible : Visibility.Collapsed;
            ResultPanel.Visibility = panel == ResultPanel ? Visibility.Visible : Visibility.Collapsed;
            EmptyPanel.Visibility = panel == EmptyPanel ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCount()
        {
            if (AddBtn == null) return;
            int selected = _rows.Count(r => r.IsChecked);
            AddBtn.Content = selected > 0 ? "添加选中（" + selected + "）" : "添加选中";
            AddBtn.IsEnabled = selected > 0;
            NoSelectionHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SelectAll_Changed(object sender, RoutedEventArgs e)
        {
            if (SelectAll.IsChecked == null) return;
            var value = SelectAll.IsChecked == true;
            foreach (var row in _rows) row.IsChecked = value;
            UpdateCount();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var picked = _rows.Where(r => r.IsChecked).Select(r => r.Found).ToList();
            if (picked.Count == 0) return;

            _closedByUser = true;
            var handler = Finished;
            if (handler != null) handler(picked);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _closedByUser = true;
            if (_cts != null) _cts.Cancel();
            var handler = Cancelled;
            if (handler != null) handler();
        }

        private void Rescan_Click(object sender, RoutedEventArgs e)
        {
            _rows.Clear();
            RunScan();
        }
    }

    public class ScanRow : INotifyPropertyChanged
    {
        private bool _isChecked;

        public ScanRow(FoundLauncher found)
        {
            Found = found;
            var row = this;
            found.GetType();
        }

        public FoundLauncher Found { get; private set; }

        public string Name { get { return Found.SuggestedName; } }
        public string Path { get { return Found.Path; } }
        public string Reason { get { return Found.Reason; } }

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs("IsChecked"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
