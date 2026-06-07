using Dapper;

namespace HttpMcpServer.Data;

public static class DatabaseInitializer
{
    public static async Task Initialize(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=data/mcp.db";

        // 使用 SqliteConnectionStringBuilder 安全解析连接字符串
        var csBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        var dbPath = csBuilder.DataSource;
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await connection.OpenAsync();

        // 建表
        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS documents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                Category TEXT NOT NULL DEFAULT 'General',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            )
            """);

        // 检查是否已有数据
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM documents");
        if (count > 0) return;

        // 插入种子数据
        var seedData = new[]
        {
            ("Getting Started with MCP", "The Model Context Protocol (MCP) enables AI models to interact with external tools and data sources through a standardized protocol. This guide covers the basics of setting up an MCP server and connecting it to AI clients.", "Tutorial"),
            ("MCP Server Architecture", "MCP servers expose tools, resources, and prompts to AI clients. The server handles JSON-RPC communication and can be hosted via stdio or HTTP transport. ASP.NET Core integration provides middleware pipeline support.", "Architecture"),
            ("Implementing MCP Tools", "Tools are the primary way MCP servers expose functionality to AI models. Each tool has a name, description, and input schema. Tools can be static methods or use dependency injection for services like databases and APIs.", "Development"),
            ("MCP Authentication Patterns", "MCP servers can use OAuth 2.0 for authentication. The protocol supports automatic discovery via /.well-known/oauth-protected-resource. JWT Bearer tokens validate against JWKS endpoints from identity providers like Auth0.", "Security"),
            ("SQLite with Dapper Best Practices", "Dapper is a lightweight ORM for .NET that extends IDbConnection with simple query methods. Use parameterized queries to prevent SQL injection. For SQLite, use Microsoft.Data.Sqlite for best performance and feature support.", "Database"),
            ("Deploying ASP.NET Core to Azure", "Azure App Service provides managed hosting for ASP.NET Core applications. Use deployment slots for zero-downtime deployments. Configure connection strings and secrets via Azure Key Vault with Managed Identity.", "DevOps"),
            ("C# Record Types and Pattern Matching", "C# record types provide value-based equality and immutability. Combined with pattern matching (switch expressions, property patterns), they enable concise and readable data processing code.", "Development"),
            ("ASP.NET Core Middleware Pipeline", "Middleware components form a pipeline that handles HTTP requests and responses. Built-in middleware includes authentication, authorization, CORS, and routing. Custom middleware can handle cross-cutting concerns like logging and error handling.", "Architecture"),
            ("OAuth 2.0 and OpenID Connect Explained", "OAuth 2.0 provides authorization flows for applications. OpenID Connect adds identity layer on top. Authorization Code Flow with PKCE is recommended for public clients. JWT access tokens contain claims about the user and scopes.", "Security"),
            ("HTTP Streaming and Server-Sent Events", "HTTP streaming enables real-time data delivery without WebSocket complexity. Server-Sent Events (SSE) provide one-way server push over HTTP. Streamable HTTP combines regular POST requests with optional SSE for MCP protocol communication.", "Networking"),
        };

        foreach (var (title, content, category) in seedData)
        {
            await connection.ExecuteAsync(
                "INSERT INTO documents (Title, Content, Category) VALUES (@Title, @Content, @Category)",
                new { Title = title, Content = content, Category = category });
        }
    }
}
