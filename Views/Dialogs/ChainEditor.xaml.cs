using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LauncherLauncher.Models;
using LauncherLauncher.Services;

namespace LauncherLauncher.Views.Dialogs
{
    public partial class ChainEditor : UserControl
    {
        public event Action<LaunchChain> Saved;
        public event Action Cancelled;

        private readonly LaunchChain _chain;
        private readonly bool _isNew;
        private readonly ObservableCollection<StepRow> _rows = new ObservableCollection<StepRow>();

        public ChainEditor(LaunchChain chain, bool isNew)
        {
            InitializeComponent();

            _chain = chain;
            _isNew = isNew;

            TitleText.Text = isNew ? "新的接力" : "编辑接力";
            NameBox.Text = chain.Name ?? "";

            if (chain.Mode == LaunchChain.ModeSimultaneous) ModeSim.IsChecked = true;
            else if (chain.Mode == LaunchChain.ModeDelay) ModeDelay.IsChecked = true;
            else ModeWait.IsChecked = true;

            DelayBox.Text = chain.DelayMs.ToString();
            UpdateDelayState();

            foreach (var step in chain.Steps) AddRow(new ChainStep { LauncherId = step.LauncherId, Note = step.Note });

            StepList.ItemsSource = _rows;
            _rows.CollectionChanged += (s, e) => UpdateEmpty();
            UpdateEmpty();

            var items = AppState.Db.Launchers.OrderBy(l => l.DisplayName).ToList();
            PickBox.ItemsSource = items;
            PickBox.DisplayMemberPath = "DisplayName";
            if (items.Count > 0) PickBox.SelectedIndex = 0;
        }

        private void UpdateEmpty()
        {
            if (EmptySteps == null) return;
            EmptySteps.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (PickBox != null) PickBox.IsEnabled = AppState.Db.Launchers.Count > 0;
        }

        private void AddRow(ChainStep step)
        {
            _rows.Add(new StepRow(step, AppState.Db.Find(step.LauncherId)));
            Renumber();
        }

        private void Renumber()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Index = i + 1;
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            UpdateDelayState();
        }

        private void UpdateDelayState()
        {
            if (DelayBox == null) return;
            DelayBox.IsEnabled = ModeDelay.IsChecked == true;
        }

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            var entry = PickBox.SelectedItem as LauncherEntry;
            if (entry == null)
            {
                ShowError("先把启动器录进列表，再来排接力。");
                return;
            }

            AddRow(new ChainStep { LauncherId = entry.Id });
            HideError();
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) == null ? null : (sender as FrameworkElement).Tag as StepRow;
            if (row == null) return;
            int index = _rows.IndexOf(row);
            if (index <= 0) return;
            _rows.Move(index, index - 1);
            Renumber();
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) == null ? null : (sender as FrameworkElement).Tag as StepRow;
            if (row == null) return;
            int index = _rows.IndexOf(row);
            if (index < 0 || index >= _rows.Count - 1) return;
            _rows.Move(index, index + 1);
            Renumber();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) == null ? null : (sender as FrameworkElement).Tag as StepRow;
            if (row == null) return;
            _rows.Remove(row);
            Renumber();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = (NameBox.Text ?? "").Trim();
            if (name.Length == 0)
            {
                ShowError("接力总得有个名字。");
                return;
            }

            if (_rows.Count == 0)
            {
                ShowError("至少要有一个启动器参与接力。");
                return;
            }

            int delay = 900;
            if (ModeDelay.IsChecked == true)
            {
                if (!int.TryParse((DelayBox.Text ?? "").Trim(), out delay) || delay < 0 || delay > 600000)
                {
                    ShowError("间隔得是 0 到 600000 之间的毫秒数。");
                    return;
                }
            }

            var chain = _isNew ? new LaunchChain() : _chain;
            if (_isNew) chain.Id = Guid.NewGuid().ToString("N");
            chain.Name = name;
            chain.Mode = ModeSim.IsChecked == true
                ? LaunchChain.ModeSimultaneous
                : ModeDelay.IsChecked == true ? LaunchChain.ModeDelay : LaunchChain.ModeWaitExit;
            chain.DelayMs = delay;
            chain.Steps = _rows.Select(r => new ChainStep { LauncherId = r.Step.LauncherId, Note = r.Step.Note }).ToList();

            var handler = Saved;
            if (handler != null) handler(chain);
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

        private void HideError()
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    public class StepRow : INotifyPropertyChanged
    {
        private int _index;
        private string _name;
        private string _path;

        public StepRow(ChainStep step, LauncherEntry entry)
        {
            Step = step;
            _name = entry != null ? entry.DisplayName : "（这个启动器已经不在列表里了）";
            _path = entry != null ? entry.Path : "";
        }

        public ChainStep Step { get; private set; }

        public int Index
        {
            get { return _index; }
            set
            {
                if (_index == value) return;
                _index = value;
                Raise("Index");
            }
        }

        public string Name
        {
            get { return _name; }
            private set { _name = value; Raise("Name"); }
        }

        public string Path
        {
            get { return _path; }
            private set { _path = value; Raise("Path"); }
        }

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
