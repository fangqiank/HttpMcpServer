# HttpMcpServer

ASP.NET Core MCP (Model Context Protocol) 服务器 -- 通过 StreamableHttp 传输协议暴露 12 个 AI 工具调用能力，使用 Auth0 JWT 保护敏感工具，支持 MCP OAuth 自动发现，内置 SQLite 文档数据库。

An ASP.NET Core MCP server exposing 12 tools via StreamableHttp transport, with Auth0 JWT selective tool authorization, MCP OAuth auto-discovery, and a built-in SQLite document store.

![Architecture](HttpMcpServer-architecture.svg)

## Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| .NET | 10.0 | Runtime |
| ASP.NET Core | 10.0 | Web host & middleware |
| ModelContextProtocol | 1.4.0 | MCP SDK (core types) |
| ModelContextProtocol.AspNetCore | 1.4.0 | MCP HTTP transport + auth filters |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.x | JWT Bearer authentication |
| Dapper | 2.x | Lightweight ORM for SQLite |
| Microsoft.Data.Sqlite | 10.x | SQLite ADO.NET provider |
| Auth0 | -- | OAuth 2.0 / OIDC identity provider |

## Modules

| Module | File | Auth | Responsibility |
|---|---|---|---|
| Host | `Program.cs` | -- | Auth0 JWT + JWKS, MCP server DI, OAuth metadata, middleware pipeline |
| DatabaseInitializer | `Data/DatabaseInitializer.cs` | -- | SQLite schema creation + seed data |
| WeatherTools | `Tools/WeatherTools.cs` | Public | Simulated weather & forecast data |
| CalculatorTools | `Tools/CalculatorTools.cs` | Public | Math evaluation, unit conversion |
| TimeTools | `Tools/TimeTools.cs` | Public | Timezone-aware time queries |
| DatabaseTools | `Tools/DatabaseTools.cs` | **JWT Required** | Document CRUD + search (DI-injected, Dapper) |

## Authorization Model

MCP SDK `AddAuthorizationFilters()` provides per-tool access control:

| Attribute | Behavior |
|---|---|
| `[AllowAnonymous]` | Tool visible and callable without authentication |
| `[Authorize]` | Tool **hidden** from `tools/list` and **blocked** on call without valid JWT |

```
No Token:              tools/list → get_weather, get_forecast, calculate,
                                   convert_units, get_current_time, list_timezones
                       (7 document tools are hidden)

With valid JWT:        tools/list → all 12 tools available
```

## OAuth Auto-Discovery

The server exposes `/.well-known/oauth-protected-resource` for MCP-native OAuth flow:

```
1. MCP client connects → receives 401
2. Discovers /.well-known/oauth-protected-resource
3. Finds Auth0 as authorization server
4. Initiates Authorization Code flow with PKCE
5. User logs in via Auth0
6. Client receives access token
7. Subsequent requests include Bearer token
```

## Quick Start

```bash
# Restore and build
dotnet build HttpMcpServer/HttpMcpServer.csproj

# Run (defaults to http://localhost:5034)
dotnet run --project HttpMcpServer/HttpMcpServer.csproj
```

The server auto-creates `data/mcp.db` with 10 seed documents on first run.

## Configuration

Update `appsettings.json` with your Auth0 settings:

```json
{
  "Auth0": {
    "Domain": "your-tenant.us.auth0.com",
    "Audience": "https://your-api-identifier"
  }
}
```

### Auth0 Setup

1. **Auth0 Dashboard → Applications → APIs → Create API** (sets Audience, RS256 signing)
2. **Auth0 Dashboard → Applications → Applications → Create Application**
   - Type: Machine to Machine (for testing) or Native (for OAuth flow)
   - Authorize the API
3. **For OAuth auto-discovery**: Add `http://localhost:6274/oauth/callback` to Allowed Callback URLs

### Get a test token

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

| Endpoint | Auth | Description |
|---|---|---|
| `/mcp` | Per-tool | MCP StreamableHttp endpoint (JSON-RPC 2.0) |
| `/health` | None | Health check |
| `/.well-known/oauth-protected-resource` | None | OAuth resource metadata for auto-discovery |

## MCP Tools

### Public Tools (no authentication required)

| Tool | Parameters | Description |
|---|---|---|
| `get_weather` | `city`, `units?` | Get current weather for a city |
| `get_forecast` | `city` | Get 3-day weather forecast |
| `calculate` | `expression` | Evaluate math expression |
| `convert_units` | `value`, `from`, `to` | Unit conversion (km/mi, kg/lb, C/F) |
| `get_current_time` | `timezone?` | Get time for a timezone |
| `list_timezones` | `search?` | List available timezones |

### Protected Tools (valid Auth0 JWT required)

| Tool | Parameters | Description |
|---|---|---|
| `search_documents` | `query`, `limit?` | Search documents by keyword |
| `get_document` | `id` | Get a specific document by ID |
| `list_documents` | `page?`, `pageSize?` | List documents with pagination |
| `create_document` | `title`, `content`, `category?` | Create a new document |
| `update_document` | `id`, `title?`, `content?`, `category?` | Update document (only provided fields) |
| `delete_document` | `id` | Delete a document by ID |
| `count_documents` | -- | Get total document count by category |

## Client Configuration

### MCP Inspector

1. Transport: `Streamable HTTP`
2. URL: `http://localhost:5034/mcp`
3. For protected tools: Add header `Authorization: Bearer <token>`

### Claude Desktop (via mcp-remote)

```json
{
  "mcpServers": {
    "http-tools-server": {
      "command": "npx",
      "args": ["-y", "mcp-remote", "http://localhost:5034/mcp"]
    }
  }
}
```

### Claude Code CLI

```bash
claude mcp add http-tools-server --transport streamable-http http://localhost:5034/mcp
```

## Example Request

```bash
# Public tool - no token needed
curl -X POST http://localhost:5034/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0", "id": "1",
    "method": "tools/call",
    "params": { "name": "get_weather", "arguments": { "city": "Shanghai" } }
  }'

# Protected tool - token required
curl -X POST http://localhost:5034/mcp \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-access-token>" \
  -d '{
    "jsonrpc": "2.0", "id": "2",
    "method": "tools/call",
    "params": { "name": "search_documents", "arguments": { "query": "MCP protocol" } }
  }'

# Create a document
curl -X POST http://localhost:5034/mcp \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-access-token>" \
  -d '{
    "jsonrpc": "2.0", "id": "3",
    "method": "tools/call",
    "params": { "name": "create_document", "arguments": { "title": "My Doc", "content": "Hello world", "category": "Notes" } }
  }'
```
