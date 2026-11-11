using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace LauncherLauncher.Services
{
    public static class Store
    {
        private static string _dataDir;

        /// <summary>程序所在目录（exe 旁边）。</summary>
        public static string AppDirectory
        {
            get
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                string p = asm.Location;
                if (string.IsNullOrEmpty(p)) p = AppDomain.CurrentDomain.BaseDirectory;
                return Path.GetDirectoryName(p);
            }
        }

        /// <summary>便携模式：exe 旁边存在 portable.txt 时，数据放在同目录的 Data 里。</summary>
        public static bool IsPortable
        {
            get { return File.Exists(Path.Combine(AppDirectory, "portable.txt")); }
        }

        public static string DataDirectory
        {
            get
            {
                if (_dataDir != null) return _dataDir;
                _dataDir = IsPortable
                    ? Path.Combine(AppDirectory, "Data")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LauncherLauncher");
                if (!Directory.Exists(_dataDir)) Directory.CreateDirectory(_dataDir);
                return _dataDir;
            }
        }

        public static string DataFile { get { return Path.Combine(DataDirectory, "data.json"); } }
        public static string BackupFile { get { return Path.Combine(DataDirectory, "data.backup.json"); } }
        public static string CrashLog { get { return Path.Combine(DataDirectory, "crash.log"); } }

        public static JavaScriptSerializer NewSerializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 200 };
        }

        public static Models.AppDatabase Load()
        {
            var db = TryLoad(DataFile) ?? TryLoad(BackupFile) ?? new Models.AppDatabase();
            Normalize(db);
            return db;
        }

        private static Models.AppDatabase TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var db = NewSerializer().Deserialize<Models.AppDatabase>(json);
                return db;
            }
            catch (Exception ex)
            {
                TryLog("load " + path + " failed: " + ex);
                return null;
            }
        }

        public static void Normalize(Models.AppDatabase db)
        {
            if (db == null) throw new ArgumentNullException("db");
            if (db.Launchers == null) db.Launchers = new System.Collections.Generic.List<Models.LauncherEntry>();
            if (db.Chains == null) db.Chains = new System.Collections.Generic.List<Models.LaunchChain>();
            if (db.RecentIds == null) db.RecentIds = new System.Collections.Generic.List<string>();
            if (db.Settings == null) db.Settings = new Models.AppSettings();
            if (db.Settings.LaunchDelayMs <= 0) db.Settings.LaunchDelayMs = 900;
            db.EnsureChain();
            foreach (var l in db.Launchers)
            {
                if (string.IsNullOrEmpty(l.Id)) l.Id = Guid.NewGuid().ToString("N");
            }
        }

        public static void Save(Models.AppDatabase db)
        {
            try
            {
                if (!Directory.Exists(DataDirectory)) Directory.CreateDirectory(DataDirectory);
                string json = NewSerializer().Serialize(db);
                string tmp = DataFile + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(DataFile))
                {
                    try
                    {
                        File.Copy(DataFile, BackupFile, true);
                        File.Delete(DataFile);
                    }
                    catch { /* 备份失败不影响主流程 */ }
                }
                File.Move(tmp, DataFile);
            }
            catch (Exception ex)
            {
                TryLog("save failed: " + ex);
                throw;
            }
        }

        /// <summary>读一眼某个 exe 的体积，用来算“本软件比你的启动器小多少”。</summary>
        public static long ExeSize()
        {
            try
            {
                return new FileInfo(Assembly.GetEntryAssembly().Location).Length;
            }
            catch { return 0; }
        }

        public static void TryLog(string message)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                File.AppendAllText(CrashLog,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
