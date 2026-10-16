using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MiniWeather;

public sealed class DesktopWindowHost
{
    private const int GwlExstyle = -20, WsExToolwindow = 0x80, WsExNoactivate = 0x08000000;
    private const uint SmtoAbortifhung = 2;
    private readonly Window window;
    private IntPtr workerParent;
    public bool Embedded { get; private set; }
    public bool WindowAlive
    {
        get
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            return hwnd != IntPtr.Zero && IsWindow(hwnd);
        }
    }

    public DesktopWindowHost(Window window)
    {
        this.window = window;
        window.SourceInitialized += (_, _) => Attach();
    }

    public void Attach()
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlExstyle, new IntPtr(style | WsExToolwindow | WsExNoactivate));
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SmtoAbortifhung, 1000, out _);
        IntPtr worker = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            var defView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero) return true;
            worker = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return false;
        }, IntPtr.Zero);
        // WorkerW is undocumented Shell behavior. Verify the parent and fall back when unavailable.
        if (worker != IntPtr.Zero)
        {
            SetLastError(0);
            SetParent(hwnd, worker);
            Embedded = GetParent(hwnd) == worker;
            if (Embedded) workerParent = worker;
        }
        // Without WorkerW, leave the ordinary non-topmost window visible.
    }

    public void KeepBehind()
    {
        var current = new WindowInteropHelper(window).Handle;
        if (Embedded && (workerParent == IntPtr.Zero || !IsWindow(workerParent) || GetParent(current) != workerParent))
        {
            Embedded = false;
            Attach();
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? windowName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? windowName);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("kernel32.dll")] private static extern void SetLastError(uint error);
}
