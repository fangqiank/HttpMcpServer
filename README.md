# HttpMcpServer

ASP.NET Core MCP (Model Context Protocol) 服务器 — 通过 StreamableHttp 传输协议暴露 AI 工具调用能力，使用 Auth0 JWT 保护端点。

An ASP.NET Core MCP server exposing AI tool-calling capabilities via StreamableHttp transport, protected with Auth0 JWT authentication.

![Architecture](HttpMcpServer-architecture.svg)

## Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| .NET | 10.0 | Runtime |
| ASP.NET Core | 10.0 | Web host & middleware |
| ModelContextProtocol | 1.4.0 | MCP SDK (core types) |
| ModelContextProtocol.AspNetCore | 1.4.0 | MCP HTTP transport + auth filters |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.x | JWT Bearer authentication |
| Auth0 | — | OAuth 2.0 / OIDC identity provider |

## Modules

| Module | File | Responsibility |
|---|---|---|
| Host | `Program.cs` | Auth0 JWT validation, MCP server DI, middleware pipeline |
| WeatherTools | `Tools/WeatherTools.cs` | Simulated weather data (get_weather, get_forecast) |
| CalculatorTools | `Tools/CalculatorTools.cs` | Math expression evaluation, unit conversion |
| TimeTools | `Tools/TimeTools.cs` | Timezone-aware time queries (get_current_time, list_timezones) |
| DatabaseTools | `Tools/DatabaseTools.cs` | Document search with DI-injected logger/config |

## Authentication Flow

```
Auth0 (OIDC Provider)
  │
  │ 1. Issues JWT access token (RS256 signed)
  │
  ▼
MCP Client (Claude Desktop / Inspector)
  │
  │ 2. Sends Bearer token in Authorization header
  │
  ▼ POST /mcp
┌──────────────────────────────────┐
│ ASP.NET Core Middleware Pipeline  │
│  CORS → JWT Auth → Authorization │
├──────────────────────────────────┤
│ MCP SDK (AddAuthorizationFilters)│
│  [Authorize] on all tools        │
├──────────────────────────────────┤
│ MCP Tools                        │
│  Weather / Calculator /          │
│  Time / Database                 │
└──────────────────────────────────┘
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

## Configuration

Create an API in [Auth0 Dashboard](https://manage.auth0.com/) and update `appsettings.json`:

```json
{
  "Auth0": {
    "Domain": "your-tenant.us.auth0.com",
    "Audience": "https://your-api-identifier"
  }
}
```

### Auth0 Setup Steps

1. **Auth0 Dashboard → Applications → APIs → Create API**
   - Set Name and Identifier (Identifier = Audience)
   - Signing Algorithm: RS256

2. **Auth0 Dashboard → Applications → Applications → Create Application**
   - Type: Machine to Machine (for testing)
   - Authorize the API created above

3. **Get a test token**:
   ```bash
   curl --request POST \
     --url https://your-tenant.us.auth0.com/oauth/token \
     --header 'content-type: application/json' \
     --data '{
       "client_id": "<client-id>",
       "client_secret": "<client-secret>",
       "audience": "https://your-api-identifier",
       "grant_type": "client_credentials"
     }'
   ```

## API Endpoints

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/mcp` | POST | Required | MCP StreamableHttp endpoint (JSON-RPC 2.0) |
| `/health` | GET | None | Health check |

## MCP Tools

| Tool | Parameters | Description |
|---|---|---|
| `get_weather` | `city` (required), `units` (optional) | Get current weather for a city |
| `get_forecast` | `city` (required) | Get 3-day weather forecast |
| `calculate` | `expression` (required) | Evaluate a math expression |
| `convert_units` | `value`, `from`, `to` (all required) | Convert between units (km/mi, kg/lb, C/F) |
| `get_current_time` | `timezone` (optional, default: UTC) | Get current time for a timezone |
| `list_timezones` | `search` (optional) | List available timezones |
| `search_documents` | `query` (required), `limit` (optional) | Search document database |

## Client Configuration

### MCP Inspector

1. Transport Type: `Streamable HTTP`
2. URL: `http://localhost:5000/mcp`
3. Add custom header: `Authorization: Bearer <your-token>`

### Claude Desktop (via mcp-remote)

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
  -H "Authorization: Bearer <your-access-token>" \
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
