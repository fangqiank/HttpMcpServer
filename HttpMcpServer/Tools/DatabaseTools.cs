using System.ComponentModel;
using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace HttpMcpServer.Tools
{
    public class DatabaseTools
    {
        private readonly ILogger<DatabaseTools> _logger;
        private readonly IDbConnection _db;

        public DatabaseTools(ILogger<DatabaseTools> logger, IDbConnection db)
        {
            _logger = logger;
            _db = db;
        }

        [McpServerTool(Name = "search_documents")]
        [Description("Search documents by keyword in title, content, or category")]
        [Authorize]
        public async Task<string> SearchDocuments(
            [Description("Search keyword")] string query,
            [Description("Maximum number of results (1-50)")] int limit = 5)
        {
            _logger.LogInformation("Searching documents for: {Query}", query);

            var sql = """
                SELECT Id, Title, Content, Category, CreatedAt, UpdatedAt
                FROM documents
                WHERE Title LIKE '%' || @Query || '%'
                   OR Content LIKE '%' || @Query || '%'
                   OR Category LIKE '%' || @Query || '%'
                ORDER BY UpdatedAt DESC
                LIMIT @Limit
                """;

            var rows = await _db.QueryAsync(sql, new { Query = query, Limit = Math.Clamp(limit, 1, 50) });
            var docs = rows.Select(r => (dynamic)r).ToList();

            if (docs.Count == 0)
                return $"No documents found matching '{query}'.";

            var lines = docs.Select((d, i) =>
                $"{i + 1}. [{d.Category}] {d.Title} (ID:{d.Id}, updated: {d.UpdatedAt})");
            return $"Found {docs.Count} document(s) for '{query}':\n{string.Join("\n", lines)}";
        }

        [McpServerTool(Name = "get_document")]
        [Description("Get a specific document by ID")]
        [Authorize]
        public async Task<string> GetDocument(
            [Description("Document ID")] int id)
        {
            var sql = "SELECT * FROM documents WHERE Id = @Id";
            var row = await _db.QueryFirstOrDefaultAsync(sql, new { Id = id });

            if (row == null)
                return $"Document with ID {id} not found.";

            var d = (dynamic)row;
            return $"ID: {d.Id}\nTitle: {d.Title}\nCategory: {d.Category}\nCreated: {d.CreatedAt}\nUpdated: {d.UpdatedAt}\n\n{d.Content}";
        }

        [McpServerTool(Name = "list_documents")]
        [Description("List documents with pagination")]
        [Authorize]
        public async Task<string> ListDocuments(
            [Description("Page number (starts from 1)")] int page = 1,
            [Description("Items per page (1-50)")] int pageSize = 10)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            var offset = (Math.Max(page, 1) - 1) * pageSize;

            var sql = """
                SELECT Id, Title, Category, UpdatedAt, COUNT(*) OVER() AS TotalCount
                FROM documents
                ORDER BY UpdatedAt DESC
                LIMIT @PageSize OFFSET @Offset
                """;

            var rows = await _db.QueryAsync(sql, new { PageSize = pageSize, Offset = offset });
            var docs = rows.Select(r => (dynamic)r).ToList();

            var total = docs.Count > 0 ? (int)docs[0].TotalCount : 0;
            var totalPages = (int)Math.Ceiling((double)total / pageSize);

            var lines = docs.Select((d, i) =>
                $"{offset + i + 1}. [{d.Category}] {d.Title} (ID:{d.Id}, updated: {d.UpdatedAt})");

            return $"Page {page}/{totalPages} (total: {total} documents):\n{string.Join("\n", lines)}";
        }

        [McpServerTool(Name = "create_document")]
        [Description("Create a new document")]
        [Authorize]
        public async Task<string> CreateDocument(
            [Description("Document title")] string title,
            [Description("Document content")] string content,
            [Description("Document category")] string category = "General")
        {
            var sql = """
                INSERT INTO documents (Title, Content, Category)
                VALUES (@Title, @Content, @Category);
                SELECT last_insert_rowid();
                """;

            var id = await _db.ExecuteScalarAsync<int>(sql, new { Title = title, Content = content, Category = category });
            _logger.LogInformation("Created document ID: {Id}", id);

            return $"Document created successfully. ID: {id}";
        }

        [McpServerTool(Name = "update_document")]
        [Description("Update an existing document. Only provided fields will be changed.")]
        [Authorize]
        public async Task<string> UpdateDocument(
            [Description("Document ID")] int id,
            [Description("New title (null to keep current)")] string? title = null,
            [Description("New content (null to keep current)")] string? content = null,
            [Description("New category (null to keep current)")] string? category = null)
        {
            // 先获取当前文档
            var current = await _db.QueryFirstOrDefaultAsync(
                "SELECT Title, Content, Category FROM documents WHERE Id = @Id", new { Id = id });
            if (current == null)
                return $"Document with ID {id} not found.";

            var d = (dynamic)current;
            var finalTitle = title ?? (string)d.Title;
            var finalContent = content ?? (string)d.Content;
            var finalCategory = category ?? (string)d.Category;

            var sql = """
                UPDATE documents
                SET Title = @Title, Content = @Content, Category = @Category, UpdatedAt = datetime('now')
                WHERE Id = @Id;
                SELECT changes();
                """;

            var affected = await _db.ExecuteScalarAsync<int>(sql,
                new { Id = id, Title = finalTitle, Content = finalContent, Category = finalCategory });

            _logger.LogInformation("Updated document ID: {Id}", id);
            return $"Document {id} updated successfully.";
        }

        [McpServerTool(Name = "delete_document")]
        [Description("Delete a document by ID")]
        [Authorize]
        public async Task<string> DeleteDocument(
            [Description("Document ID")] int id)
        {
            var sql = """
                DELETE FROM documents WHERE Id = @Id;
                SELECT changes();
                """;

            var affected = await _db.ExecuteScalarAsync<int>(sql, new { Id = id });

            if (affected == 0)
                return $"Document with ID {id} not found.";

            _logger.LogInformation("Deleted document ID: {Id}", id);
            return $"Document {id} deleted successfully.";
        }

        [McpServerTool(Name = "count_documents")]
        [Description("Get total document count")]
        [Authorize]
        public async Task<string> CountDocuments()
        {
            var total = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM documents");
            var categories = await _db.QueryAsync("""
                SELECT Category, COUNT(*) as Count
                FROM documents
                GROUP BY Category
                ORDER BY Count DESC
                """);

            var catLines = categories.Select(r => (dynamic)r)
                .Select(c => $"  {c.Category}: {c.Count}");

            return $"Total documents: {total}\nBy category:\n{string.Join("\n", catLines)}";
        }
    }
}
