using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LauncherLauncher.Models;
using LauncherLauncher.Services;

namespace LauncherLauncher.Views.Dialogs
{
    public partial class ProgressRunDialog : UserControl
    {
        public event Action Finished;

        private readonly List<RunRow> _rows = new List<RunRow>();
        private readonly bool _simultaneous;
        private readonly int _delayMs;
        private bool _cancelRequested;
        private bool _completed;
        private bool _closed;

        public ProgressRunDialog(List<LauncherEntry> live, string title, bool simultaneous, int delayMs)
        {
            InitializeComponent();

            _simultaneous = simultaneous;
            _delayMs = Math.Max(120, delayMs);

            foreach (var entry in live) _rows.Add(new RunRow(entry));
            RunList.ItemsSource = _rows;

            TitleText.Text = title;
            SubtitleText.Text = simultaneous
                ? "同时启动 " + _rows.Count + " 个启动器。祝你的任务管理器好运。"
                : "依次启动 " + _rows.Count + " 个启动器，中间留一点体面。";
            Bar.Maximum = Math.Max(1, _rows.Count);
            ProgressText.Text = "0 / " + _rows.Count;

            Loaded += OnLoaded;
            Unloaded += (s, e) =>
            {
                _closed = true;
                _cancelRequested = true;
            };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            int ok = 0;
            int fail = 0;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];

                if (_cancelRequested || _closed)
                {
                    row.SetSkipped();
                    continue;
                }

                row.SetRunning();
                var outcome = LauncherRunner.Start(row.Entry, AppState.Db.Settings);

                if (outcome.Success)
                {
                    row.Entry.MarkLaunched();
                    AppState.TouchRecent(row.Entry.Id);
                    row.SetOk();
                    ok++;
                }
                else
                {
                    row.SetFail(outcome.Message);
                    fail++;
                }

                Bar.Value = ok + fail;
                ProgressText.Text = (ok + fail) + " / " + _rows.Count;

                if (i < _rows.Count - 1 && !_cancelRequested && !_closed)
                {
                    await Task.Delay(_simultaneous ? 70 : _delayMs);
                }
            }

            _completed = true;
            if (_closed) return;

            string summary;
            if (_cancelRequested)
            {
                summary = "取消了。已经起来的那些启动器不会自己关掉，它们正等着你去挨个处理。";
            }
            else if (_rows.Count == 1)
            {
                summary = ok > 0
                    ? "启动完成。现在你的屏幕上多了一个启动器，不知道它要干什么。"
                    : "启动失败。它连起来都不愿意，可能有它自己的想法。";
            }
            else
            {
                summary = fail == 0
                    ? "全部启动成功，共 " + ok + " 个。它们彼此并不知道对方的存在。"
                    : "成功 " + ok + " 个，失败 " + fail + " 个。失败的那些大概有自己的难处。";
            }

            SummaryText.Text = summary;
            CancelBtn.Content = "关闭";
            CancelBtn.Style = (Style)FindResource("Btn.Primary");
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_completed)
            {
                _cancelRequested = true;
                CancelBtn.IsEnabled = false;
                CancelBtn.Content = "正在取消…";
                return;
            }

            var handler = Finished;
            if (handler != null) handler();
        }
    }

    public class RunRow : INotifyPropertyChanged
    {
        private static readonly Brush MutedBg = Freeze(Color.FromRgb(0xF1, 0xF4, 0xF9));
        private static readonly Brush AccentBg = Freeze(Color.FromRgb(0xEA, 0xF0, 0xFF));
        private static readonly Brush SuccessBg = Freeze(Color.FromRgb(0xE7, 0xF7, 0xF0));
        private static readonly Brush DangerBg = Freeze(Color.FromRgb(0xFD, 0xEC, 0xEC));
        private static readonly Brush AccentFg = Freeze(Color.FromRgb(0x2E, 0x6B, 0xFF));
        private static readonly Brush SuccessFg = Freeze(Color.FromRgb(0x12, 0x99, 0x6B));
        private static readonly Brush DangerFg = Freeze(Color.FromRgb(0xE5, 0x48, 0x4D));
        private static readonly Brush TertiaryFg = Freeze(Color.FromRgb(0x98, 0xA1, 0xB0));

        private static Brush Freeze(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private string _status = "等待中";
        private string _detail;
        private string _glyph = "\uE823";
        private Brush _statusBrush = TertiaryFg;
        private Brush _badgeBrush = MutedBg;
        private Brush _glyphBrush = TertiaryFg;

        public RunRow(LauncherEntry entry)
        {
            Entry = entry;
            _detail = entry.Path;
        }

        public LauncherEntry Entry { get; private set; }

        public string Name { get { return Entry.DisplayName; } }

        public string Status
        {
            get { return _status; }
            private set { _status = value; Raise("Status"); }
        }

        public string Detail
        {
            get { return _detail; }
            private set { _detail = value; Raise("Detail"); }
        }

        public string Glyph
        {
            get { return _glyph; }
            private set { _glyph = value; Raise("Glyph"); }
        }

        public Brush StatusBrush
        {
            get { return _statusBrush; }
            private set { _statusBrush = value; Raise("StatusBrush"); }
        }

        public Brush BadgeBrush
        {
            get { return _badgeBrush; }
            private set { _badgeBrush = value; Raise("BadgeBrush"); }
        }

        public Brush GlyphBrush
        {
            get { return _glyphBrush; }
            private set { _glyphBrush = value; Raise("GlyphBrush"); }
        }

        public void SetRunning()
        {
            Detail = Entry.Path;
            Status = "启动中";
            Glyph = "\uE768";
            StatusBrush = AccentFg;
            BadgeBrush = AccentBg;
            GlyphBrush = AccentFg;
        }

        public void SetOk()
        {
            Detail = "已经起来了";
            Status = "已启动";
            Glyph = "\uE73E";
            StatusBrush = SuccessFg;
            BadgeBrush = SuccessBg;
            GlyphBrush = SuccessFg;
        }

        public void SetFail(string message)
        {
            Detail = string.IsNullOrEmpty(message) ? "没起来" : message;
            Status = "失败";
            Glyph = "\uE711";
            StatusBrush = DangerFg;
            BadgeBrush = DangerBg;
            GlyphBrush = DangerFg;
        }

        public void SetSkipped()
        {
            Detail = "没轮到它";
            Status = "已跳过";
            Glyph = "\uE8D8";
            StatusBrush = TertiaryFg;
            BadgeBrush = MutedBg;
            GlyphBrush = TertiaryFg;
        }

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
