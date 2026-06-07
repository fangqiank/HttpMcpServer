using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace HttpMcpServer.Tools
{
    public class TimeTools
    {
        [McpServerTool(Name = "get_current_time")]
        [Description("Get the current time for a timezone (supports IANA like 'Asia/Shanghai' and Windows format)")]
        [AllowAnonymous]
        public static Task<string> GetCurrentTime(
            [Description("Timezone ID (IANA: 'Asia/Shanghai', 'America/New_York' or Windows: 'Pacific Standard Time')")]
            string timezone = "UTC")
        {
            TimeZoneInfo tzInfo;
            try
            {
                tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch (TimeZoneNotFoundException)
            {
                throw new McpException(
                    $"Timezone '{timezone}' not found. Use list_timezones to see available IDs.");
            }

            var time = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tzInfo);
            var offset = tzInfo.GetUtcOffset(DateTime.UtcNow);
            var offsetStr = $"UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:D2}:{offset.Minutes:D2}";

            // 显示 IANA ID（如果与 Windows ID 不同）
            var displayId = TimeZoneInfo.TryConvertWindowsIdToIanaId(tzInfo.Id, out var ianaId) && ianaId != tzInfo.Id
                ? $"{tzInfo.Id} / {ianaId}"
                : tzInfo.Id;

            return Task.FromResult($"Current time in {displayId}: {time:yyyy-MM-dd HH:mm:ss} ({offsetStr})");
        }

        [McpServerTool(Name = "list_timezones")]
        [Description("List available timezones matching a search string, returns both Windows and IANA IDs")]
        [AllowAnonymous]
        public static Task<string> ListTimezones(
            [Description("Search string to filter timezones (e.g., 'Shanghai', 'Europe', '+08')")]
            string search = "")
        {
            var timezones = TimeZoneInfo.GetSystemTimeZones()
                .Where(tz =>
                {
                    if (string.IsNullOrEmpty(search))
                        return true;

                    // 在 Windows ID、显示名、IANA ID 中搜索
                    if (tz.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (tz.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var iana) &&
                        iana.Contains(search, StringComparison.OrdinalIgnoreCase))
                        return true;

                    return false;
                })
                .Take(10)
                .Select(tz =>
                {
                    var ianaPart = TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var ianaId)
                        ? $" | IANA: {ianaId}" : "";
                    return $"{tz.Id}{ianaPart} — {tz.DisplayName}";
                });

            var result = timezones.ToList();
            if (result.Count == 0)
                return Task.FromResult($"No timezones found matching '{search}'.");

            return Task.FromResult($"Available timezones (showing {result.Count}):\n{string.Join("\n", result)}");
        }
    }
}
