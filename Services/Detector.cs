using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace LauncherLauncher.Services
{
    public class FoundLauncher
    {
        public string Path { get; set; }
        public string SuggestedName { get; set; }
        public string Reason { get; set; }
        public int Score { get; set; }
        public bool IsJar { get { return string.Equals(System.IO.Path.GetExtension(Path), ".jar", StringComparison.OrdinalIgnoreCase); } }
    }

    /// <summary>在本机瞎翻，找出看起来像“启动器”的东西。</summary>
    public static class Detector
    {
        private static readonly Dictionary<string, string> KnownNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "pcl", "PCL2" },
            { "pcl2", "PCL2" },
            { "pcl2exe", "PCL2" },
            { "plain craft launcher", "Plain Craft Launcher 2" },
            { "plain craft launcher 2", "Plain Craft Launcher 2" },
            { "hmcl", "HMCL" },
            { "hmcl-stable", "HMCL" },
            { "hellominecraftlauncher", "Hello Minecraft! Launcher" },
            { "hello minecraft launcher", "Hello Minecraft! Launcher" },
            { "hello minecraft! launcher", "Hello Minecraft! Launcher" },
            { "bakaxl", "BakaXL" },
            { "bakaxl_start", "BakaXL" },
            { "xmcl", "XMCL" },
            { "xmcl client", "XMCL" },
            { "prismlauncher", "Prism Launcher" },
            { "multimc", "MultiMC" },
            { "polymc", "PolyMC" },
            { "atlauncher", "ATLauncher" },
            { "gdlauncher", "GDLauncher" },
            { "tlauncher", "TLauncher" },
            { "sklauncher", "SKLauncher" },
            { "lunar client", "Lunar Client" },
            { "lunarclient", "Lunar Client" },
            { "badlion client", "Badlion Client" },
            { "badlion", "Badlion Client" },
            { "feather", "Feather" },
            { "modrinth app", "Modrinth App" },
            { "curseforge", "CurseForge" },
            { "technic launcher", "Technic Launcher" },
            { "techniclauncher", "Technic Launcher" },
            { "minecraftlauncher", "Minecraft Launcher" },
            { "minecraft launcher", "Minecraft Launcher" },
            { "official launcher", "官方启动器" },
            { "salwyrr", "Salwyrr Launcher" },
            { "batmod", "BatMod" },
            { "mc launcher", "MC Launcher" },
            { "mclauncher", "MC Launcher" },
            { "launcher", "某个启动器" }
        };

        /// <summary>名字里带这些词的，基本都不是启动器，是启动器的零件。</summary>
        private static readonly string[] Noise =
        {
            "pwa", "rsxruntime", "unins", "crashpad", "crashhandler", "launcherservice", "launchertelemetry",
            "notification", "elevation", "ffmpeg", "chrome_proxy", "msedge", "updater", "launcherhelper",
            "installer", "repairtool", "reporttool", "mclauncherruntime"
        };

        private static readonly HashSet<string> SkipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "windows", "winsxs", "$recycle.bin", "system volume information", "node_modules", ".git", ".svn",
            "programdata", "packages", "temp", "tmp", "cache", ".cache", "installer", "driverstore",
            "servicing", "assembly", "dotnet", "nuget", "refs", "windowsapps", "common files",
            "microsoft.net", "microsoft sdks", "microsoft visual studio", "reference assemblies",
            "windows defender", "windows nt", "windows mail", "windows media player", "internet explorer",
            "recovery", "perflogs", "config.msi", "msocache", "boot", "efi", "system32", "syswow64"
        };

        public static List<FoundLauncher> Scan(Action<string> progress, CancellationToken token)
        {
            var found = new Dictionary<string, FoundLauncher>(StringComparer.OrdinalIgnoreCase);
            var sw = Stopwatch.StartNew();
            int visited = 0;
            const int MaxDirs = 9000;
            var timeout = TimeSpan.FromSeconds(9);

            foreach (var root in BuildRoots())
            {
                if (token.IsCancellationRequested) break;
                if (!Directory.Exists(root.Path)) continue;
                if (root.Depth <= 0) continue;

                var queue = new Queue<KeyValuePair<string, int>>();
                queue.Enqueue(new KeyValuePair<string, int>(root.Path, 0));
                while (queue.Count > 0)
                {
                    if (token.IsCancellationRequested) return Finish(found);
                    if (sw.Elapsed > timeout || visited > MaxDirs) return Finish(found);

                    var kv = queue.Dequeue();
                    string dir = kv.Key;
                    int depth = kv.Value;
                    visited++;

                    if (progress != null && visited % 40 == 0)
                    {
                        try { progress(dir); } catch { }
                    }

                    IEnumerable<string> files;
                    try { files = Directory.EnumerateFiles(dir); }
                    catch { files = new string[0]; }
                    foreach (var f in files)
                    {
                        if (token.IsCancellationRequested) return Finish(found);
                        Consider(f, found);
                    }

                    if (depth >= root.Depth) continue;
                    IEnumerable<string> subs;
                    try { subs = Directory.EnumerateDirectories(dir); }
                    catch { continue; }
                    foreach (var sub in subs)
                    {
                        string name;
                        try { name = Path.GetFileName(sub); }
                        catch { continue; }
                        if (SkipDirs.Contains(name)) continue;
                        try
                        {
                            var attrs = File.GetAttributes(sub);
                            if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                        }
                        catch { continue; }
                        queue.Enqueue(new KeyValuePair<string, int>(sub, depth + 1));
                    }
                }
            }
            return Finish(found);
        }

        private static List<FoundLauncher> Finish(Dictionary<string, FoundLauncher> found)
        {
            return found.Values.OrderByDescending(f => f.Score).ThenBy(f => f.SuggestedName).ToList();
        }

        private static void Consider(string file, Dictionary<string, FoundLauncher> found)
        {
            string ext = Path.GetExtension(file);
            if (!string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase)) return;

            string nameNoExt = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(nameNoExt)) return;
            if (nameNoExt.IndexOf("launcherlauncher", StringComparison.OrdinalIgnoreCase) >= 0) return;

            string lower = nameNoExt.ToLowerInvariant();
            foreach (var noise in Noise)
            {
                if (lower.IndexOf(noise, StringComparison.Ordinal) >= 0) return;
            }

            string pretty;
            int score;
            if (!Identify(nameNoExt, ext, out pretty, out score)) return;
            if (score < 55) return;
            if (string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase) && score < 75) return;

            string full;
            try { full = Path.GetFullPath(file); }
            catch { return; }

            FoundLauncher existing;
            if (found.TryGetValue(full, out existing))
            {
                if (score > existing.Score) { existing.Score = score; existing.SuggestedName = pretty; }
                return;
            }

            found[full] = new FoundLauncher
            {
                Path = full,
                SuggestedName = pretty,
                Score = score,
                Reason = ext.Equals(".jar", StringComparison.OrdinalIgnoreCase) ? "Java 程序" : (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ? "快捷方式" : "可执行文件")
            };
        }

        private static bool Identify(string nameNoExt, string ext, out string pretty, out int score)
        {
            pretty = nameNoExt.Trim();
            score = 0;
            string key = Normalize(nameNoExt);

            string mapped;
            if (KnownNames.TryGetValue(key, out mapped))
            {
                pretty = mapped;
                score = key == "launcher" ? 55 : 100;
                return true;
            }

            if (key.StartsWith("pcl", StringComparison.Ordinal)) { pretty = "PCL2"; score = 92; return true; }
            if (key.StartsWith("hmcl", StringComparison.Ordinal)) { pretty = "HMCL"; score = 92; return true; }
            if (key.StartsWith("bakaxl", StringComparison.Ordinal)) { pretty = "BakaXL"; score = 92; return true; }
            if (key.StartsWith("xmcl", StringComparison.Ordinal)) { pretty = "XMCL"; score = 90; return true; }
            if (key.StartsWith("prism", StringComparison.Ordinal)) { pretty = "Prism Launcher"; score = 88; return true; }

            if (key.Contains("launcher") || key.Contains("启动器"))
            {
                // 只认「和 Minecraft 有关系的启动器」。其余带 launcher 字样的（Epic、PWA 外壳、
                // 各种运行时）给个不及格的分数，让它们留在外面。
                if (key.Contains("minecraft") || key.Contains("mc") || key.Contains("mine"))
                {
                    score = 78;
                    pretty = key.Contains("minecraft") ? "Minecraft Launcher" : pretty;
                }
                else if (key == "launcher")
                {
                    score = 55;
                }
                else
                {
                    score = 45;
                }
                return true;
            }

            if (key.Contains("minecraft") && (key.Contains("client") || key.Contains("启动")))
            {
                score = 58;
                return true;
            }

            return false;
        }

        private static string Normalize(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '.') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private class Root
        {
            public string Path;
            public int Depth;
        }

        private static List<Root> BuildRoots()
        {
            var list = new List<Root>();
            Action<string, int> add = (p, d) =>
            {
                if (string.IsNullOrWhiteSpace(p)) return;
                if (list.Any(r => string.Equals(r.Path, p, StringComparison.OrdinalIgnoreCase))) return;
                list.Add(new Root { Path = p, Depth = d });
            };

            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 2);
            add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), 2);
            add(Path.Combine(user, "Downloads"), 3);
            add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 3);
            add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 3);
            add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 3);
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), 4);
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), 3);
            add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), 3);
            add(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), 3);
            add(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), 3);

            // 常见的“我把游戏装在这儿”的地方
            foreach (var drive in SafeDrives())
            {
                foreach (var leaf in new[] { "Games", "Game", "Minecraft", ".minecraft", "MC", "Launcher", "游戏" })
                {
                    string p = Path.Combine(drive, leaf);
                    if (Directory.Exists(p)) add(p, 3);
                }
                string gamesMc = Path.Combine(drive, "Games", "Minecraft");
                if (Directory.Exists(gamesMc)) add(gamesMc, 3);
            }

            // 各大启动器的默认数据目录
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            add(Path.Combine(appdata, ".minecraft"), 2);
            add(Path.Combine(appdata, "XMCL"), 2);
            add(Path.Combine(appdata, "PrismLauncher"), 2);
            add(Path.Combine(appdata, "MultiMC"), 2);
            add(Path.Combine(appdata, "BakaXL"), 2);
            add(Path.Combine(local, "BakaXL"), 2);
            add(Path.Combine(local, "XMCL"), 2);

            return list;
        }

        private static IEnumerable<string> SafeDrives()
        {
            DriveInfo[] drives;
            try { drives = DriveInfo.GetDrives(); }
            catch { return new string[0]; }
            return drives.Where(d =>
            {
                try { return d.IsReady && d.DriveType == DriveType.Fixed; }
                catch { return false; }
            }).Select(d => d.RootDirectory.FullName).ToArray();
        }
    }
}
