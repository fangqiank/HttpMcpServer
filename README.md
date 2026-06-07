# HttpMcpServer

ASP.NET Core MCP (Model Context Protocol) 服务器 — 通过 StreamableHttp 传输协议暴露 AI 工具调用能力。

An ASP.NET Core MCP server exposing AI tool-calling capabilities via StreamableHttp transport.

![Architecture](HttpMcpServer-architecture.svg)

## Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| .NET | 10.0 | Runtime |
| ASP.NET Core | 10.0 | Web host & middleware |
| ModelContextProtocol | 1.4.0 | MCP SDK (core types) |
| ModelContextProtocol.AspNetCore | 1.4.0 | MCP HTTP transport integration |

## Modules

| Module | File | Responsibility |
|---|---|---|
| Host | `Program.cs` | DI registration, middleware pipeline, endpoint mapping |
| WeatherTools | `Tools/WeatherTools.cs` | Simulated weather data (get_weather, get_forecast) |
| CalculatorTools | `Tools/CalculatorTools.cs` | Math expression evaluation, unit conversion |
| TimeTools | `Tools/TimeTools.cs` | Timezone-aware time queries (get_current_time, list_timezones) |
| DatabaseTools | `Tools/DatabaseTools.cs` | Document search with DI-injected logger/config |

## Data Flow

```
MCP Client (Claude/SDK)
  │
  ▼ HTTP POST /mcp (JSON-RPC 2.0)
┌──────────────────────────────┐
│ ASP.NET Core Middleware      │
│  CORS → Request Logging      │
├──────────────────────────────┤
│ Endpoint Routing             │
│  /mcp → MCP SDK Handler      │
│  /health → Health Check      │
├──────────────────────────────┤
│ ModelContextProtocol SDK     │
│  StreamableHttp Transport    │
│  Tool Discovery & Dispatch   │
├──────────────────────────────┤
│ MCP Tools                    │
│  Weather / Calculator /      │
│  Time / Database             │
└──────────────────────────────┘
```

## Quick Start

```bash
# Restore and build
dotnet build HttpMcpServer/HttpMcpServer.csproj

# Run (defaults to http://localhost:5000)
dotnet run --project HttpMcpServer/HttpMcpServer.csproj

# Run with HTTPS profile
dotnet run --project HttpMcpServer/HttpMcpServer.csproj --launch-profile https
```

## API Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/mcp` | POST | MCP StreamableHttp endpoint (JSON-RPC 2.0) |
| `/health` | GET | Health check |

## MCP Tools

| Tool | Parameters | Description |
|---|---|---|
| `get_weather` | `city` (required), `units` (optional: celsius/fahrenheit) | Get current weather for a city |
| `get_forecast` | `city` (required) | Get 3-day weather forecast |
| `calculate` | `expression` (required) | Evaluate a math expression |
| `convert_units` | `value`, `from`, `to` (all required) | Convert between units (km/mi, kg/lb, C/F) |
| `get_current_time` | `timezone` (optional, default: UTC) | Get current time for a timezone |
| `list_timezones` | `search` (optional) | List available timezones |
| `search_documents` | `query` (required), `limit` (optional, default: 5) | Search document database |

## Configuration

### Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "http-tools-server": {
      "command": "npx",
      "args": ["-y", "mcp-remote", "http://localhost:5000/mcp"]
    }
  }
}
```

### Claude Code CLI

```bash
claude mcp add http-tools-server --transport streamable-http http://localhost:5000/mcp
```

## Example Request

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "tools/call",
    "params": {
      "name": "get_weather",
      "arguments": { "city": "Shanghai", "units": "celsius" }
    }
  }'
```
