using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;

namespace MiniWeather;

public sealed class App : Application
{
    private Mutex? instance;
    private bool ownsInstance;
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private SettingsService settings = null!;
    private WeatherCache cache = null!;
    private LocationService locations = null!;
    private WeatherService weather = null!;
    private StartupService startup = null!;
    private WidgetView widget = null!;
    private DesktopWindowHost host = null!;
    private TrayService tray = null!;
    private ShellEventHost shellEvents = null!;
    private EventHandler? displayChanged;
    private SettingsView? settingsWindow;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly DispatcherTimer refreshTimer = new();
    private readonly DispatcherTimer hostTimer = new();
    private int failures;
    private bool mock;
    private bool windowed;
    private bool forceMockMotion;
    private int mockCode = 2;
    private double mockWind;
    private bool refreshPending;

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);
            var app = new App();
#if PREVIEW
            app.mock = true;
#else
            app.windowed = args.Contains("--windowed");
            var mockArg = args.FirstOrDefault(x => x == "--mock" || x.StartsWith("--mock=", StringComparison.OrdinalIgnoreCase));
            app.mock = mockArg is not null;
            app.forceMockMotion = app.mock && args.Contains("--force-motion-preview");
            (app.mockCode, app.mockWind) = (mockArg?.Split('=', 2).ElementAtOrDefault(1)?.ToLowerInvariant()) switch
            {
                "sun" => (0, 0), "cloud" => (3, 0), "fog" => (45, 0), "rain" => (61, 0),
                "snow" => (71, 0), "wind" => (3, 48), "storm" => (95, 0), _ => (2, 0)
            };
#endif
            app.DispatcherUnhandledException += (_, e) =>
            {
                LogCrash(e.Exception);
                MessageBox.Show(e.Exception.Message, "MiniWeather 错误");
                e.Handled = true;
                app.Shutdown();
            };
            app.Run();
        }
        catch (Exception ex) { LogCrash(ex); }
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniWeather");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "error.log"), $"{DateTimeOffset.Now:O}\n{ex}\n\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#if PREVIEW
        instance = new Mutex(true, @"Local\MiniWeatherPreview", out var created);
#else
        instance = new Mutex(true, @"Local\MiniWeather", out var created);
#endif
        ownsInstance = created;
        if (!created) { Shutdown(); return; }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        settings = new SettingsService(); cache = new WeatherCache(settings);
#if PREVIEW
        settings.Value.Size = 180;
        settings.Value.Opacity = 80;
#endif
        locations = new LocationService(client); weather = new WeatherService(client); startup = new StartupService(windowed);
        if (settings.Value.City is null && !mock)
        {
            var picker = new CityPicker(locations);
            if (picker.ShowDialog() != true || picker.SelectedCity is null) { Shutdown(); return; }
            settings.Value.City = picker.SelectedCity; settings.Save();
        }
        if (mock && settings.Value.City is null) settings.Value.City = new City("预览城市", null, null, 0, 0, "UTC");
        tray = new TrayService(() => _ = RefreshAsync(true), ChooseCity, ToggleLock, ShowSettings, ToggleStartup, () => Shutdown());
        shellEvents = new ShellEventHost();
        shellEvents.ShellRestarted += OnShellRestarted;
        CreateWidget();
        settings.Changed += () =>
        {
            refreshTimer.Interval = TimeSpan.FromMinutes(settings.Value.RefreshMinutes);
            DisplayPlacement.EnsureVisible(widget, settings.Value.Monitor);
        };
        displayChanged = (_, _) => _ = Dispatcher.BeginInvoke(new Action(() => DisplayPlacement.EnsureVisible(widget, settings.Value.Monitor)));
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += displayChanged;
        refreshTimer.Tick += async (_, _) => await RefreshAsync();
        refreshTimer.Interval = TimeSpan.FromMinutes(settings.Value.RefreshMinutes);
        refreshTimer.Start();
        hostTimer.Interval = TimeSpan.FromSeconds(20);
        hostTimer.Tick += (_, _) => CheckDesktopHost();
        hostTimer.Start();
        _ = RefreshAsync();
    }

    private void CreateWidget()
    {
        widget = new WidgetView(settings, windowed, forceMockMotion);
#if !PREVIEW
        if (!windowed) host = new DesktopWindowHost(widget);
#endif
        widget.SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(widget).Handle);
            source?.AddHook(WidgetWndProc);
        };
        widget.PositionChanged += SavePosition;
        widget.RefreshRequested += () => _ = RefreshAsync(true);
        widget.SettingsRequested += ShowSettings;
        widget.Show();
        DisplayPlacement.Restore(widget, settings.Value);
        if (settings.Value.City is { } city)
        {
            var stored = cache.Load(city);
            if (stored is not null) widget.SetWeather(stored, true);
            else if (mock) widget.SetWeather(MockWeatherService.Create(city, mockCode, mockWind));
        }
    }

    private IntPtr WidgetWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is 0x02E0 or 0x02E3)
            _ = Dispatcher.BeginInvoke(new Action(() => DisplayPlacement.EnsureVisible(widget, settings.Value.Monitor)));
        return IntPtr.Zero;
    }

    private async void OnShellRestarted()
    {
        await Task.Delay(1200);
        if (Dispatcher.HasShutdownStarted) return;
        await Dispatcher.InvokeAsync(() =>
        {
            tray.ReaddIcon();
            CheckDesktopHost();
        });
    }

    private void CheckDesktopHost()
    {
#if !PREVIEW
        if (!windowed && !host.WindowAlive)
        {
            widget.DisposeBindings();
            try { widget.Close(); } catch (InvalidOperationException) { }
            CreateWidget();
            _ = RefreshAsync();
        }
        else if (!windowed) host.KeepBehind();
#endif
        DisplayPlacement.EnsureVisible(widget, settings.Value.Monitor);
        widget.SetMotionAllowed(!WindowOcclusion.IsCovered(widget));
    }

    private async Task RefreshAsync(bool manual = false)
    {
        if (!await refreshGate.WaitAsync(0)) { refreshPending = true; return; }
        try
        {
            var city = settings.Value.City;
            if (city is null) return;
            if (mock) { widget.SetWeather(MockWeatherService.Create(city, mockCode, mockWind)); return; }
            var cached = cache.Load(city);
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById(city.TimeZone)).DateTime);
            if (!manual && cached is not null && cached.CurrentTemperature is not null && cached.Days[1].Date == localToday &&
                DateTimeOffset.UtcNow - cached.UpdatedAt < TimeSpan.FromMinutes(settings.Value.RefreshMinutes))
            { widget.SetWeather(cached); return; }
            var fresh = await weather.FetchAsync(city);
            if (settings.Value.City != city) { refreshPending = true; return; }
            cache.Save(fresh); widget.SetWeather(fresh); failures = 0;
            refreshTimer.Interval = TimeSpan.FromMinutes(settings.Value.RefreshMinutes);
        }
        catch (Exception)
        {
            failures = Math.Min(failures + 1, 6);
            refreshTimer.Interval = TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, failures)));
            if (settings.Value.City is { } city && cache.Load(city) is { } stored) widget.SetWeather(stored, true);
            else widget.SetError("天气获取失败，请稍后重试或右键刷新。");
        }
        finally
        {
            refreshGate.Release();
            if (refreshPending)
            {
                refreshPending = false;
                _ = Dispatcher.BeginInvoke(new Action(() => { _ = RefreshAsync(true); }));
            }
        }
    }

    private void ChooseCity()
    {
        var picker = new CityPicker(locations) { Owner = settingsWindow };
        if (picker.ShowDialog() != true || picker.SelectedCity is null) return;
        settings.Value.City = picker.SelectedCity; settings.Save();
        _ = RefreshAsync(true);
    }

    private void ShowSettings()
    {
        if (settingsWindow is { IsVisible: true }) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsView(settings, startup, ChooseCity, ResetPosition);
        settingsWindow.Show();
    }

    private void ToggleLock() { settings.Value.Locked = !settings.Value.Locked; settings.Save(); }
    private void ToggleStartup()
    {
        try { startup.SetEnabled(!startup.IsEnabled()); settings.Value.Autostart = startup.IsEnabled(); settings.Save(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "启动项设置失败"); }
    }
    private void ResetPosition()
    {
        DisplayPlacement.Reset(widget);
        SavePosition();
    }
    private void SavePosition()
    {
        DisplayPlacement.Remember(widget, settings.Value);
        settings.Save();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        refreshTimer.Stop(); hostTimer.Stop();
        if (displayChanged is not null) Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= displayChanged;
        widget?.DisposeBindings(); shellEvents?.Dispose(); tray?.Dispose(); client.Dispose();
        if (ownsInstance) instance?.ReleaseMutex();
        instance?.Dispose(); base.OnExit(e);
    }
}
