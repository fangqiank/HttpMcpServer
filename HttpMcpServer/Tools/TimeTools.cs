using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace HttpMcpServer.Tools
{
    /// <summary>
    /// 常用时区列表，MCP 客户端会展示为下拉选择
    /// </summary>
    public enum TimezoneId
    {
        [Description("UTC")] UTC,
        [Description("America/New_York")] New_York,
        [Description("America/Chicago")] Chicago,
        [Description("America/Denver")] Denver,
        [Description("America/Los_Angeles")] Los_Angeles,
        [Description("America/Sao_Paulo")] Sao_Paulo,
        [Description("Europe/London")] London,
        [Description("Europe/Paris")] Paris,
        [Description("Europe/Berlin")] Berlin,
        [Description("Europe/Moscow")] Moscow,
        [Description("Africa/Cairo")] Cairo,
        [Description("Asia/Dubai")] Dubai,
        [Description("Asia/Kolkata")] Kolkata,
        [Description("Asia/Bangkok")] Bangkok,
        [Description("Asia/Shanghai")] Shanghai,
        [Description("Asia/Hong_Kong")] Hong_Kong,
        [Description("Asia/Tokyo")] Tokyo,
        [Description("Asia/Seoul")] Seoul,
        [Description("Australia/Sydney")] Sydney,
        [Description("Pacific/Auckland")] Auckland,
    }

    public class TimeTools
    {
        [McpServerTool(Name = "get_current_time")]
        [Description("Get the current time for a timezone")]
        [AllowAnonymous]
        public static Task<string> GetCurrentTime(
            [Description("Select a timezone")] TimezoneId timezone = TimezoneId.UTC)
        {
            // 从枚举的 Description 特性获取 IANA 时区 ID
            var timezoneId = timezone.GetType()
                .GetField(timezone.ToString())?
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .Cast<DescriptionAttribute>()
                .FirstOrDefault()?.Description ?? timezone.ToString();

            TimeZoneInfo tzInfo;
            try
            {
                tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new McpException(
                    $"Timezone '{timezoneId}' not found. Use list_timezones to see available IDs.");
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
