using HttpMcpServer.Models;
using System.Text.Json;

namespace HttpMcpServer.Services
{
    public interface IMcpToolHandler
    {
        Task<ListToolsResult> ListToolsAsync();
        Task<ToolCallResult> CallToolAsync(string name, JsonElement? arguments);
    }
}
