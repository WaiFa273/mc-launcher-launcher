using System;
using System.Windows;
using System.Windows.Controls;
using LauncherLauncher.Services;

namespace LauncherLauncher.Views
{
    public partial class ReportPage : UserControl, IRefreshable
    {
        private ReportData _report;

        public ReportPage()
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
            var report = ReportBuilder.Build(AppState.Db);
            _report = report;

            ScoreText.DataContext = report;
            ScoreText.Text = report.Score.ToString();
            ScoreTitleText.Text = report.ScoreTitle;
            ScoreCommentText.Text = report.ScoreComment;
            HeadlineText.Text = report.Headline;
            ReportNoText.Text = "报告编号 " + report.ReportNo;
            StampText.Text = report.Stamp + " · " + report.Signature;
            MetricList.ItemsSource = report.Metrics;
            DiagnosisList.ItemsSource = report.Diagnoses;

            bool empty = AppState.Db.Launchers.Count == 0;
            EmptyHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

            SubtitleText.Text = empty
                ? "根据你收藏的启动器，出一份严肃的体检报告。目前没有素材。"
                : "一份关于你和 " + AppState.Db.Launchers.Count + " 个启动器之间关系的体检报告。";

            if (Wnd != null) Wnd.UpdateStatus();
        }

        private void Recheck_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
            if (Wnd == null) return;
            Wnd.Toast(AppState.Db.Launchers.Count == 0
                ? "又体检了一次，还是没有启动器。"
                : "重新体检完了。数值变了，结局大概没变。");
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (_report == null) return;

            try
            {
                Clipboard.SetText(_report.PlainText);
                if (Wnd != null) Wnd.Toast("报告已复制。可以拿去吓唬别人了。");
            }
            catch (Exception ex)
            {
                Store.TryLog("clipboard: " + ex);
                if (Wnd != null) Wnd.Toast("剪贴板被别的程序占着，没复制上。", "\uE7BA");
            }
        }
    }
}
