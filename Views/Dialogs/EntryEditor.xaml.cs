using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LauncherLauncher.Models;
using LauncherLauncher.Services;
using WinForms = System.Windows.Forms;

namespace LauncherLauncher.Views.Dialogs
{
    public partial class EntryEditor : UserControl
    {
        private readonly LauncherEntry _target;

        public event Action<LauncherEntry> Saved;
        public event Action Cancelled;

        public EntryEditor(LauncherEntry entry)
        {
            InitializeComponent();
            _target = entry;
            UpdatePreview();

            if (entry == null) return;

            TitleText.Text = "编辑启动器";
            SubtitleText.Text = "修改之后记得保存，不然它就变回原来那个了。";
            NameBox.Text = entry.Name;
            PathBox.Text = entry.Path;
            ArgsBox.Text = entry.Arguments;
            DirBox.Text = entry.WorkingDirectory;
            TagsBox.Text = entry.TagsRaw;
            NotesBox.Text = entry.Notes;
            AdminCheck.IsChecked = entry.RunAsAdmin;

            UpdatePreview();
        }

        private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();

            if (string.IsNullOrWhiteSpace(NameBox.Text) && !string.IsNullOrWhiteSpace(PathBox.Text))
            {
                try
                {
                    NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(PathBox.Text.Trim().Trim('"'));
                }
                catch { }
            }
        }

        private void UpdatePreview()
        {
            var path = PathBox.Text.Trim().Trim('"');
            System.Windows.Media.ImageSource icon = null;
            try
            {
                if (path.Length > 0 && File.Exists(path)) icon = IconLoader.Get(path);
            }
            catch { }

            // 还没选文件的时候，先用本软件自己的图标占位——一个启动器的图标，放在另一个启动器里。
            if (icon == null && string.IsNullOrEmpty(path)) icon = IconLoader.AppIcon;

            if (icon != null)
            {
                PreviewIcon.Source = icon;
                PreviewIcon.Visibility = Visibility.Visible;
                FallbackIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                PreviewIcon.Visibility = Visibility.Collapsed;
                FallbackIcon.Visibility = Visibility.Visible;
                FallbackIcon.FontFamily = (System.Windows.Media.FontFamily)FindResource("Font.App");
                FallbackIcon.FontWeight = FontWeights.SemiBold;
                FallbackIcon.Text = GuessInitial(path);
            }
        }

        private static string GuessInitial(string path)
        {
            try
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path ?? "");
                if (string.IsNullOrEmpty(name)) return "?";
                return name.Substring(0, 1).ToUpperInvariant();
            }
            catch
            {
                return "?";
            }
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "挑一个启动器",
                Filter = "启动器 (*.exe;*.jar;*.lnk;*.bat;*.cmd)|*.exe;*.jar;*.lnk;*.bat;*.cmd|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            var current = PathBox.Text.Trim().Trim('"');
            try
            {
                if (current.Length > 0)
                {
                    var dir = System.IO.Path.GetDirectoryName(current);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dialog.InitialDirectory = dir;
                }
            }
            catch { }

            if (dialog.ShowDialog() == true) PathBox.Text = dialog.FileName;
        }

        private void BrowseDir_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "选一个工作目录（不选也行）";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == WinForms.DialogResult.OK) DirBox.Text = dialog.SelectedPath;
            }
        }

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            var path = PathBox.Text.Trim().Trim('"');
            if (path.Length == 0 || !File.Exists(path)) return;
            LauncherRunner.RevealInExplorer(path);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var path = PathBox.Text.Trim().Trim('"');

            if (path.Length == 0)
            {
                ShowError("路径不能为空。一个启动器总得有个文件。");
                return;
            }

            if (!File.Exists(path))
            {
                ShowError("这个文件不存在。要么你打错了，要么它已经跑路了。");
                return;
            }

            var name = NameBox.Text.Trim();
            if (name.Length == 0)
            {
                try { name = System.IO.Path.GetFileNameWithoutExtension(path); } catch { name = "无名启动器"; }
            }

            var entry = _target ?? new LauncherEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                AddedUtcTicks = DateTime.UtcNow.Ticks
            };

            entry.Name = name;
            entry.Path = path;
            entry.Arguments = ArgsBox.Text.Trim();
            entry.WorkingDirectory = DirBox.Text.Trim();
            entry.TagsRaw = TagsBox.Text.Trim();
            entry.Notes = NotesBox.Text.Trim();
            entry.RunAsAdmin = AdminCheck.IsChecked == true;

            var handler = Saved;
            if (handler != null) handler(entry);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var handler = Cancelled;
            if (handler != null) handler();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
