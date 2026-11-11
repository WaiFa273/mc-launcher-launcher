using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LauncherLauncher.Services
{
    /// <summary>从 exe / lnk 里抠图标，抠不到就给 null（界面自己会兜底）。</summary>
    public static class IconLoader
    {
        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static ImageSource _appIcon;

        public static ImageSource AppIcon
        {
            get
            {
                if (_appIcon != null) return _appIcon;
                try
                {
                    var sri = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
                    if (sri != null)
                    {
                        using (var stream = sri.Stream)
                        {
                            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            var frame = decoder.Frames[decoder.Frames.Count - 1];
                            frame.Freeze();
                            _appIcon = frame;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Store.TryLog("app icon failed: " + ex.Message);
                }
                return _appIcon;
            }
        }

        public static ImageSource Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string key = path + "|" + SafeStamp(path);
            ImageSource cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            ImageSource result = null;
            try
            {
                string target = path;
                if (string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string resolved = ResolveShortcut(path);
                    if (!string.IsNullOrEmpty(resolved)) target = resolved;
                }

                if (File.Exists(target))
                {
                    using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(target))
                    {
                        if (icon != null)
                        {
                            var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            src.Freeze();
                            result = src;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Store.TryLog("icon failed for " + path + ": " + ex.Message);
            }

            Cache[key] = result;
            return result;
        }

        private static string SafeStamp(string path)
        {
            try { return File.GetLastWriteTimeUtc(path).Ticks.ToString(); }
            catch { return "0"; }
        }

        /// <summary>用 WScript.Shell 解析 .lnk 的目标路径。</summary>
        public static string ResolveShortcut(string lnkPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                var scType = shortcut.GetType();
                return scType.InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            }
            catch (Exception ex)
            {
                Store.TryLog("lnk resolve failed: " + ex.Message);
                return null;
            }
        }
    }
}
