using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace HttpMcpServer.Tools
{
    public class WeatherTools
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeatherTools> _logger;

        public WeatherTools(HttpClient httpClient, ILogger<WeatherTools> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// 将城市名解析为经纬度（使用 Open-Meteo Geocoding API，支持中英文）
        /// </summary>
        private async Task<(double Latitude, double Longitude, string DisplayName)> ResolveCityAsync(string city)
        {
            city = city.Trim();

            // 先尝试中文（支持中文城市名如"上海"、"合肥"），再尝试英文
            var result = await TryGeocodeAsync(city, "zh") ?? await TryGeocodeAsync(city, "en");

            if (result == null)
                throw new McpException($"City '{city}' not found. Try English name like 'Shanghai' or 'Calgary'.");

            return result.Value;
        }

        private async Task<(double Latitude, double Longitude, string DisplayName)?> TryGeocodeAsync(string city, string language)
        {
            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language={language}";

            string geoJson;
            try
            {
                geoJson = await _httpClient.GetStringAsync(geoUrl);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Geocoding API request failed for city: {City}", city);
                return null;
            }

            using var geoDoc = JsonDocument.Parse(geoJson);

            if (!geoDoc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            var first = results[0];
            var lat = first.GetProperty("latitude").GetDouble();
            var lon = first.GetProperty("longitude").GetDouble();
            var name = first.GetProperty("name").GetString() ?? city;
            var country = first.TryGetProperty("country", out var c) ? c.GetString() : "";
            var admin1 = first.TryGetProperty("admin1", out var a) ? a.GetString() : "";

            var display = string.IsNullOrEmpty(admin1) ? $"{name}, {country}" : $"{name}, {admin1}, {country}";
            return (lat, lon, display);
        }

        /// <summary>
        /// WMO Weather interpretation code 转人类可读描述
        /// </summary>
        private static string InterpretWmoCode(int code) => code switch
        {
            0 => "Clear sky",
            1 => "Mainly clear",
            2 => "Partly cloudy",
            3 => "Overcast",
            45 or 48 => "Fog",
            >= 51 and <= 55 => "Drizzle",
            >= 56 and <= 57 => "Freezing drizzle",
            >= 61 and <= 65 => "Rain",
            >= 66 and <= 67 => "Freezing rain",
            >= 71 and <= 77 => "Snow",
            >= 80 and <= 82 => "Rain showers",
            >= 85 and <= 86 => "Snow showers",
            >= 95 and <= 99 => "Thunderstorm",
            _ => $"Unknown (code {code})"
        };

        [McpServerTool(Name = "get_weather")]
        [Description("Get current real-time weather for a city using Open-Meteo API")]
        [AllowAnonymous]
        public async Task<string> GetWeather(
            [Description("City name (e.g., 'Shanghai', 'New York', 'London')")] string city,
            [Description("Temperature units (celsius/fahrenheit)")] string units = "celsius")
        {
            _logger.LogInformation("Getting real-time weather for: {City}", city);

            var (lat, lon, displayName) = await ResolveCityAsync(city);

            var tempUnit = units == "fahrenheit" ? "fahrenheit" : "celsius";
            var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true&temperature_unit={tempUnit}";
            var weatherJson = await _httpClient.GetStringAsync(weatherUrl);
            using var doc = JsonDocument.Parse(weatherJson);

            var current = doc.RootElement.GetProperty("current_weather");
            var temperature = current.GetProperty("temperature").GetDouble();
            var windspeed = current.GetProperty("windspeed").GetDouble();
            var winddir = current.GetProperty("winddirection").GetDouble();
            var weatherCode = current.GetProperty("weathercode").GetInt32();
            var time = current.GetProperty("time").GetString();

            var condition = InterpretWmoCode(weatherCode);
            var unitSymbol = units == "fahrenheit" ? "F" : "C";

            return $"Weather in {displayName} (as of {time}):\n" +
                   $"  Temperature: {temperature}°{unitSymbol}\n" +
                   $"  Condition: {condition}\n" +
                   $"  Wind: {windspeed} km/h, direction {winddir}°";
        }

        [McpServerTool(Name = "get_forecast")]
        [Description("Get 3-day weather forecast for a city using Open-Meteo API")]
        [AllowAnonymous]
        public async Task<string> GetForecast(
            [Description("City name (e.g., 'Shanghai', 'New York', 'London')")] string city)
        {
            _logger.LogInformation("Getting 3-day forecast for: {City}", city);

            var (lat, lon, displayName) = await ResolveCityAsync(city);

            var forecastUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                              "&daily=temperature_2m_max,temperature_2m_min,weathercode&timezone=auto&forecast_days=3";
            var forecastJson = await _httpClient.GetStringAsync(forecastUrl);
            using var doc = JsonDocument.Parse(forecastJson);

            var daily = doc.RootElement.GetProperty("daily");
            var dates = daily.GetProperty("time").EnumerateArray().Select(e => e.GetString()!).ToList();
            var maxTemps = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(e => e.GetDouble()).ToList();
            var minTemps = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(e => e.GetDouble()).ToList();
            var codes = daily.GetProperty("weathercode").EnumerateArray().Select(e => e.GetInt32()).ToList();

            var lines = new List<string>();
            for (int i = 0; i < dates.Count; i++)
            {
                var condition = InterpretWmoCode(codes[i]);
                lines.Add($"{dates[i]}: {minTemps[i]:F1}°C ~ {maxTemps[i]:F1}°C, {condition}");
            }

            return $"3-day forecast for {displayName}:\n{string.Join("\n", lines)}";
        }
    }
}
