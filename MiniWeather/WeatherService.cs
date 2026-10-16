using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MiniWeather;

public sealed class LocationService(HttpClient client)
{
    public async Task<List<City>> SearchAsync(string query, CancellationToken token = default)
    {
        if (query.Trim().Length < 2) return [];
        var uri = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=10&language=zh&format=json";
        using var response = await client.GetAsync(uri, token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        if (!json.RootElement.TryGetProperty("results", out var results)) return [];
        return results.EnumerateArray().Select(x => new City(
            x.GetProperty("name").GetString() ?? "",
            x.TryGetProperty("admin1", out var region) ? region.GetString() : null,
            x.TryGetProperty("country", out var country) ? country.GetString() : null,
            x.GetProperty("latitude").GetDouble(), x.GetProperty("longitude").GetDouble(),
            x.GetProperty("timezone").GetString() ?? "UTC")).ToList();
    }
}

public sealed class WeatherService(HttpClient client)
{
    private static readonly string Daily = "weather_code,temperature_2m_max,temperature_2m_min,wind_speed_10m_max";

    public async Task<WeatherSnapshot> FetchAsync(City city, CancellationToken token = default)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        var yesterday = today.AddDays(-1);
        var basis = $"latitude={city.Latitude.ToString(CultureInfo.InvariantCulture)}&longitude={city.Longitude.ToString(CultureInfo.InvariantCulture)}&daily={Daily}&timezone={Uri.EscapeDataString(city.TimeZone)}";
        // Yesterday comes from the historical reanalysis archive, not an observed station record.
        var historyUrl = $"https://archive-api.open-meteo.com/v1/archive?{basis}&start_date={yesterday:yyyy-MM-dd}&end_date={yesterday:yyyy-MM-dd}";
        var forecastUrl = $"https://api.open-meteo.com/v1/forecast?{basis}&forecast_days=4&current=temperature_2m";
        var (history, _) = await ReadDaysAsync(historyUrl, WeatherOrigin.HistoricalModel, token);
        var (forecast, currentTemperature) = await ReadDaysAsync(forecastUrl, WeatherOrigin.ForecastModel, token);
        var days = history.Concat(forecast).Where(x => x.Date >= yesterday && x.Date <= today.AddDays(3))
            .GroupBy(x => x.Date).Select(x => x.First()).OrderBy(x => x.Date).ToList();
        if (days.Count != 5 || days[0].Date != yesterday || days[4].Date != today.AddDays(3))
            throw new InvalidDataException("天气服务未返回连续五天的数据。");
        return new(city, days, DateTimeOffset.UtcNow, currentTemperature);
    }

    private async Task<(List<DayWeather> Days, double? CurrentTemperature)> ReadDaysAsync(string url, WeatherOrigin origin, CancellationToken token)
    {
        using var response = await client.GetAsync(url, token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        var daily = json.RootElement.GetProperty("daily");
        var dates = daily.GetProperty("time").EnumerateArray().ToArray();
        var highs = daily.GetProperty("temperature_2m_max").EnumerateArray().ToArray();
        var lows = daily.GetProperty("temperature_2m_min").EnumerateArray().ToArray();
        var codes = daily.GetProperty("weather_code").EnumerateArray().ToArray();
        var winds = daily.GetProperty("wind_speed_10m_max").EnumerateArray().ToArray();
        if (dates.Length != highs.Length || dates.Length != lows.Length || dates.Length != codes.Length || dates.Length != winds.Length)
            throw new InvalidDataException("天气服务返回的数据长度不一致。");
        var days = Enumerable.Range(0, dates.Length).Select(i => new DayWeather(
            DateOnly.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            highs[i].GetDouble(), lows[i].GetDouble(), codes[i].GetInt32(), origin,
            winds[i].ValueKind == JsonValueKind.Number ? winds[i].GetDouble() : 0)).ToList();
        double? currentTemperature = null;
        if (json.RootElement.TryGetProperty("current", out var current) &&
            current.TryGetProperty("temperature_2m", out var temperature) && temperature.ValueKind == JsonValueKind.Number)
            currentTemperature = temperature.GetDouble();
        return (days, currentTemperature);
    }
}

public static class MockWeatherService
{
    public static WeatherSnapshot Create(City city, int code = 2, double windSpeed = 0)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var codes = new[] { 0, code, 61, 71, 3 };
        return new(city, Enumerable.Range(-1, 5).Select((offset, index) =>
            new DayWeather(today.AddDays(offset), 21 - index, 12 - index, codes[index], WeatherOrigin.Mock,
                index == 1 ? windSpeed : 0)).ToList(), DateTimeOffset.UtcNow, 18.5);
    }
}
