using System.Windows;
using System.Windows.Controls;

namespace MiniWeather;

public sealed class CityPicker : Window
{
    private readonly LocationService locations;
    private readonly TextBox query = new() { Margin = new Thickness(0, 0, 8, 0), FontSize = 15 };
    private readonly ListBox results = new() { Margin = new Thickness(0, 12, 0, 0), Height = 220, DisplayMemberPath = nameof(City.DisplayName) };
    private readonly TextBlock status = new() { Margin = new Thickness(0, 8, 0, 0), Text = "输入至少两个字符搜索城市" };
    public City? SelectedCity { get; private set; }

    public CityPicker(LocationService locations)
    {
        this.locations = locations;
        Title = "选择城市 · MiniWeather"; Width = 390; Height = 450;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var outer = new StackPanel { Margin = new Thickness(20) };
        outer.Children.Add(new TextBlock { Text = "选择天气城市", FontSize = 21, FontWeight = FontWeights.SemiBold });
        var searchRow = new DockPanel { Margin = new Thickness(0, 18, 0, 0) };
        var button = new Button { Content = "搜索", MinWidth = 70, Padding = new Thickness(8, 5, 8, 5) };
        DockPanel.SetDock(button, Dock.Right); searchRow.Children.Add(button); searchRow.Children.Add(query);
        outer.Children.Add(searchRow); outer.Children.Add(status); outer.Children.Add(results);
        Content = outer;
        button.Click += async (_, _) => await SearchAsync();
        query.KeyDown += async (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) await SearchAsync(); };
        results.MouseDoubleClick += (_, _) => Select();
        var select = new Button { Content = "使用所选城市", Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(8) };
        select.Click += (_, _) => Select(); outer.Children.Add(select);
    }

    private async Task SearchAsync()
    {
        status.Text = "正在搜索…";
        try
        {
            var cities = await locations.SearchAsync(query.Text);
            results.ItemsSource = cities;
            status.Text = cities.Count == 0 ? "没有找到城市，请尝试其他名称" : $"找到 {cities.Count} 个城市";
        }
        catch (Exception ex) { status.Text = "搜索失败：" + ex.Message; }
    }
    private void Select()
    {
        if (results.SelectedItem is not City city) return;
        SelectedCity = city; DialogResult = true;
    }
}
