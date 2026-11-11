using System;
using System.Globalization;

namespace LauncherLauncher.Util
{
    public static class Format
    {
        /// <summary>“刚刚 / 12 分钟前 / 3 天前 / 2025-03-01”。</summary>
        public static string Relative(DateTime local)
        {
            if (local == DateTime.MinValue || local.Year < 2000) return "从未启动";
            var span = DateTime.Now - local;
            if (span.TotalSeconds < 0) return "刚刚";
            if (span.TotalSeconds < 60) return "刚刚";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " 分钟前";
            if (span.TotalHours < 24) return (int)span.TotalHours + " 小时前";
            if (span.TotalDays < 30) return (int)span.TotalDays + " 天前";
            if (span.TotalDays < 365) return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string HumanSize(long bytes)
        {
            if (bytes < 1024) return bytes + " 字节";
            double kb = bytes / 1024.0;
            if (kb < 1024) return Round(kb) + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return Round(mb) + " MB";
            return Round(mb / 1024.0) + " GB";
        }

        private static string Round(double v)
        {
            if (v >= 100) return Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
            if (v >= 10) return v.ToString("0.#", CultureInfo.InvariantCulture);
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string Elide(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            if (max <= 3) return text.Substring(0, max);
            int head = (max - 1) / 2;
            int tail = max - 1 - head;
            return text.Substring(0, head) + "…" + text.Substring(text.Length - tail);
        }

        /// <summary>“1 小时 12 分钟”。</summary>
        public static string Duration(double seconds)
        {
            if (seconds < 60) return (int)seconds + " 秒";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours < 1) return ts.Minutes + " 分钟";
            if (ts.TotalDays < 1) return (int)ts.TotalHours + " 小时 " + ts.Minutes + " 分钟";
            return (int)ts.TotalDays + " 天 " + ts.Hours + " 小时";
        }
    }
}
