using Microsoft.Win32;
using System.Diagnostics;

namespace MiniWeather;

public sealed class StartupService(bool windowed = false)
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue("MiniWeather") is string;
    }
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true) ?? throw new InvalidOperationException("无法打开用户启动项注册表。");
        var executable = System.IO.Path.Combine(AppContext.BaseDirectory, "MiniWeather.exe");
        if (enabled) key.SetValue("MiniWeather", $"\"{executable}\"{(windowed ? " --windowed" : "")}");
        else key.DeleteValue("MiniWeather", false);
    }
}
