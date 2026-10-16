using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Documents;

namespace MiniWeather;

public sealed class WidgetView : Window
{
    private readonly SettingsService settings;
    private readonly bool forceMockMotion;
    private readonly Border card;
    private readonly Grid root;
    private WeatherSnapshot? snapshot;
    private string statusText = "正在获取天气…";
    private bool hasDrawnWeather;
    private bool motionAllowed = true;
    public event Action? PositionChanged;
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;

    public WidgetView(SettingsService settings, bool windowed = false, bool forceMockMotion = false)
    {
        this.settings = settings;
        this.forceMockMotion = forceMockMotion;
        Width = Height = settings.Value.Size;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = windowed;
        Topmost = false;
#if PREVIEW
        ShowInTaskbar = true;
        Topmost = true;
#endif
        Title = "MiniWeather";
        Left = settings.Value.Left ?? SystemParameters.WorkArea.Right - Width - 28;
        Top = settings.Value.Top ?? SystemParameters.WorkArea.Bottom - Height - 28;
        card = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10, 14, 10),
            BorderThickness = new Thickness(1) };
        root = new Grid();
        card.Child = root;
        Content = card;
        MouseLeftButtonDown += (_, e) =>
        {
            if (settings.Value.Locked || e.ClickCount > 1) return;
            try { DragMove(); PositionChanged?.Invoke(); } catch (InvalidOperationException) { }
        };
        MouseRightButtonUp += (_, _) => ShowMenu();
        MouseEnter += (_, _) => card.Opacity = 1;
        MouseLeave += (_, _) => card.Opacity = .96;
        settings.Changed += ApplySettings;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        IsVisibleChanged += (_, _) => { if (snapshot is not null) Draw(); };
        Closed += (_, _) => DisposeBindings();
        ApplySettings();
    }

    public void DisposeBindings()
    {
        settings.Changed -= ApplySettings;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
    }

    private void OnSystemPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (settings.Value.Theme == "System") Dispatcher.BeginInvoke(ApplySettings);
    }

    public void SetWeather(WeatherSnapshot data, bool stale = false)
    {
        snapshot = data;
        ApplyPalette();
        Draw();
        if (hasDrawnWeather && ShouldAnimate())
            root.BeginAnimation(OpacityProperty, new DoubleAnimation(.65, 1, TimeSpan.FromMilliseconds(450)));
        hasDrawnWeather = true;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(data.City.TimeZone);
        var stamp = TimeZoneInfo.ConvertTime(data.UpdatedAt, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        ToolTip = $"{data.City.DisplayName}\n最后成功更新：{stamp} ({data.City.TimeZone})" +
                  (data.CurrentTemperature is not null ? "\n当前温度为天气模型估算值，非实测站点读数" : "\n当前温度暂不可用") +
                  (stale ? "\n当前显示缓存数据" : "") +
                  (data.Days[0].Origin == WeatherOrigin.HistoricalModel ? "\n昨天：历史再分析模型数据，非实测站点记录" : "");
    }

    public void SetError(string message)
    {
        snapshot = null;
        statusText = message;
        ToolTip = message;
        ApplyPalette();
        Draw();
    }

    public void SetMotionAllowed(bool allowed)
    {
        if (motionAllowed == allowed) return;
        motionAllowed = allowed;
        if (snapshot is not null) Draw();
    }

    private void ApplySettings()
    {
        Width = Height = settings.Value.Size;
        var dark = settings.Value.Theme == "Dark" || (settings.Value.Theme == "System" && IsSystemDark());
        card.Tag = dark;
        ApplyPalette();
        Draw();
    }

    private void ApplyPalette()
    {
        var dark = card.Tag is true;
        var alpha = (byte)Math.Round(settings.Value.Opacity * 2.55);
        var accent = AccentColor(snapshot is null ? "partly" : WeatherCodes.Kind(snapshot.Days[1]), dark);
        var baseColor = dark ? Color.FromRgb(28, 38, 55) : Color.FromRgb(247, 249, 253);
        var top = Mix(baseColor, accent, dark ? .17 : .13);
        var bottom = Mix(baseColor, accent, dark ? .06 : .04);
        card.Background = new LinearGradientBrush(Color.FromArgb(alpha, top.R, top.G, top.B),
            Color.FromArgb(alpha, bottom.R, bottom.G, bottom.B), new Point(0, 0), new Point(1, 1));
        card.BorderBrush = new SolidColorBrush(dark ? Color.FromArgb((byte)(alpha / 3), 255, 255, 255)
            : Color.FromArgb((byte)(alpha / 4), 70, 95, 120));
    }

    private static Color Mix(Color a, Color b, double amount) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * amount),
        (byte)Math.Round(a.G + (b.G - a.G) * amount),
        (byte)Math.Round(a.B + (b.B - a.B) * amount));

    private static Color AccentColor(string kind, bool dark) => (kind, dark) switch
    {
        ("sun", false) => Color.FromRgb(184, 103, 28),
        ("sun", true) => Color.FromRgb(255, 197, 108),
        ("rain" or "storm", false) => Color.FromRgb(37, 111, 180),
        ("rain" or "storm", true) => Color.FromRgb(117, 188, 245),
        ("snow" or "fog", false) => Color.FromRgb(40, 125, 151),
        ("snow" or "fog", true) => Color.FromRgb(142, 214, 226),
        ("wind", false) => Color.FromRgb(28, 131, 125),
        ("wind", true) => Color.FromRgb(114, 216, 196),
        (_, false) => Color.FromRgb(67, 109, 173),
        _ => Color.FromRgb(153, 189, 242)
    };

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) == 0;
        }
        catch { return false; }
    }

    private void Draw()
    {
        root.Children.Clear(); root.RowDefinitions.Clear();
        var dark = card.Tag is true;
        var ink = new SolidColorBrush(dark ? Colors.White : Color.FromRgb(27, 48, 70));
        var secondary = new SolidColorBrush(dark ? Color.FromRgb(212, 222, 235) : Color.FromRgb(76, 99, 120));
        var kind = snapshot is null ? "partly" : WeatherCodes.Kind(snapshot.Days[1]);
        var accent = new SolidColorBrush(AccentColor(kind, dark));
        var shadow = new DropShadowEffect { Color = dark ? Colors.Black : Colors.White, BlurRadius = 3, ShadowDepth = 1, Opacity = .75 };
        if (snapshot is null)
        {
            root.Children.Add(new TextBlock { Text = settings.Value.City?.Name ?? "MiniWeather", FontSize = 13,
                FontWeight = FontWeights.SemiBold, Foreground = ink, Effect = shadow });
            root.Children.Add(new TextBlock { Text = statusText, FontSize = 12, Foreground = secondary,
                TextWrapping = TextWrapping.Wrap, VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Effect = shadow });
            return;
        }
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
        var header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var city = Label(snapshot.City.Name, 12, ink, FontWeights.SemiBold, shadow); city.TextTrimming = TextTrimming.CharacterEllipsis;
        var todayBadge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 1, 5, 1),
            Background = new SolidColorBrush(Color.FromArgb(dark ? (byte)45 : (byte)24,
                AccentColor(kind, dark).R, AccentColor(kind, dark).G, AccentColor(kind, dark).B)),
            Child = Label("今天", 10, accent, FontWeights.SemiBold, shadow) };
        Grid.SetColumn(todayBadge, 1); header.Children.Add(city); header.Children.Add(todayBadge); Grid.SetRow(header, 0); root.Children.Add(header);
        var main = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        var today = snapshot.Days[1];
        var compact = settings.Value.Size <= 190;
        var iconSize = compact ? 50 : 60;
        var mainIcon = new WeatherIcon { Code = today.Code, WindSpeed = today.WindSpeed, Width = iconSize, Height = iconSize,
            RenderTransformOrigin = new Point(.5, .5) };
        var scene = new Grid { Width = iconSize, Height = iconSize, Margin = new Thickness(0, 0, compact ? 3 : 6, 0), ClipToBounds = true };
        var particles = new Canvas { Width = iconSize, Height = iconSize, IsHitTestVisible = false };
        var accentColor = AccentColor(kind, dark);
        scene.Children.Add(new Ellipse { Width = iconSize - 8, Height = iconSize - 8, IsHitTestVisible = false,
            Fill = new SolidColorBrush(Color.FromArgb(dark ? (byte)42 : (byte)35,
                accentColor.R, accentColor.G, accentColor.B)) });
        scene.Children.Add(mainIcon); scene.Children.Add(particles);
        main.Children.Add(scene);
        AnimateIcon(mainIcon, today, particles);
        var mainText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var currentRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        currentRow.Children.Add(new TextBlock { Text = "当前", FontSize = 10, Foreground = secondary,
            FontFamily = new FontFamily("Microsoft YaHei UI"), Effect = shadow, Margin = new Thickness(0, 0, 4, compact ? 5 : 7),
            VerticalAlignment = VerticalAlignment.Bottom });
        currentRow.Children.Add(Label(snapshot.CurrentTemperature is double temperature ? $"{temperature:0}°" : "--°",
            compact ? 25 : 30, accent, FontWeights.Bold, shadow));
        mainText.Children.Add(currentRow);
        mainText.Children.Add(Label(WeatherCodes.Description(today), compact ? 10 : 11, ink, FontWeights.SemiBold, shadow));
        var highLow = new TextBlock { FontSize = compact ? 9 : 10, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Microsoft YaHei UI"), Effect = shadow, VerticalAlignment = VerticalAlignment.Center };
        highLow.Inlines.Add(new Run($"高{today.High:0}°") { Foreground = new SolidColorBrush(dark ? Color.FromRgb(255, 194, 130) : Color.FromRgb(179, 91, 48)) });
        highLow.Inlines.Add(new Run("  /  ") { Foreground = secondary });
        highLow.Inlines.Add(new Run($"低{today.Low:0}°") { Foreground = new SolidColorBrush(dark ? Color.FromRgb(146, 208, 246) : Color.FromRgb(56, 117, 177)) });
        mainText.Children.Add(highLow);
        main.Children.Add(mainText); Grid.SetRow(main, 1); root.Children.Add(main);
        var divider = new Border { Background = new SolidColorBrush(dark ? Color.FromArgb(65, 255, 255, 255) : Color.FromArgb(45, 30, 50, 70)) };
        Grid.SetRow(divider, 2); root.Children.Add(divider);
        var small = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        small.RowDefinitions.Add(new RowDefinition()); small.RowDefinitions.Add(new RowDefinition());
        small.ColumnDefinitions.Add(new ColumnDefinition()); small.ColumnDefinitions.Add(new ColumnDefinition());
        var other = new[] { snapshot.Days[0], snapshot.Days[2], snapshot.Days[3], snapshot.Days[4] };
        for (var i = 0; i < 4; i++)
        {
            var day = other[i];
            var label = i switch { 0 => "昨天", 1 => "明天", 2 => "后天", _ => day.Date.ToString("ddd", new CultureInfo("zh-CN")) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(Label(label, compact ? 9 : 10, secondary, FontWeights.Normal, shadow, compact ? 22 : 27));
            row.Children.Add(new WeatherIcon { Code = day.Code, WindSpeed = day.WindSpeed,
                Width = compact ? 18 : 22, Height = compact ? 18 : 22,
                Margin = new Thickness(0, 0, compact ? 1 : 2, 0) });
            row.Children.Add(Label($"{day.High:0}/{day.Low:0}", compact ? 9 : 10, ink, FontWeights.SemiBold, shadow));
            Grid.SetRow(row, i / 2); Grid.SetColumn(row, i % 2); small.Children.Add(row);
        }
        Grid.SetRow(small, 3); root.Children.Add(small);
    }

    private static TextBlock Label(string text, double size, Brush color, FontWeight weight, Effect shadow, double? width = null) =>
        new() { Text = text, FontSize = size, Foreground = color, FontWeight = weight, FontFamily = new FontFamily("Microsoft YaHei UI"),
            VerticalAlignment = System.Windows.VerticalAlignment.Center, Effect = shadow, Width = width ?? double.NaN };

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        void Add(string title, Action action) { var item = new MenuItem { Header = title }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Add("刷新天气", () => RefreshRequested?.Invoke());
        Add("设置", () => SettingsRequested?.Invoke());
        menu.IsOpen = true;
    }

    private bool ShouldAnimate()
    {
        if (!settings.Value.Animations || !motionAllowed || !IsVisible || (!forceMockMotion && !SystemParameters.ClientAreaAnimation)) return false;
        if (forceMockMotion) return true;
        var power = System.Windows.Forms.SystemInformation.PowerStatus;
        return power.PowerLineStatus != System.Windows.Forms.PowerLineStatus.Offline || power.BatteryLifePercent > .3;
    }

    private void AnimateIcon(WeatherIcon icon, DayWeather day, Canvas particles)
    {
        if (!ShouldAnimate()) return;
        var kind = WeatherCodes.Kind(day);
        if (kind is "sun" or "storm" or "fog")
        {
            var animation = new DoubleAnimation(kind == "storm" ? .86 : .78, 1,
                TimeSpan.FromSeconds(kind == "storm" ? 6 : 3))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            Timeline.SetDesiredFrameRate(animation, 15);
            icon.BeginAnimation(OpacityProperty, animation);
        }
        else if (kind is "cloud" or "partly")
        {
            var move = new TranslateTransform();
            icon.RenderTransform = move;
            var animation = new DoubleAnimation(-1.5, 1.5, TimeSpan.FromSeconds(4))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            Timeline.SetDesiredFrameRate(animation, 15);
            move.BeginAnimation(TranslateTransform.XProperty, animation);
        }
        else if (kind is "rain" or "snow" or "wind")
        {
            for (var i = 0; i < 3; i++)
            {
                FrameworkElement mark;
                if (kind == "snow")
                    mark = new Ellipse { Width = 3.5, Height = 3.5, Fill = Brushes.White,
                        Stroke = new SolidColorBrush(Color.FromRgb(125, 174, 205)), StrokeThickness = .6 };
                else
                    mark = new Line { X1 = 0, Y1 = 0, X2 = kind == "wind" ? 11 : -2, Y2 = kind == "wind" ? 0 : 7,
                        Stroke = new SolidColorBrush(kind == "wind" ? Color.FromRgb(148, 195, 213) : Color.FromRgb(81, 166, 221)),
                        StrokeThickness = kind == "wind" ? 1.8 : 2, StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round };
                Canvas.SetLeft(mark, kind == "wind" ? 15 : 21 + i * 12);
                Canvas.SetTop(mark, kind == "wind" ? 20 + i * 13 : 48);
                var move = new TranslateTransform(); mark.RenderTransform = move;
                var duration = TimeSpan.FromSeconds(kind == "snow" ? 3.6 : kind == "wind" ? 2.8 : 1.4);
                var animation = new DoubleAnimation(kind == "wind" ? -5 : -4, kind == "wind" ? 10 : 8, duration)
                { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(i * .4),
                    EasingFunction = new SineEase() };
                Timeline.SetDesiredFrameRate(animation, 15);
                move.BeginAnimation(kind == "wind" ? TranslateTransform.XProperty : TranslateTransform.YProperty, animation);
                particles.Children.Add(mark);
            }
        }
    }
}
