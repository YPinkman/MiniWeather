using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MiniWeather;

public static class WindowOcclusion
{
    public static bool IsCovered(Window widget)
    {
        var foreground = GetForegroundWindow();
        var hwnd = new WindowInteropHelper(widget).Handle;
        if (foreground == IntPtr.Zero || hwnd == IntPtr.Zero || foreground == hwnd || foreground == GetShellWindow()) return false;
        GetWindowThreadProcessId(foreground, out var processId);
        if (processId == Environment.ProcessId) return false;
        if (!GetWindowRect(foreground, out var top) || !GetWindowRect(hwnd, out var card)) return false;
        var a = Rectangle.FromLTRB(top.Left, top.Top, top.Right, top.Bottom);
        var b = Rectangle.FromLTRB(card.Left, card.Top, card.Right, card.Bottom);
        var overlap = Rectangle.Intersect(a, b);
        return overlap.Width >= b.Width * .8 && overlap.Height >= b.Height * .8;
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
}
