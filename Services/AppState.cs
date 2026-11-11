using System;
using System.Linq;
using LauncherLauncher.Models;

namespace LauncherLauncher.Services
{
    /// <summary>全局状态：一个数据库 + 一个“变了”的事件。</summary>
    public static class AppState
    {
        public static AppDatabase Db { get; private set; }

        public static event Action Changed;

        public static string VersionText
        {
            get
            {
                var v = typeof(AppState).Assembly.GetName().Version;
                return "v" + v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        public static string SizeText
        {
            get { return Util.Format.HumanSize(Store.ExeSize()); }
        }

        public static void Initialize()
        {
            Db = Store.Load();
        }

        /// <summary>数据库彻底读不出来时的兜底：换一份空数据继续活着。</summary>
        public static void Reset()
        {
            var db = new AppDatabase();
            Store.Normalize(db);
            Db = db;
        }

        public static void Save()
        {
            Store.Save(Db);
        }

        public static void RaiseChanged(bool save)
        {
            if (save) Save();
            var h = Changed;
            if (h != null) h();
        }

        /// <summary>加入最近启动队列（最多 8 个）。</summary>
        public static void TouchRecent(string id)
        {
            if (Db == null || string.IsNullOrEmpty(id)) return;
            if (Db.RecentIds == null) Db.RecentIds = new System.Collections.Generic.List<string>();
            Db.RecentIds.Remove(id);
            Db.RecentIds.Insert(0, id);
            while (Db.RecentIds.Count > 8) Db.RecentIds.RemoveAt(Db.RecentIds.Count - 1);
        }

        public static int Count { get { return Db == null ? 0 : Db.Launchers.Count; } }
        public static int AliveCount { get { return Db == null ? 0 : Db.Launchers.Count(l => l.Exists); } }
        public static int UsedCount { get { return Db == null ? 0 : Db.Launchers.Count(l => l.LaunchCount > 0); } }
    }
}
