using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace HttpMcpServer.Tools
{
    public class DatabaseTools
    {
        private readonly ILogger<DatabaseTools> _logger;
        private readonly IConfiguration _configuration;

        public DatabaseTools(ILogger<DatabaseTools> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [McpServerTool(Name = "search_documents")]
        [Description("Search through document database")]
        [Authorize]
        public async Task<string> SearchDocuments(
            [Description("Search query")] string query,
            [Description("Maximum number of results")] int limit = 5)
        {
            _logger.LogInformation("Searching documents for: {Query}", query);

            // 模拟数据库搜索
            await Task.Delay(100);

            var results = new[]
            {
                $"Document about {query} - Overview",
                $"Getting started with {query}",
                $"Advanced {query} techniques",
                $"{query} API reference",
                $"Troubleshooting {query} issues"
            };

            var limitedResults = results.Take(limit);
            return $"Search results for '{query}':\n{string.Join("\n", limitedResults.Select((r, i) => $"{i + 1}. {r}"))}";
        }
    }
}
