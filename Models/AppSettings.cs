using System;
using System.Collections.Generic;

namespace LauncherLauncher.Models
{
    /// <summary>全局设置。</summary>
    public class AppSettings
    {
        public AppSettings()
        {
            AutoScanOnStartup = true;
            DoubleClickToLaunch = true;
            ShowPathInList = true;
            LaunchDelayMs = 900;
            SortMode = 0;
            JavaPath = "";
        }

        /// <summary>启动时自动扫描本机启动器。</summary>
        public bool AutoScanOnStartup { get; set; }

        /// <summary>双击卡片直接启动。</summary>
        public bool DoubleClickToLaunch { get; set; }

        /// <summary>列表里显示完整路径。</summary>
        public bool ShowPathInList { get; set; }

        /// <summary>关闭窗口时最小化到托盘。</summary>
        public bool MinimizeToTray { get; set; }

        /// <summary>启动启动器后把自己最小化。</summary>
        public bool MinimizeAfterLaunch { get; set; }

        /// <summary>“一键启动”时同时启动（而不是挨个来）。</summary>
        public bool LaunchAllSimultaneous { get; set; }

        /// <summary>“一键启动”间隔（毫秒）。</summary>
        public int LaunchDelayMs { get; set; }

        /// <summary>开机自启。</summary>
        public bool AutoStartWithWindows { get; set; }

        /// <summary>自定义 java(w).exe 路径，留空则自动找。</summary>
        public string JavaPath { get; set; }

        /// <summary>0=最近启动 1=名称 2=启动次数。</summary>
        public int SortMode { get; set; }

        /// <summary>关掉那些“你确定吗”的二次确认。</summary>
        public bool SkipConfirmations { get; set; }

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }
    }

    /// <summary>落盘的整个数据库。</summary>
    public class AppDatabase
    {
        public AppDatabase()
        {
            Version = 1;
            Launchers = new List<LauncherEntry>();
            Chains = new List<LaunchChain>();
            Settings = new AppSettings();
            RecentIds = new List<string>();
        }

        public int Version { get; set; }
        public List<LauncherEntry> Launchers { get; set; }
        public List<LaunchChain> Chains { get; set; }
        public AppSettings Settings { get; set; }
        public List<string> RecentIds { get; set; }

        public LaunchChain PrimaryChain
        {
            get
            {
                if (Chains == null || Chains.Count == 0) return null;
                return Chains[0];
            }
        }

        public LauncherEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id) || Launchers == null) return null;
            foreach (var l in Launchers) if (l.Id == id) return l;
            return null;
        }

        public void EnsureChain()
        {
            if (Chains == null) Chains = new List<LaunchChain>();
            if (Chains.Count == 0)
            {
                Chains.Add(new LaunchChain());
            }
        }

        public bool IsKnownPath(string path)
        {
            if (string.IsNullOrEmpty(path) || Launchers == null) return false;
            foreach (var l in Launchers)
                if (string.Equals(l.Path, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
