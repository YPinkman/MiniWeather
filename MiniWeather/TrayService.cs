using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace MiniWeather;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    public TrayService(Action refresh, Action city, Action toggleLock, Action settings, Action toggleStartup, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        void Add(string text, Action action) => menu.Items.Add(text, null, (_, _) => action());
        Add("刷新天气", refresh); Add("更改城市", city); Add("锁定 / 解锁组件", toggleLock);
        Add("设置", settings); Add("开机自启（切换）", toggleStartup); menu.Items.Add(new Forms.ToolStripSeparator()); Add("退出", exit);
        icon = new Forms.NotifyIcon { Text = "MiniWeather", Icon = Drawing.SystemIcons.Information, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => settings();
    }
    public void Dispose() { icon.Visible = false; icon.Dispose(); }
    public void ReaddIcon() { icon.Visible = false; icon.Visible = true; }
}
