using System.Windows;
using System.Windows.Controls;

namespace MiniWeather;

public sealed class SettingsView : Window
{
    private readonly SettingsService settings;
    private readonly StartupService startup;
    private readonly Action chooseCity;
    private readonly Action resetPosition;
    private bool ready;

    public SettingsView(SettingsService settings, StartupService startup, Action chooseCity, Action resetPosition)
    {
        this.settings = settings; this.startup = startup; this.chooseCity = chooseCity; this.resetPosition = resetPosition;
        Title = "MiniWeather 设置"; Width = 400; Height = 500; MinWidth = 360; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(22) };
        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Content = scroll;
        Header(panel, "外观");
        var theme = new ComboBox { Margin = new Thickness(0, 5, 0, 10), ItemsSource = new[] { "跟随系统", "浅色", "深色" }, SelectedIndex = settings.Value.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 } };
        panel.Children.Add(theme);
        theme.SelectionChanged += (_, _) => { if (!ready) return; settings.Value.Theme = theme.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" }; settings.Save(); };
        SliderRow(panel, "背景不透明度", 0, 100, settings.Value.Opacity, value => { settings.Value.Opacity = value; settings.Save(); });
        SliderRow(panel, "组件尺寸", 180, 240, settings.Value.Size, value => { settings.Value.Size = value; settings.Save(); });
        Check(panel, "启用天气动效", settings.Value.Animations, value => { settings.Value.Animations = value; settings.Save(); });
        Header(panel, "天气");
        var cityLabel = new TextBlock { Text = "当前城市：" + (settings.Value.City?.DisplayName ?? "未设置"), Margin = new Thickness(0, 5, 0, 7) };
        panel.Children.Add(cityLabel);
        Button(panel, "搜索并更改城市", () => { chooseCity(); cityLabel.Text = "当前城市：" + (settings.Value.City?.DisplayName ?? "未设置"); });
        SliderRow(panel, "刷新间隔（分钟）", 15, 360, settings.Value.RefreshMinutes, value => { settings.Value.RefreshMinutes = value; settings.Save(); });
        Header(panel, "桌面行为");
        Check(panel, "开机自启", startup.IsEnabled(), value =>
        {
            try { startup.SetEnabled(value); settings.Value.Autostart = value; settings.Save(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "启动项设置失败"); }
        });
        Check(panel, "锁定组件位置", settings.Value.Locked, value => { settings.Value.Locked = value; settings.Save(); });
        Button(panel, "恢复默认位置", resetPosition);
        ready = true;
    }

    private static void Header(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 5) });
    private static void Button(Panel panel, string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 2, 0, 7) };
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    private static void Check(Panel panel, string text, bool initial, Action<bool> changed)
    {
        var box = new CheckBox { Content = text, IsChecked = initial, Margin = new Thickness(0, 7, 0, 7) };
        box.Checked += (_, _) => changed(true); box.Unchecked += (_, _) => changed(false); panel.Children.Add(box);
    }
    private static void SliderRow(Panel panel, string text, int min, int max, int initial, Action<int> changed)
    {
        var label = new TextBlock { Text = $"{text}：{initial}", Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(label);
        var slider = new Slider { Minimum = min, Maximum = max, Value = initial, TickFrequency = text.Contains("间隔") ? 15 : 1,
            IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 6) };
        slider.ValueChanged += (_, _) => { var n = (int)slider.Value; label.Text = $"{text}：{n}"; changed(n); };
        panel.Children.Add(slider);
    }
}
