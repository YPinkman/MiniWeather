using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MiniWeather;

public sealed class ShellEventHost : IDisposable
{
    private readonly HwndSource source;
    private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    public event Action? ShellRestarted;

    public ShellEventHost()
    {
        source = new HwndSource(new HwndSourceParameters("MiniWeather.ShellEvents")
        { Width = 0, Height = 0, WindowStyle = 0, ExtendedWindowStyle = 0x80 });
        source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == taskbarCreated) ShellRestarted?.Invoke();
        return IntPtr.Zero;
    }

    public void Dispose() { source.RemoveHook(WndProc); source.Dispose(); }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);
}
