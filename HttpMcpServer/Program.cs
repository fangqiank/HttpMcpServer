using HttpMcpServer.Tools;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// 添加 MCP 服务器服务
builder.Services
    .AddMcpServer(options =>
    {
        // 服务器信息
        options.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "HttpMcpServer",
            Version = "1.0.0"
        };

        // 协议版本
        options.ProtocolVersion = "2024-11-05";

        // 自定义能力声明
        options.Capabilities = new ModelContextProtocol.Protocol.ServerCapabilities
        {
            Tools = new ModelContextProtocol.Protocol.ToolsCapability()
        };
    })
    .WithHttpTransport()
    // 注册工具类（无状态静态工具）
    .WithTools<WeatherTools>()
    .WithTools<CalculatorTools>()
    .WithTools<TimeTools>()
    // 注册需要依赖注入的工具类
    .WithTools<DatabaseTools>();

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

var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.UseCors();

app.Use(async (context, next) =>
{
    logger.LogInformation(
        "MCP Request: {Method} {Path}{QueryString}",
        context.Request.Method,
        context.Request.Path,
        context.Request.QueryString);

    await next();

    logger.LogInformation(
        "MCP Response: {StatusCode}",
        context.Response.StatusCode);
});

app.MapMcp("/mcp");
app.MapHealthChecks("/health");

app.Run();
