# 从 stdio 到 Streamable HTTP：.NET MCP 服务器实战指南

> From stdio to Streamable HTTP: A Practical Guide to .NET MCP Servers

---

## 1. 从 stdio MCP 服务器迁移到 Streamable HTTP

### stdio 模式：最简单的起步

MCP 协议最初设计的传输方式是 **stdio**——客户端把服务器当作子进程启动，通过标准输入/输出交换 JSON-RPC 消息。C# SDK 的最小化实现只需要几行代码：

```csharp
// stdio 模式 - 控制台应用
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()  // 关键：使用 stdio 传输
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

客户端配置（如 Claude Desktop）：

```json
{
  "mcpServers": {
    "my-server": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/MyServer/MyServer.csproj"]
    }
  }
}
```

**stdio 的限制**：
- 每个客户端独占一个进程，无法共享
- 服务器必须是客户端的本地子进程
- 无法通过互联网访问
- 进程生命周期由客户端管理，崩溃后无自动恢复

### 迁移到 Streamable HTTP

将 stdio 改为 HTTP 只需要三处变化：

```diff
- var builder = Host.CreateApplicationBuilder(args);
+ var builder = WebApplication.CreateBuilder(args);

  builder.Services
      .AddMcpServer()
-     .WithStdioServerTransport()
+     .WithHttpTransport()          // 1. 换传输方式
      .WithTools<MyTools>();

- await builder.Build().RunAsync();
+ var app = builder.Build();
+ app.MapMcp("/mcp");               // 2. 映射 HTTP 端点
+ app.Run("http://localhost:5000");  // 3. 指定监听地址
```

**关键差异**：

| 对比项 | stdio | Streamable HTTP |
|---|---|---|
| 宿主 | `Host.CreateApplicationBuilder` | `WebApplication.CreateBuilder` |
| 传输注册 | `WithStdioServerTransport()` | `WithHttpTransport()` |
| 端点映射 | 不需要 | `app.MapMcp("/mcp")` |
| NuGet 包 | `ModelContextProtocol` | `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` |
| 客户端连接 | 启动子进程 | HTTP POST 到 URL |
| 并发支持 | 单客户端 | 多客户端 |

---

## 2. 在 ASP.NET Core 中托管 MCP 服务器

以本项目（HttpMcpServer）为例，完整的托管配置如下：

```csharp
using HttpMcpServer.Tools;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// JSON 序列化配置
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// MCP 服务器注册
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "HttpMcpServer",
            Version = "1.0.0"
        };
        options.ProtocolVersion = "2024-11-05";
        options.Capabilities = new ModelContextProtocol.Protocol.ServerCapabilities
        {
            Tools = new ModelContextProtocol.Protocol.ToolsCapability()
        };
    })
    .WithHttpTransport()          // 启用 Streamable HTTP 传输
    .WithTools<WeatherTools>()    // 注册工具
    .WithTools<CalculatorTools>()
    .WithTools<TimeTools>()
    .WithTools<DatabaseTools>();

// CORS（允许跨域客户端连接）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Mcp-Session-Id");
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseCors();
app.MapMcp("/mcp");        // MCP 端点
app.MapHealthChecks("/health");
app.Run();
```

**为什么需要 `WithHttpTransport()`**：这个调用将 MCP SDK 的 HTTP 传输处理器注册到 DI 容器中。没有它，`MapMcp()` 不知道该用什么传输方式。

**CORS 的必要性**：浏览器端的 MCP 客户端（如 MCP Inspector）从不同源访问服务器时需要 CORS 支持。`Mcp-Session-Id` 是 Streamable HTTP 的关键响应头，必须显式暴露。

---

## 3. 为什么 `[McpServerTool]` 工具与传输无关

这是 MCP SDK 设计中最优雅的部分。工具定义不包含任何传输相关的代码：

```csharp
[McpServerToolType]
public static class CalculatorTools
{
    [McpServerTool, Description("Perform mathematical calculations")]
    public static Task<string> Calculate(string expression)
    {
        var result = new DataTable().Compute(expression, null);
        return Task.FromResult($"{expression} = {result}");
    }
}
```

注意这个类里：
- **没有** HTTP 相关代码（没有 `HttpContext`、`Request`、`Response`）
- **没有** stdio 相关代码（没有 `Console.In`、`Console.Out`）
- **没有** JSON-RPC 相关代码（没有 `method`、`params`、`id`）
- **只有** 业务逻辑：接收参数，返回结果

**SDK 在背后做了什么**：

```
客户端请求 → 传输层（stdio/HTTP/SSE）→ JSON-RPC 解析 → 路由到 [McpServerTool] → 业务逻辑 → 返回值 → JSON-RPC 封装 → 传输层 → 客户端响应
```

传输层是一个可插拔的"管道"。工具只需要声明 `[McpServerToolType]` 和 `[McpServerTool]` 属性，SDK 自动完成：
1. **工具发现**：扫描程序集，找到所有标记了 `[McpServerTool]` 的方法
2. **Schema 生成**：根据方法参数和 `[Description]` 特性自动生成 JSON Schema
3. **请求路由**：收到 `tools/call` 请求时，按 name 匹配并调用对应方法
4. **参数反序列化**：将 JSON-RPC params 反序列化为方法参数
5. **结果封装**：将返回值封装为 MCP 的 content 格式

这意味着同一套工具代码可以 **零修改** 地在 stdio 和 HTTP 之间切换：

```csharp
// stdio 模式
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<CalculatorTools>();

// HTTP 模式
builder.Services.AddMcpServer().WithHttpTransport().WithTools<CalculatorTools>();
```

工具类 `CalculatorTools` 完全不需要改动。

---

## 4. 用 MCP Inspector 调试 HTTP MCP 服务器

MCP Inspector 是官方提供的交互式调试工具，可以可视化地测试 MCP 服务器的工具、资源和提示。

### 安装和启动

```bash
# 全局安装
npm install -g @modelcontextprotocol/inspector

# 启动 Inspector
npx @modelcontextprotocol/inspector
```

Inspector 默认在 `http://localhost:6274` 启动一个 Web UI。

### 连接到 Streamable HTTP 服务器

1. **启动你的 MCP 服务器**：

```bash
dotnet run --project HttpMcpServer/HttpMcpServer.csproj
# 服务器运行在 http://localhost:5000
```

2. **在 Inspector UI 中配置连接**：

```
Transport Type: Streamable HTTP
URL: http://localhost:5000/mcp
```

3. **点击 Connect**，Inspector 会发送 `initialize` 请求并显示服务器能力。

### 调试流程

```
┌─────────────────────────────────────────────────┐
│  MCP Inspector (http://localhost:6274)           │
│                                                  │
│  1. Connection Tab                               │
│     Transport: Streamable HTTP                   │
│     URL: http://localhost:5000/mcp               │
│     [Connect]                                    │
│                                                  │
│  2. Tools Tab (连接成功后自动列出)                │
│     ┌──────────────────────────────────┐         │
│     │ get_weather                       │         │
│     │ get_forecast                      │         │
│     │ calculate                         │         │
│     │ convert_units                     │         │
│     │ get_current_time                  │         │
│     │ list_timezones                    │         │
│     │ search_documents                  │         │
│     └──────────────────────────────────┘         │
│                                                  │
│  3. 选择工具 → 填写参数 → 点击 [Run Tool]        │
│                                                  │
│  4. 查看 Request/Response 的完整 JSON-RPC 报文    │
└─────────────────────────────────────────────────┘
```

### 也可以用 curl 直接测试

```bash
# 初始化
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "1",
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": { "name": "test", "version": "1.0" }
    }
  }'

# 调用工具
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <从初始化响应获取>" \
  -d '{
    "jsonrpc": "2.0",
    "id": "2",
    "method": "tools/call",
    "params": {
      "name": "calculate",
      "arguments": { "expression": "2+3*4" }
    }
  }'
```

---

## 5. 为什么 Claude 要求 HTTPS，以及如何用 ngrok 隧道

### 问题：Claude Desktop 拒绝 HTTP

Claude Desktop 的 Remote MCP 功能 **强制要求 HTTPS**，即使目标是 `localhost`：

```json
// 这个配置会被拒绝
{
  "mcpServers": {
    "my-server": {
      "type": "streamable-http",
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

这是安全策略：HTTPS 防止中间人攻击，确保 MCP 工具调用的数据不被篡改。但本地开发时这个限制很麻烦。

### 解决方案 A：ngrok 隧道（推荐）

ngrok 为你的本地服务器创建一个 HTTPS 隧道：

```bash
# 1. 安装 ngrok
# Windows: winget install ngrok.ngrok
# macOS: brew install ngrok

# 2. 登录（首次需要）
ngrok config add-authtoken YOUR_TOKEN

# 3. 创建隧道到本地端口
ngrok http 5000
```

ngrok 会输出一个 HTTPS 地址：

```
Forwarding  https://a1b2c3d4.ngrok-free.app -> http://localhost:5000
```

然后在 Claude Desktop 中配置：

```json
{
  "mcpServers": {
    "http-tools-server": {
      "type": "streamable-http",
      "url": "https://a1b2c3d4.ngrok-free.app/mcp"
    }
  }
}
```

**注意事项**：
- ngrok 免费版的 URL 每次重启都会变
- ngrok 免费版有带宽和连接数限制
- 隧道延迟约 20-50ms（数据经过 ngrok 服务器中转）

### 解决方案 B：mcp-remote 本地代理

不需要公网隧道，通过 stdio 桥接：

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

`mcp-remote` 在本地启动一个 stdio 进程，内部连接到你的 HTTP 服务器，绕过了 Claude Desktop 的 HTTPS 限制。

### 解决方案 C：本地 HTTPS 自签名证书

```bash
# 生成开发证书
dotnet dev-certs https --trust

# 启动 HTTPS
dotnet run --launch-profile https
```

但 Claude Desktop 仍然可能拒绝自签名证书。

---

## 6. 有状态 vs 无状态 HTTP：扩展性权衡

MCP SDK 的 `WithHttpTransport()` 提供了两种运行模式，这是 HTTP MCP 服务器架构中最关键的选择。

### Stateful 模式（默认）

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = false;  // 默认值
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.MaxIdleSessionCount = 1_000;
    });
```

**工作方式**：
- 客户端首次请求时，服务器创建一个 **Session**，通过 `Mcp-Session-Id` 响应头返回
- 后续请求必须携带 `Mcp-Session-Id` 头，服务器找到对应 Session 继续处理
- Session 在内存中保存状态（已初始化、已订阅的资源、工具列表等）

**适用场景**：
- 需要服务器主动发起请求（sampling、elicitation）
- 需要维护跨请求的上下文
- 单实例部署

**扩展性问题**：
```
Client A → Session 1 ──→ Server Instance 1 (内存中有 Session 1)
Client B → Session 2 ──→ Server Instance 2 (内存中没有 Session 1)

如果 Client A 的下一次请求被负载均衡到 Instance 2：
Client A → Session 1 ──→ Server Instance 2 → 404 Session Not Found!
```

需要 **Session 粘滞（Sticky Session）** 或 **Session 迁移（Session Migration）**：

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = false;
        options.SessionMigrationHandler = new MySessionMigrationHandler();
        // MySessionMigrationHandler 从 Redis/DB 恢复 Session 状态
    });
```

### Stateless 模式（推荐）

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;  // 推荐！
    });
```

**工作方式**：
- 每次请求都是独立的，不依赖 Session
- 没有 `Mcp-Session-Id`，无需维护会话状态
- 每个请求可以由任意服务实例处理

**适用场景**：
- 工具是无状态的（输入 → 输出，不需要上下文）
- 需要水平扩展（多实例、K8s、容器）
- 不需要服务器主动请求（sampling 等）

**限制**：
- 不支持服务器到客户端的请求（sampling、elicitation）
- 不支持跨请求的资源订阅
- 每次请求都重新初始化工具配置（极小开销）

### Per-Request 动态配置

Stateless 模式下，`ConfigureSessionOptions` 在 **每次请求** 时调用，可以实现基于请求的动态工具注册：

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
        options.ConfigureSessionOptions = (httpContext, mcpServerOptions, ct) =>
        {
            // 根据请求头动态调整工具
            var apiKey = httpContext.Request.Headers["X-API-Key"].ToString();
            mcpServerOptions.ToolCollection = GetToolsForApiKey(apiKey);
            return Task.CompletedTask;
        };
    });
```

### 选择决策树

```
你的 MCP 服务器需要 sampling/elicitation 吗？
├── 是 → 必须用 Stateful 模式
│         └── 需要水平扩展吗？
│             ├── 否 → Stateful + 单实例，OK
│             └── 是 → Stateful + Session Migration（Redis/DB）
│
└── 否 → 用 Stateless 模式（推荐）
          ├── 天然支持负载均衡
          ├── 无需 Sticky Session
          └── 部署简单（Docker/K8s 随意扩缩）
```

### 本项目的建议

当前 HttpMcpServer 的所有工具都是无状态的（`static` 方法，输入→输出），应使用 Stateless 模式：

```csharp
builder.Services
    .AddMcpServer(options => { /* ... */ })
    .WithHttpTransport(options =>
    {
        options.Stateless = true;  // 所有工具无状态，推荐开启
    })
    .WithTools<WeatherTools>()
    .WithTools<CalculatorTools>()
    .WithTools<TimeTools>()
    .WithTools<DatabaseTools>();
```

---

## 总结

| 主题 | 关键结论 |
|---|---|
| stdio → HTTP | 只需换 3 行代码：宿主、传输、端点映射 |
| ASP.NET Core 托管 | `AddMcpServer()` + `WithHttpTransport()` + `MapMcp()` |
| 工具传输无关性 | `[McpServerTool]` 只有业务逻辑，传输层由 SDK 处理 |
| MCP Inspector 调试 | `npx @modelcontextprotocol/inspector`，选择 Streamable HTTP 连接 |
| Claude HTTPS 要求 | 用 ngrok 隧道或 mcp-remote 代理绕过本地开发限制 |
| 有状态 vs 无状态 | 工具无状态就用 Stateless，天然支持水平扩展 |

> 参考：[C# MCP SDK 官方文档](https://github.com/modelcontextprotocol/csharp-sdk) | [MCP 协议规范](https://spec.modelcontextprotocol.io)
