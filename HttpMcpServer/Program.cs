using HttpMcpServer.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Auth0 JWT Bearer 认证
var auth0Domain = builder.Configuration["Auth0:Domain"]
    ?? throw new InvalidOperationException("Auth0:Domain is not configured. Check appsettings.json.");
var auth0Audience = builder.Configuration["Auth0:Audience"] ?? "";

// 启动时加载 JWKS 签名密钥
using var jwksClient = new HttpClient();
var jwksJson = await jwksClient.GetStringAsync($"https://{auth0Domain}/.well-known/jwks.json");
var jwks = new JsonWebKeySet(jwksJson);

var validIssuers = new HashSet<string>
{
    $"https://{auth0Domain}/",
    $"https://{auth0Domain}"
};

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Audience = auth0Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = validIssuers,
            ValidateAudience = true,
            ValidAudiences = [auth0Audience],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            IssuerSigningKeys = jwks.GetSigningKeys()
        };
    });

builder.Services.AddAuthorization();

// MCP 服务器服务
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
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithTools<WeatherTools>()
    .WithTools<CalculatorTools>()
    .WithTools<TimeTools>()
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
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    logger.LogInformation(
        "MCP Request: {Method} {Path}{QueryString} User={User}",
        context.Request.Method,
        context.Request.Path,
        context.Request.QueryString,
        context.User.Identity?.Name ?? "anonymous");

    await next();

    logger.LogInformation(
        "MCP Response: {StatusCode}",
        context.Response.StatusCode);
});

app.MapMcp("/mcp").RequireAuthorization();
app.MapHealthChecks("/health");

app.Run();
