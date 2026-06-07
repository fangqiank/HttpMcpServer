using HttpMcpServer.Models;
using System.Text.Json;

namespace HttpMcpServer.Services
{
    public class McpToolHandler : IMcpToolHandler
    {
        private readonly Dictionary<string, Func<JsonElement?, Task<ToolCallResult>>> _handlers;
        public McpToolHandler()
        {
            _handlers = new Dictionary<string, Func<JsonElement?, Task<ToolCallResult>>>
            {
                ["get_weather"] = HandleGetWeather,
                ["calculate"] = HandleCalculate,
                ["get_current_time"] = HandleGetCurrentTime,
                ["search_docs"] = HandleSearchDocs
            };
        }

        private Task<ToolCallResult> HandleSearchDocs(JsonElement? arguments)
        {
            var query = "";
            var limit = 5;

            if(arguments.HasValue)
            {
                var args = arguments.Value;
                if (args.TryGetProperty("query", out var queryElement))
                    query = queryElement.GetString() ?? "";

                if (args.TryGetProperty("limit", out var limitElement) && limitElement.TryGetInt32(out var limitValue))
                    limit = limitValue;
            }

            var docs = new[]
            {
                $"Document about {query} - Overview",
                $"Getting started with {query}",
                $"Advanced {query} techniques",
                $"{query} API reference",
                $"Troubleshooting {query} issues"
            };

            var results = docs
                .Take(limit)
                .Select((doc, index) =>
                    $"{index + 1}. {doc}"
                    );

            return Task.FromResult(new ToolCallResult
            {
                Content = new List<ContentItem>
                {
                    new() { 
                        Type = "text", 
                        Text = $"Search results for '{query}':\n{string.Join("\n", results)}" }
                }
            });
        }

        private Task<ToolCallResult> HandleGetCurrentTime(JsonElement? arguments)
        {
            var timezone = "UTC";
            if (arguments.HasValue && arguments.Value.TryGetProperty("timezone", out var tz))
                timezone = tz.GetString() ?? "UTC";

            try
            {
                TimeZoneInfo tzInfo;
                try
                {
                    tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                }
                catch
                {
                    tzInfo = TimeZoneInfo.Utc;
                }

                var time = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tzInfo);
                return Task.FromResult(new ToolCallResult
                {
                    Content = new List<ContentItem>
                    {
                        new() { Type = "text", Text = $"Current time in {timezone}: {time:yyyy-MM-dd HH:mm:ss zzz}" }
                    }
                });
            }
            catch
            {
                return Task.FromResult(new ToolCallResult
                {
                    IsError = true,
                    Content = new List<ContentItem>
                    {
                        new() { Type = "text", Text = $"Invalid timezone: {timezone}" }
                    }
                });
            }
        }

        private async Task<ToolCallResult> HandleCalculate(JsonElement? arguments)
        {
            if (!arguments.HasValue || !arguments.Value.TryGetProperty("expression", out var expressionElement))
            {
                return new ToolCallResult
                {
                    IsError = true,
                    Content = new List<ContentItem>
                    {
                        new() { Type = "text", Text = "Missing expression parameter" }
                    }
                };
            }

            try
            {
                var expression = expressionElement.GetString() ?? "";
                expression = expression.Replace(" ", "");

                double result = 0;
                if (expression.Contains('+'))
                {
                    var parts = expression.Split('+');
                    result = double.Parse(parts[0]) + double.Parse(parts[1]);
                }
                else if (expression.Contains('-'))
                {
                    var parts = expression.Split('-');
                    result = double.Parse(parts[0]) - double.Parse(parts[1]);
                }
                else if (expression.Contains('*'))
                {
                    var parts = expression.Split('*');
                    result = double.Parse(parts[0]) * double.Parse(parts[1]);
                }
                else if (expression.Contains('/'))
                {
                    var parts = expression.Split('/');
                    result = double.Parse(parts[0]) / double.Parse(parts[1]);
                }

                return new ToolCallResult
                {
                    Content = new List<ContentItem>
                    {
                        new() { Type = "text", Text = $"{expression} = {result}" }
                    }
                };
            }
            catch (Exception ex)
            {
                return new ToolCallResult
                {
                    IsError = true,
                    Content = new List<ContentItem>
                    {
                        new() { Type = "text", Text = $"Error evaluating expression: {ex.Message}" }
                    }
                };
            }
        }

        private async Task<ToolCallResult> HandleGetWeather(JsonElement? arguments)
        {
            var city = "Unknown";
            var units = "celsius";

            if (arguments.HasValue)
            {
                var args = arguments.Value;

                if (args.TryGetProperty("city", out var cityElement))
                    city = cityElement.GetString() ?? "Unknown";
                
                if (args.TryGetProperty("units", out var unitsElement))
                    units = unitsElement.GetString() ?? "celsius";
            }

            // Simulate weather data retrieval
            var random = new Random();
            var temperature = units == "fahrenheit"
                ? random.Next(32, 100)
                : random.Next(0, 38);

            var conditions = new[] { "Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Windy" };

            var condition = conditions[random.Next(conditions.Length)];

            var resultText = $"Weather in {city}: {temperature}°{(units == "fahrenheit" ? "F" : "C")}, {condition}";
            
            return new ToolCallResult
            {
                Content = new List<ContentItem>
                {
                    new() { Type = "text", Text = resultText }
                }
            };
        }

        public async Task<ToolCallResult> CallToolAsync(string name, JsonElement? arguments)
        {
            if(_handlers.TryGetValue(name, out var handler))
                return await handler(arguments);

            return new ToolCallResult
            {
                IsError = true,
                Content = new List<ContentItem>
                {
                    new() { Type = "text", Text = $"Unknown tool: {name}" }
                }
            };
        }

        public Task<ListToolsResult> ListToolsAsync()
        {
            var tools = new List<ToolDefinition>
            {
                new()
                {
                    Name = "get_weather",
                    Description = "Get the current weather for a given location.",
                    InputSchema = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new
                        {
                            city = new
                            {
                                type = "string",
                                description = "City name"
                            },
                            units = new
                            {
                                type = "string",
                                description = "Temperature units (celsius/fahrenheit)",
                                @enum = new[] { "celsius", "fahrenheit" }
                            }
                        },
                        required = new[] { "city" }
                    })
                },

                new()
                {
                    Name = "calculate",
                    Description = "Perform mathematical calculations",
                    InputSchema = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new
                        {
                            expression = new
                            {
                                type = "string",
                                description = "Mathematical expression to evaluate"
                            }
                        },
                        required = new[] { "expression" }
                    })
                },

                new()
                {
                    Name = "get_current_time",
                    Description = "Get the current time for a timezone",
                    InputSchema = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new
                        {
                            timezone = new
                            {
                                type = "string",
                                description = "Timezone (e.g., 'UTC', 'America/New_York')"
                            }
                        }
                    })
                },

                new()
                {
                    Name = "search_docs",
                    Description = "Search through documentation",
                    InputSchema = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "Search query"
                            },
                            limit = new
                            {
                                type = "number",
                                description = "Maximum number of results",
                                @default = 5
                            }
                        },
                        required = new[] { "query" }
                    })
                }
            };

            return Task.FromResult(new ListToolsResult { Tools = tools });
        }
    }
}
