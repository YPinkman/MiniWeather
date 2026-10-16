using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace MiniWeather;

public static class DisplayPlacement
{
    private const uint SwpNoSize = 1, SwpNoZOrder = 4, SwpNoActivate = 0x10;
    private const int Margin = 28;

    public static void Restore(Window window, AppSettings settings)
    {
        if (settings.PixelLeft is int x && settings.PixelTop is int y &&
            Forms.Screen.AllScreens.Any(s => s.DeviceName == settings.Monitor))
            Move(window, x, y);
        EnsureVisible(window, settings.Monitor);
    }

    public static void EnsureVisible(Window window, string? preferredMonitor)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return;
        var width = r.Right - r.Left;
        var height = r.Bottom - r.Top;
        if (width <= 0 || height <= 0) return;
        var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        if (Forms.Screen.AllScreens.Any(s =>
        {
            var visible = Rectangle.Intersect(rect, s.WorkingArea);
            return visible.Width >= Math.Min(width, s.WorkingArea.Width) / 2 &&
                   visible.Height >= Math.Min(height, s.WorkingArea.Height) / 2;
        })) return;
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == preferredMonitor) ?? Forms.Screen.PrimaryScreen;
        if (screen is null) return;
        var work = screen.WorkingArea;
        var x = width >= work.Width ? work.Left : Math.Clamp(r.Left, work.Left, work.Right - width);
        var y = height >= work.Height ? work.Top : Math.Clamp(r.Top, work.Top, work.Bottom - height);
        Move(window, x, y);
    }

    public static void Reset(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var screen = Forms.Screen.PrimaryScreen;
        if (hwnd == IntPtr.Zero || screen is null || !GetWindowRect(hwnd, out var r)) return;
        Move(window, screen.WorkingArea.Right - (r.Right - r.Left) - Margin,
            screen.WorkingArea.Bottom - (r.Bottom - r.Top) - Margin);
    }

    public static void Remember(Window window, AppSettings settings)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return;
        settings.Left = window.Left;
        settings.Top = window.Top;
        settings.PixelLeft = r.Left;
        settings.PixelTop = r.Top;
        settings.Monitor = Forms.Screen.FromHandle(hwnd).DeviceName;
    }

    private static void Move(Window window, int x, int y)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var parent = GetParent(hwnd);
        if (parent != IntPtr.Zero)
        {
            var point = new NativePoint { X = x, Y = y };
            MapWindowPoints(IntPtr.Zero, parent, ref point, 1);
            x = point.X; y = point.Y;
        }
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref NativePoint point, uint count);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
