using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace LauncherLauncher.Services
{
    /// <summary>
    /// 「启动器启动器启动启动器启动器」——本项目最终极的那个玩笑。
    /// 每一层把自己再启动一次，直到 MaxDepth 层，然后在最深的一层里体面地打住。
    /// </summary>
    public static class Nesting
    {
        /// <summary>最深能套到第几层（0 = 用户点开的那一只，MaxDepth = 最里面那只）。</summary>
        public const int MaxDepth = 9;

        public static readonly int Depth = ParseDepth();

        public static bool IsNested
        {
            get { return Depth > 0; }
        }

        private static int ParseDepth()
        {
            try
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (arg == null || !arg.StartsWith("--nest=", StringComparison.OrdinalIgnoreCase)) continue;

                    int depth;
                    if (int.TryParse(arg.Substring(7).Trim(), out depth) && depth > 0 && depth < 200) return depth;
                }
            }
            catch { }
            return 0;
        }

        public static string ExePath()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                if (assembly != null && !string.IsNullOrEmpty(assembly.Location)) return assembly.Location;
            }
            catch { }

            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args.Length > 0 && File.Exists(args[0])) return args[0];
            }
            catch { }

            return null;
        }

        /// <summary>把自己再启动一份，层数加一。</summary>
        public static bool Spawn()
        {
            var exe = ExePath();
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;

            try
            {
                var psi = new ProcessStartInfo(exe, "--nest=" + (Depth + 1));
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                Store.TryLog("nest spawn: " + ex);
                return false;
            }
        }

        /// <summary>把这一窝的兄弟们全送走（不包括自己）。</summary>
        public static void CloseAll()
        {
            var procs = Siblings();
            if (procs == null) return;

            foreach (var p in procs)
            {
                try
                {
                    if (p.Id == Process.GetCurrentProcess().Id) continue;
                    p.CloseMainWindow();
                }
                catch { }
            }

            // 有的窗口可能设置了「关到托盘」，温柔地关不掉，两秒后再送一次。
            try
            {
                var timer = new Timer(_ =>
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (p.Id == Process.GetCurrentProcess().Id) continue;
                            if (!p.HasExited) p.Kill();
                        }
                        catch { }
                    }
                }, null, 2000, Timeout.Infinite);
                GC.KeepAlive(timer);
            }
            catch { }
        }

        /// <summary>这一窝里还有几只。</summary>
        public static int SiblingCount()
        {
            var procs = Siblings();
            return procs == null ? 1 : procs.Length;
        }

        private static Process[] Siblings()
        {
            try
            {
                var exe = ExePath();
                var name = string.IsNullOrEmpty(exe)
                    ? "LauncherLauncher"
                    : Path.GetFileNameWithoutExtension(exe);
                return Process.GetProcessesByName(name);
            }
            catch
            {
                return null;
            }
        }
    }
}
