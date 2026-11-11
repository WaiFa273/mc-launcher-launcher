using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Media;

namespace LauncherLauncher.Models
{
    /// <summary>一条“启动器”记录。</summary>
    public class LauncherEntry : INotifyPropertyChanged
    {
        private string _name = "";
        private string _path = "";
        private string _arguments = "";
        private string _workingDirectory = "";
        private string _notes = "";
        private string _tagsRaw = "";
        private bool _runAsAdmin;
        private bool _isSelected;
        private int _launchCount;
        private long _lastLaunchUtcTicks;
        private long _addedUtcTicks = DateTime.UtcNow.Ticks;
        private ImageSource _iconSource;

        public LauncherEntry()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        /// <summary>稳定 id，接力链用它引用启动器。</summary>
        public string Id { get; set; }

        public string Name
        {
            get { return _name; }
            set { if (Set(ref _name, value)) { OnPropertyChanged("DisplayName"); OnPropertyChanged("Initial"); } }
        }

        public string Path
        {
            get { return _path; }
            set
            {
                if (Set(ref _path, value))
                {
                    _iconSource = null;
                    RefreshDerived();
                }
            }
        }

        public string Arguments
        {
            get { return _arguments; }
            set { Set(ref _arguments, value); }
        }

        public string WorkingDirectory
        {
            get { return _workingDirectory; }
            set { Set(ref _workingDirectory, value); }
        }

        public string Notes
        {
            get { return _notes; }
            set { if (Set(ref _notes, value)) { OnPropertyChanged("HasNotes"); } }
        }

        public string TagsRaw
        {
            get { return _tagsRaw; }
            set { if (Set(ref _tagsRaw, value)) { OnPropertyChanged("TagList"); OnPropertyChanged("HasTags"); } }
        }

        public bool RunAsAdmin
        {
            get { return _runAsAdmin; }
            set { if (Set(ref _runAsAdmin, value)) { OnPropertyChanged("RunAsText"); } }
        }

        public int LaunchCount
        {
            get { return _launchCount; }
            set { if (Set(ref _launchCount, value)) { OnPropertyChanged("LaunchCountText"); } }
        }

        /// <summary>最后一次启动时间（UTC ticks，0 表示从未）。</summary>
        public long LastLaunchUtcTicks
        {
            get { return _lastLaunchUtcTicks; }
            set { if (Set(ref _lastLaunchUtcTicks, value)) { OnPropertyChanged("LastLaunchText"); OnPropertyChanged("LastLaunchUtc"); } }
        }

        public long AddedUtcTicks
        {
            get { return _addedUtcTicks; }
            set { Set(ref _addedUtcTicks, value); }
        }

        [ScriptIgnore]
        public DateTime LastLaunchUtc
        {
            get { return _lastLaunchUtcTicks <= 0 ? DateTime.MinValue : new DateTime(_lastLaunchUtcTicks, DateTimeKind.Utc).ToLocalTime(); }
        }

        [ScriptIgnore]
        public DateTime AddedUtc
        {
            get { return _addedUtcTicks <= 0 ? DateTime.Now : new DateTime(_addedUtcTicks, DateTimeKind.Utc).ToLocalTime(); }
        }

        /// <summary>界面选中态（不落盘）。</summary>
        [ScriptIgnore]
        public bool IsSelected
        {
            get { return _isSelected; }
            set { Set(ref _isSelected, value); }
        }

        /// <summary>从可执行文件提取出来的图标（不落盘，按需加载）。</summary>
        [ScriptIgnore]
        public ImageSource IconSource
        {
            get { return _iconSource; }
            set { if (Set(ref _iconSource, value)) { OnPropertyChanged("HasIcon"); } }
        }

        [ScriptIgnore]
        public bool HasIcon { get { return _iconSource != null; } }

        [ScriptIgnore]
        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(_name) ? (string.IsNullOrEmpty(_path) ? "未命名启动器" : System.IO.Path.GetFileNameWithoutExtension(_path)) : _name; }
        }

        [ScriptIgnore]
        public string Initial
        {
            get
            {
                string n = DisplayName;
                return string.IsNullOrEmpty(n) ? "?" : n.Substring(0, 1).ToUpperInvariant();
            }
        }

        [ScriptIgnore]
        public string KindText
        {
            get
            {
                string ext = System.IO.Path.GetExtension(_path ?? "").ToLowerInvariant();
                if (ext == ".jar") return "Java 程序";
                if (ext == ".lnk") return "快捷方式";
                if (ext == ".bat" || ext == ".cmd") return "批处理";
                if (ext == ".exe") return "应用程序";
                return string.IsNullOrEmpty(ext) ? "未知" : ext.TrimStart('.').ToUpperInvariant() + " 文件";
            }
        }

        [ScriptIgnore]
        public bool Exists
        {
            get { return !string.IsNullOrEmpty(_path) && File.Exists(_path); }
        }

        [ScriptIgnore]
        public bool Missing { get { return !Exists; } }

        [ScriptIgnore]
        public string DirectoryPath
        {
            get
            {
                try { return string.IsNullOrEmpty(_path) ? "" : System.IO.Path.GetDirectoryName(_path); }
                catch { return ""; }
            }
        }

        [ScriptIgnore]
        public string LaunchCountText
        {
            get { return _launchCount <= 0 ? "从未启动" : "启动 " + _launchCount + " 次"; }
        }

        [ScriptIgnore]
        public string LastLaunchText
        {
            get { return Util.Format.Relative(LastLaunchUtc); }
        }

        [ScriptIgnore]
        public string RunAsText { get { return _runAsAdmin ? "管理员" : ""; } }

        [ScriptIgnore]
        public bool HasNotes { get { return !string.IsNullOrWhiteSpace(_notes); } }

        [ScriptIgnore]
        public bool HasTags { get { return TagList.Length > 0; } }

        [ScriptIgnore]
        public string[] TagList
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_tagsRaw)) return new string[0];
                return _tagsRaw.Split(new[] { ',', '，', ';', '；', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0)
                    .Distinct()
                    .Take(6)
                    .ToArray();
            }
        }

        public void MarkLaunched()
        {
            LaunchCount = LaunchCount + 1;
            LastLaunchUtcTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>路径变化后让所有派生属性重新求值。</summary>
        public void RefreshDerived()
        {
            OnPropertyChanged("Exists");
            OnPropertyChanged("Missing");
            OnPropertyChanged("KindText");
            OnPropertyChanged("DirectoryPath");
            OnPropertyChanged("DisplayName");
            OnPropertyChanged("Initial");
            OnPropertyChanged("HasIcon");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }

        protected bool Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
