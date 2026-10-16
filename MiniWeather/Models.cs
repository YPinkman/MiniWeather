namespace MiniWeather;

public sealed record City(string Name, string? Region, string? Country, double Latitude, double Longitude, string TimeZone)
{
    public string DisplayName => string.Join(" · ", new[] { Name, Region, Country }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public enum WeatherOrigin { HistoricalModel, ForecastModel, Mock }

public sealed record DayWeather(DateOnly Date, double High, double Low, int Code, WeatherOrigin Origin, double WindSpeed = 0);

public sealed record WeatherSnapshot(City City, List<DayWeather> Days, DateTimeOffset UpdatedAt, double? CurrentTemperature = null);

public sealed class AppSettings
{
    public City? City { get; set; }
    public string Theme { get; set; } = "System";
    public int Opacity { get; set; } = 80;
    public int Size { get; set; } = 210;
    public bool Animations { get; set; } = true;
    public int RefreshMinutes { get; set; } = 60;
    public bool Locked { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }
    public int? PixelLeft { get; set; }
    public int? PixelTop { get; set; }
    public string? Monitor { get; set; }
    public bool Autostart { get; set; }
}

public static class WeatherCodes
{
    public static string Description(DayWeather day) => IsWindy(day.Code, day.WindSpeed) ? "大风" : Description(day.Code);
    public static string Kind(DayWeather day) => Kind(day.Code, day.WindSpeed);
    public static string Kind(int code, double windSpeed) => IsWindy(code, windSpeed) ? "wind" : Kind(code);
    private static bool IsWindy(int code, double windSpeed) => code is >= 0 and <= 3 && windSpeed >= 40;

    public static string Description(int code) => code switch
    {
        0 => "晴", 1 or 2 => "多云", 3 => "阴", 45 or 48 => "雾",
        51 or 53 or 55 or 56 or 57 => "毛毛雨", 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "雨",
        71 or 73 or 75 or 77 or 85 or 86 => "雪", 95 or 96 or 97 or 99 => "雷雨", _ => "未知"
    };

    public static string Kind(int code) => code switch
    {
        0 => "sun", 1 or 2 => "partly", 3 => "cloud", 45 or 48 => "fog",
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "rain",
        71 or 73 or 75 or 77 or 85 or 86 => "snow", 95 or 96 or 97 or 99 => "storm", _ => "cloud"
    };
}
