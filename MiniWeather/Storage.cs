using System.IO;
using System.Text.Json;

namespace MiniWeather;

public sealed class SettingsService
{
    public string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniWeather");
    private string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    public AppSettings Value { get; private set; } = new();
    public event Action? Changed;

    public SettingsService()
    {
        Directory.CreateDirectory(DirectoryPath);
        try { Value = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch (IOException) { }
        catch (JsonException) { }
        Value.Opacity = Math.Clamp(Value.Opacity, 0, 100);
        Value.Size = Math.Clamp(Value.Size, 180, 240);
        Value.RefreshMinutes = Math.Clamp(Value.RefreshMinutes, 15, 360);
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, SettingsPath, true);
        Changed?.Invoke();
    }
}

public sealed class WeatherCache(SettingsService settings)
{
    private string PathName => Path.Combine(settings.DirectoryPath, "weather.json");
    public WeatherSnapshot? Load(City city)
    {
        try
        {
            var data = JsonSerializer.Deserialize<WeatherSnapshot>(File.ReadAllText(PathName));
            if (data?.City.Latitude == city.Latitude && data.City.Longitude == city.Longitude && data.Days.Count == 5) return data;
        }
        catch (IOException) { }
        catch (JsonException) { }
        return null;
    }

    public void Save(WeatherSnapshot snapshot)
    {
        var temp = PathName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot));
        File.Move(temp, PathName, true);
    }
}
