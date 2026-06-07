using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace HttpMcpServer.Tools
{
    public class TimeTools
    {
        [McpServerTool(Name = "get_current_time")]
        [Description("Get the current time for a timezone")]
        [AllowAnonymous]
        public static Task<string> GetCurrentTime(
        [Description("Timezone (e.g., 'UTC', 'America/New_York', 'Asia/Shanghai')")]
        string timezone = "UTC")
        {
            TimeZoneInfo tzInfo;
            try
            {
                tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch
            {
                throw new McpException($"Invalid timezone: {timezone}");
            }

            var time = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tzInfo);
            var offset = tzInfo.GetUtcOffset(DateTime.UtcNow);
            var offsetStr = $"UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:D2}:{offset.Minutes:D2}";

            return Task.FromResult($"Current time in {timezone}: {time:yyyy-MM-dd HH:mm:ss} ({offsetStr})");
        }

        [McpServerTool(Name = "list_timezones")]
        [Description("List available timezones matching a search string")]
        [AllowAnonymous]
        public static Task<string> ListTimezones(
            [Description("Search string to filter timezones")] string search = "")
        {
            var timezones = TimeZoneInfo.GetSystemTimeZones()
                .Where(tz => string.IsNullOrEmpty(search) ||
                            tz.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .Select(tz => $"{tz.Id} - {tz.DisplayName}");

            return Task.FromResult($"Available timezones:\n{string.Join("\n", timezones)}");
        }
    }
}
