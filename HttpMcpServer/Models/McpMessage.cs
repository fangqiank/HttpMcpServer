using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpMcpServer.Models
{
    public class McpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    public class McpResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("result")]
        public JsonElement? Result { get; set; }

        [JsonPropertyName("error")]
        public McpError? Error { get; set; }
    }
    public class McpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }
    }
    public class McpNotification
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }
    public class InitializeResult
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2024-11-05";

        [JsonPropertyName("capabilities")]
        public McpCapabilities Capabilities { get; set; } = new();

        [JsonPropertyName("serverInfo")]
        public ServerInfo ServerInfo { get; set; } = new();
    }
    public class ServerInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "HttpMcpServer";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";
    }
    public class McpCapabilities
    {
        [JsonPropertyName("tools")]
        public ToolCapabilities? Tools { get; set; }
    }
    public class ToolCapabilities
    {
        [JsonPropertyName("listChanged")]
        public bool ListChanged { get; set; } = false;
    }
    public class ToolDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("inputSchema")]
        public JsonElement InputSchema { get; set; }
    }
    public class ToolCallParams
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public JsonElement? Arguments { get; set; }
    }
    public class ToolCallResult
    {
        [JsonPropertyName("content")]
        public List<ContentItem> Content { get; set; } = new();

        [JsonPropertyName("isError")]
        public bool IsError { get; set; }
    }
    public class ContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
    public class ListToolsResult
    {
        [JsonPropertyName("tools")]
        public List<ToolDefinition> Tools { get; set; } = new();
    }
}
