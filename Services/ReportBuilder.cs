using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LauncherLauncher.Models;

namespace LauncherLauncher.Services
{
    public class ReportMetric
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Hint { get; set; }
    }

    public class ReportData
    {
        public int Score { get; set; }
        public string ScoreTitle { get; set; }
        public string ScoreComment { get; set; }
        public string Headline { get; set; }
        public List<ReportMetric> Metrics { get; set; }
        public List<string> Diagnoses { get; set; }
        public string ReportNo { get; set; }
        public string Signature { get; set; }
        public string PlainText { get; set; }
        public string Stamp { get; set; }
    }

    /// <summary>“启动器体检报告”，本软件的主要存在意义之一。</summary>
    public static class ReportBuilder
    {
        public static ReportData Build(AppDatabase db)
        {
            var list = db.Launchers ?? new List<LauncherEntry>();
            int total = list.Count;
            int alive = list.Count(l => l.Exists);
            int used = list.Count(l => l.LaunchCount > 0);
            int missing = total - alive;
            int never = total - used;
            int sumLaunches = list.Sum(l => l.LaunchCount);
            long appBytes = Store.ExeSize();

            long libraryBytes = 0;
            foreach (var l in list)
            {
                try
                {
                    if (l.Exists) libraryBytes += new System.IO.FileInfo(l.Path).Length;
                }
                catch { }
            }

            var most = list.Where(l => l.LaunchCount > 0).OrderByDescending(l => l.LaunchCount).FirstOrDefault();
            var stalest = list.OrderBy(l => l.LastLaunchUtcTicks).ThenBy(l => l.AddedUtcTicks).FirstOrDefault();
            var newest = list.OrderByDescending(l => l.AddedUtcTicks).FirstOrDefault();
            int jarCount = list.Count(l => l.Path != null && l.Path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));

            int score;
            if (total == 0)
            {
                score = 100;
            }
            else
            {
                double ratio = used / (double)total;
                score = (int)Math.Round(ratio * 100);
                score -= missing * 5;
                score -= Math.Max(0, total - 4) * 2;
                score = Math.Max(0, Math.Min(100, score));
            }

            var data = new ReportData
            {
                Score = score,
                Metrics = new List<ReportMetric>(),
                Diagnoses = new List<string>(),
                ReportNo = "LL-" + DateTime.Now.ToString("yyyyMMdd") + "-" + (total * 7 + 13).ToString("0000"),
                Signature = "启动器启动器 · 门诊部",
                Stamp = "启动器依赖症 · 晚期"
            };

            data.ScoreTitle = score >= 90 ? "断舍离大师" :
                              score >= 70 ? "专一玩家" :
                              score >= 45 ? "勉强克制" :
                              score >= 20 ? "启动器收藏家" : "电子垃圾制造者";

            data.ScoreComment = total == 0
                ? "你还没有收藏任何启动器。这是本软件见过最健康的用户。"
                : score >= 90 ? "你几乎只用你收藏的那一个。克制得让人怀疑你是不是装的。"
                : score >= 70 ? "启动器数量可控，使用率还行。你只是有点舍不得删。"
                : score >= 45 ? "你收藏的启动器有一半在用。另一半大概在等一个“以后可能会用”的未来。"
                : score >= 20 ? "你收藏的数量已经超过了你的实际需求。这不是启动器，这是纪念碑。"
                : "你收藏了一堆启动器，只用过其中一个。剩下的正在硬盘深处进行考古发掘。";

            data.Headline = total == 0
                ? "体检对象：0 个启动器"
                : "共 " + total + " 个启动器，" + alive + " 个文件还在，" + used + " 个被真正启动过。";

            data.Metrics.Add(new ReportMetric { Label = "收录启动器", Value = total + " 个", Hint = "含失联记录" });
            data.Metrics.Add(new ReportMetric { Label = "文件还在", Value = alive + " 个", Hint = missing > 0 ? missing + " 个已失联" : "全部健在" });
            data.Metrics.Add(new ReportMetric { Label = "累计启动", Value = sumLaunches + " 次", Hint = total == 0 ? "" : "平均每个 " + (sumLaunches / (double)total).ToString("0.#") + " 次" });
            data.Metrics.Add(new ReportMetric { Label = "最常用", Value = most == null ? "—" : most.DisplayName, Hint = most == null ? "还没有人被你启动过" : most.LaunchCount + " 次" });
            data.Metrics.Add(new ReportMetric { Label = "最久没动", Value = stalest == null ? "—" : stalest.DisplayName, Hint = stalest == null ? "" : Util.Format.Relative(stalest.LastLaunchUtc) });
            data.Metrics.Add(new ReportMetric { Label = "最近入库", Value = newest == null ? "—" : newest.DisplayName, Hint = newest == null ? "" : newest.AddedUtc.ToString("yyyy-MM-dd") });
            data.Metrics.Add(new ReportMetric { Label = "收藏总体积", Value = Util.Format.HumanSize(libraryBytes), Hint = "只算还在的文件" });
            data.Metrics.Add(new ReportMetric
            {
                Label = "本软件体积",
                Value = Util.Format.HumanSize(appBytes),
                Hint = libraryBytes > 0 && appBytes > 0 ? "约等于收藏的 1/" + Math.Max(1, libraryBytes / Math.Max(1, appBytes)) : "轻量得不像话"
            });

            var d = data.Diagnoses;
            if (total == 0)
            {
                d.Add("你还没收藏任何启动器。这很好，你是本软件唯一治好的病人。");
                d.Add("等你装了第一个启动器，欢迎回来复诊。");
            }
            else
            {
                if (total >= 8) d.Add("你收藏了 " + total + " 个启动器。这不是收藏，这是囤积。");
                if (total >= 4 && used <= 2) d.Add("其中只有 " + used + " 个被你真正启动过，剩下 " + (total - used) + " 个正在硬盘里安静地氧化。");
                if (missing > 0) d.Add("有 " + missing + " 条记录的路径已经失效。启动器跑路了，记录还在——像极了爱情。");
                if (most != null) d.Add("「" + most.DisplayName + "」被你启动了 " + most.LaunchCount + " 次，它大概是唯一记得你的那个。");
                if (stalest != null && stalest.LaunchCount > 0) d.Add("「" + stalest.DisplayName + "」最后一次启动是在 " + Util.Format.Relative(stalest.LastLaunchUtc) + "，它可能已经开始怀疑自己的存在意义。");
                if (never > 0) d.Add("有 " + never + " 个启动器从加入以来一次都没被启动过。它们只是你的收藏品。");
                if (jarCount > 0) d.Add("其中 " + jarCount + " 个是 Java 程序。也就是说，你的机器上至少有一个 java.exe 正在某个角落等着你。");
                if (libraryBytes > 0) d.Add("你收藏的启动器一共占了 " + Util.Format.HumanSize(libraryBytes) + "，而本软件只有 " + Util.Format.HumanSize(appBytes) + "。");
                d.Add(total >= 4 ? "医师建议：保留 1 个，卸载 " + (total - 1) + " 个，然后去玩游戏，而不是继续看启动器。" : "医师建议：问题不大，继续保持。");
                d.Add("本软件支持一键启动全部启动器。这不是功能，这是行为艺术。");
            }

            data.PlainText = BuildPlainText(data, total, alive, used, missing, sumLaunches, libraryBytes, appBytes);
            return data;
        }

        private static string BuildPlainText(ReportData data, int total, int alive, int used, int missing, int launches, long libBytes, long appBytes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("启动器体检报告");
            sb.AppendLine("编号：" + data.ReportNo + "    日期：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine(new string('─', 46));
            sb.AppendLine("健康评分：" + data.Score + " / 100    " + data.ScoreTitle);
            sb.AppendLine(data.ScoreComment);
            sb.AppendLine(new string('─', 46));
            sb.AppendLine("收录启动器：" + total + " 个（文件还在 " + alive + " 个，失联 " + missing + " 个）");
            sb.AppendLine("累计启动：" + launches + " 次");
            sb.AppendLine("收藏总体积：" + Util.Format.HumanSize(libBytes) + "    本软件体积：" + Util.Format.HumanSize(appBytes));
            sb.AppendLine(new string('─', 46));
            sb.AppendLine("诊断意见：");
            for (int i = 0; i < data.Diagnoses.Count; i++)
            {
                sb.AppendLine("  " + (i + 1) + ". " + data.Diagnoses[i]);
            }
            sb.AppendLine(new string('─', 46));
            sb.AppendLine("医师签名：" + data.Signature);
            sb.AppendLine("（本报告由启动器启动器生成，不构成任何医疗建议，也不构成任何卸载建议。）");
            return sb.ToString();
        }
    }
}
