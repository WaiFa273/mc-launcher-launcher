using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LauncherLauncher.Models;

namespace LauncherLauncher.Services
{
    public class LaunchOutcome
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Process Process { get; set; }
        public string Command { get; set; }

        public static LaunchOutcome Fail(string message)
        {
            return new LaunchOutcome { Success = false, Message = message };
        }
    }

    public static class LauncherRunner
    {
        /// <summary>启动一个启动器。返回的 Process 可能为 null（比如被系统接管的情况）。</summary>
        public static LaunchOutcome Start(LauncherEntry entry, AppSettings settings, bool? runAsAdminOverride = null)
        {
            if (entry == null) return LaunchOutcome.Fail("没有这条记录。");
            if (string.IsNullOrWhiteSpace(entry.Path)) return LaunchOutcome.Fail("这条记录还没有设置路径。");
            if (!File.Exists(entry.Path)) return LaunchOutcome.Fail("文件不见了：" + entry.Path);

            if (IsSelf(entry.Path))
            {
                return LaunchOutcome.Fail("启动器启动器不能启动它自己。这会让宇宙形成一个环。");
            }

            bool asAdmin = runAsAdminOverride ?? entry.RunAsAdmin;

            string ext = Path.GetExtension(entry.Path).ToLowerInvariant();
            string workDir = !string.IsNullOrWhiteSpace(entry.WorkingDirectory) && Directory.Exists(entry.WorkingDirectory)
                ? entry.WorkingDirectory
                : SafeDirectory(entry.Path);
            string extraArgs = string.IsNullOrWhiteSpace(entry.Arguments) ? "" : " " + entry.Arguments;

            try
            {
                if (ext == ".jar")
                {
                    string java = FindJava(settings == null ? null : settings.JavaPath);
                    if (string.IsNullOrEmpty(java))
                    {
                        return LaunchOutcome.Fail("找不到 java/javaw。一个 Java 启动器没有 Java，就像一个启动器没有启动器。");
                    }

                    var psi = new ProcessStartInfo(java)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = workDir,
                        CreateNoWindow = true
                    };
                    psi.Arguments = "-jar \"" + entry.Path + "\"" + extraArgs;
                    var p = Process.Start(psi);
                    return new LaunchOutcome { Success = true, Process = p, Message = "已用 " + Path.GetFileName(java) + " 启动", Command = java };
                }

                if (ext == ".exe")
                {
                    var psi = new ProcessStartInfo(entry.Path)
                    {
                        WorkingDirectory = workDir,
                        UseShellExecute = true
                    };
                    if (!string.IsNullOrWhiteSpace(entry.Arguments)) psi.Arguments = entry.Arguments;
                    if (asAdmin) psi.Verb = "runas";
                    var p = Process.Start(psi);
                    return new LaunchOutcome { Success = true, Process = p, Message = "已启动", Command = entry.Path };
                }

                // .lnk / .bat / 其他：交给 Shell
                var psi2 = new ProcessStartInfo(entry.Path)
                {
                    WorkingDirectory = workDir,
                    UseShellExecute = true
                };
                if (!string.IsNullOrWhiteSpace(entry.Arguments)) psi2.Arguments = entry.Arguments;
                if (asAdmin) psi2.Verb = "runas";
                var p2 = Process.Start(psi2);
                return new LaunchOutcome { Success = true, Process = p2, Message = "已交给系统打开", Command = entry.Path };
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) return LaunchOutcome.Fail("你取消了管理员权限请求。");
                return LaunchOutcome.Fail("启动失败：" + ex.Message);
            }
            catch (Exception ex)
            {
                return LaunchOutcome.Fail("启动失败：" + ex.Message);
            }
        }

        private static string SafeDirectory(string path)
        {
            try { return Path.GetDirectoryName(path); }
            catch { return null; }
        }

        /// <summary>在本机找 javaw.exe（找不到再退回 java.exe）。</summary>
        /// <summary>是不是本软件自己。防止“一键启动全部”把世界套成一个环。</summary>
        public static bool IsSelf(string path)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                if (assembly == null || string.IsNullOrEmpty(assembly.Location)) return false;
                return string.Equals(Path.GetFullPath(assembly.Location), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string FindJava(string hint)
        {
            if (!string.IsNullOrWhiteSpace(hint) && File.Exists(hint)) return hint;

            string home = Environment.GetEnvironmentVariable("JAVA_HOME");
            string found = Probe(home == null ? null : Path.Combine(home, "bin"));
            if (found != null) return found;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                found = Probe(dir.Trim().Trim('"'));
                if (found != null) return found;
            }

            // Mojang 自带运行时（几乎所有玩过游戏的机器都有）
            string mcRuntime = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime");
            found = FindUnder(mcRuntime, "javaw.exe", 8);
            if (found != null) return found;

            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var vendors = new List<string>();
            foreach (var root in new[] { pf, pf86 })
            {
                if (string.IsNullOrEmpty(root)) continue;
                vendors.AddRange(new[]
                {
                    Path.Combine(root, "Java"),
                    Path.Combine(root, "Eclipse Adoptium"),
                    Path.Combine(root, "Eclipse Foundation"),
                    Path.Combine(root, "BellSoft"),
                    Path.Combine(root, "Zulu"),
                    Path.Combine(root, "Amazon Corretto"),
                    Path.Combine(root, "Microsoft"),
                    Path.Combine(root, "AdoptOpenJDK"),
                    Path.Combine(root, "Semeru"),
                });
            }
            vendors.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Eclipse Adoptium"));
            vendors.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft"));

            foreach (var v in vendors)
            {
                found = FindUnder(v, "javaw.exe", 5);
                if (found != null) return found;
            }
            return null;
        }

        private static string Probe(string binDir)
        {
            if (string.IsNullOrWhiteSpace(binDir)) return null;
            try
            {
                string javaw = Path.Combine(binDir, "javaw.exe");
                if (File.Exists(javaw)) return javaw;
                string java = Path.Combine(binDir, "java.exe");
                if (File.Exists(java)) return java;
            }
            catch { }
            return null;
        }

        private static string FindUnder(string root, string fileName, int maxDepth)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return null;
            try
            {
                var queue = new Queue<KeyValuePair<string, int>>();
                queue.Enqueue(new KeyValuePair<string, int>(root, 0));
                int visited = 0;
                while (queue.Count > 0 && visited < 4000)
                {
                    var kv = queue.Dequeue();
                    visited++;
                    string dir = kv.Key;
                    int depth = kv.Value;
                    string direct = Path.Combine(dir, fileName);
                    if (File.Exists(direct)) return direct;
                    if (depth >= maxDepth) continue;
                    IEnumerable<string> subs;
                    try { subs = Directory.EnumerateDirectories(dir); }
                    catch { continue; }
                    foreach (var sub in subs)
                    {
                        try
                        {
                            string name = Path.GetFileName(sub);
                            if (name.Equals("bin", StringComparison.OrdinalIgnoreCase))
                            {
                                string hit = Probe(sub);
                                if (hit != null) return hit;
                            }
                        }
                        catch { }
                        queue.Enqueue(new KeyValuePair<string, int>(sub, depth + 1));
                    }
                }
            }
            catch { }
            return null;
        }

        public static void RevealInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var psi = new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true };
                    Process.Start(psi);
                }
                else
                {
                    string dir = SafeDirectory(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\"") { UseShellExecute = true });
                }
            }
            catch { }
        }
    }
}
