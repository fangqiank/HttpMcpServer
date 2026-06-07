using System.ComponentModel;
using ModelContextProtocol.Server;

namespace HttpMcpServer.Tools
{
    public class WeatherTools
    {
        [McpServerTool(Name = "get_weather")]
        [Description("Get current weather for a city")]
        public static Task<string> GetWeather(
            [Description("City name")] string city,
            [Description("Temperature units (celsius/fahrenheit)")] string units = "celsius")
        {
            var temperature = units == "fahrenheit"
            ? Random.Shared.Next(32, 100)
            : Random.Shared.Next(0, 38);

            var conditions = new[] { "Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Windy" };
            var condition = conditions[Random.Shared.Next(conditions.Length)];

            var result = $"Weather in {city}: {temperature}°{(units == "fahrenheit" ? "F" : "C")}, {condition}";
            return Task.FromResult(result);
        }

        [McpServerTool(Name = "get_forecast")]
        [Description("Get 3-day weather forecast for a city")]
        public static Task<string> GetForecast(
        [Description("City name")] string city)
        {
            var forecast = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var temp = Random.Shared.Next(10, 30);
                var date = DateTime.Now.AddDays(i).ToString("MMM dd");
                forecast.Add($"{date}: {temp}°C");
            }

            return Task.FromResult($"3-day forecast for {city}:\n{string.Join("\n", forecast)}");
        }
    }
}
