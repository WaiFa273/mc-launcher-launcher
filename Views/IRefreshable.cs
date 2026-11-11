using System;

namespace LauncherLauncher.Views
{
    /// <summary>切换到这一页时会被调用一次，页面自己去取最新数据。</summary>
    public interface IRefreshable
    {
        void Refresh();
    }
}
