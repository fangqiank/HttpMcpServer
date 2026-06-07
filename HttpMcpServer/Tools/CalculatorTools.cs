using System.ComponentModel;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace HttpMcpServer.Tools
{
    public class CalculatorTools
    {
        [McpServerTool(Name = "calculate")]
        [Description("Perform mathematical calculations")]
        [Authorize]
        public static Task<string> Calculate(
        [Description("Mathematical expression to evaluate")] string expression)
        {
            try
            {
                var dataTable = new DataTable();
                var result = dataTable.Compute(expression, null);
                return Task.FromResult($"{expression} = {result}");
            }
            catch (Exception ex)
            {
                throw new McpException($"Calculation error: {ex.Message}");
            }
        }

        [McpServerTool(Name = "convert_units")]
        [Description("Convert between different units")]
        [Authorize]
        public static Task<string> ConvertUnits(
            [Description("Value to convert")] double value,
            [Description("Source unit (e.g., km, mi, kg, lb)")] string from,
            [Description("Target unit (e.g., mi, km, lb, kg)")] string to)
        {
            var conversions = new Dictionary<(string, string), double>
            {
                { ("km", "mi"), 0.621371 },
                { ("mi", "km"), 1.60934 },
                { ("kg", "lb"), 2.20462 },
                { ("lb", "kg"), 0.453592 },
            };

            if ((from.ToLower(), to.ToLower()) == ("celsius", "fahrenheit"))
            {
                var result = (value * 1.8) + 32;
                return Task.FromResult($"{value}°C = {result:F2}°F");
            }

            if ((from.ToLower(), to.ToLower()) == ("fahrenheit", "celsius"))
            {
                var result = (value - 32) / 1.8;
                return Task.FromResult($"{value}°F = {result:F2}°C");
            }

            if (conversions.TryGetValue((from.ToLower(), to.ToLower()), out var factor))
            {
                var result = value * factor;
                return Task.FromResult($"{value} {from} = {result:F2} {to}");
            }

            throw new McpException($"Conversion from {from} to {to} not supported");
        }
    }
}
