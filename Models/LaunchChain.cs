using System;
using System.Collections.Generic;
using System.Linq;

namespace LauncherLauncher.Models
{
    /// <summary>接力链里的一步。</summary>
    public class ChainStep
    {
        public string LauncherId { get; set; }
        public string Note { get; set; }

        public ChainStep() { }

        public ChainStep(string launcherId)
        {
            LauncherId = launcherId;
        }
    }

    /// <summary>“启动器接力”：一串按顺序启动的启动器。</summary>
    public class LaunchChain
    {
        public const int ModeWaitExit = 0;      // 等上一个退出，再启动下一个
        public const int ModeSimultaneous = 1;  // 全部同时启动
        public const int ModeDelay = 2;         // 间隔固定时间依次启动

        public LaunchChain()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "默认接力";
            Mode = ModeWaitExit;
            DelayMs = 1200;
            Steps = new List<ChainStep>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public int Mode { get; set; }
        public int DelayMs { get; set; }
        public List<ChainStep> Steps { get; set; }

        public string ModeText
        {
            get
            {
                switch (Mode)
                {
                    case ModeSimultaneous: return "同时启动";
                    case ModeDelay: return "间隔启动";
                    default: return "依次接力";
                }
            }
        }

        public int Count { get { return Steps == null ? 0 : Steps.Count; } }

        public LaunchChain Clone()
        {
            var c = new LaunchChain
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Name,
                Mode = Mode,
                DelayMs = DelayMs,
                Steps = (Steps ?? new List<ChainStep>()).Select(s => new ChainStep(s.LauncherId) { Note = s.Note }).ToList()
            };
            return c;
        }
    }
}
