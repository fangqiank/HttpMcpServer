using HttpMcpServer.Models;
using HttpMcpServer.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HttpMcpServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class McpController : ControllerBase
    {
        private readonly IMcpToolHandler _toolHandler;
        private readonly McpSessionManager _sessionManager;
        private readonly ILogger<McpController> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public McpController(
            IMcpToolHandler toolHandler,
            McpSessionManager sessionManager,
            ILogger<McpController> logger)
        {
            _toolHandler = toolHandler;
            _sessionManager = sessionManager;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        [HttpGet]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", server = "HTTP MCP Server" });
        }

        [HttpPost("mcp")]
        public async Task<IActionResult> HandleMcpRequest()
        {
            // Get or create session
            var sessionId = GetOrCreateSession();

            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(body))
                {
                    return BadRequest(ErrorResponse(null, -32700, "Empty request"));
                }

                McpRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<McpRequest>(body, _jsonOptions);
                }
                catch (JsonException)
                {
                    return BadRequest(ErrorResponse(null, -32700, "Parse error"));
                }

                if (request == null)
                {
                    return BadRequest(ErrorResponse(null, -32600, "Invalid Request"));
                }

                _logger.LogInformation("Received MCP request: {Method} (id: {Id})", request.Method, request.Id);

                // Handle the request based on method
                var response = await ProcessMcpMethod(request, sessionId);

                // Set session header
                Response.Headers["Mcp-Session-Id"] = sessionId;

                return new JsonResult(response, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MCP request");
                return StatusCode(500, ErrorResponse(null, -32603, "Internal error"));
            }
        }

        [HttpGet("mcp/sse")]
        public async Task StreamMcpEvents([FromQuery] string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                Response.StatusCode = 400;
                return;
            }

            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                Response.StatusCode = 404;
                return;
            }

            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            // Send initial connection event
            var initEvent = new McpNotification
            {
                Method = "connection/established",
                Params = JsonSerializer.SerializeToElement(new { sessionId })
            };

            await Response.WriteAsync($"data: {JsonSerializer.Serialize(initEvent, _jsonOptions)}\n\n");
            await Response.Body.FlushAsync();

            // Keep connection alive and listen for events
            // In a real implementation, you'd use a messaging system or event bus
            try
            {
                var cancellationToken = HttpContext.RequestAborted;

                // Send keepalive every 30 seconds
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(30000, cancellationToken);
                    await Response.WriteAsync($": keepalive\n\n");
                    await Response.Body.FlushAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected
            }
        }

        private async Task<McpResponse> ProcessMcpMethod(McpRequest request, string sessionId)
        {
            var session = _sessionManager.GetSession(sessionId);

            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "initialized" => HandleInitialized(request, sessionId),
                "tools/list" => await HandleListTools(request),
                "tools/call" => await HandleCallTool(request),
                "ping" => HandlePing(request),
                _ => ErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
            };
        }

        private McpResponse HandleInitialize(McpRequest request)
        {
            var result = new InitializeResult
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new McpCapabilities
                {
                    Tools = new ToolCapabilities { ListChanged = false }
                },
                ServerInfo = new ServerInfo
                {
                    Name = "HttpMcpServer",
                    Version = "1.0.0"
                }
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToElement(result, _jsonOptions)
            };
        }

        private McpResponse HandleInitialized(McpRequest request, string sessionId)
        {
            _sessionManager.SetInitialized(sessionId);

            return new McpResponse
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToElement(new { }, _jsonOptions)
            };
        }

        private async Task<McpResponse> HandleListTools(McpRequest request)
        {
            var tools = await _toolHandler.ListToolsAsync();

            return new McpResponse
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToElement(tools, _jsonOptions)
            };
        }

        private async Task<McpResponse> HandleCallTool(McpRequest request)
        {
            try
            {
                if (!request.Params.HasValue)
                {
                    return ErrorResponse(request.Id, -32602, "Missing params");
                }

                var callParams = JsonSerializer.Deserialize<ToolCallParams>(
                    request.Params.Value.GetRawText(), _jsonOptions);

                if (callParams == null)
                {
                    return ErrorResponse(request.Id, -32602, "Invalid params");
                }

                var result = await _toolHandler.CallToolAsync(callParams.Name, callParams.Arguments);

                return new McpResponse
                {
                    Id = request.Id,
                    Result = JsonSerializer.SerializeToElement(result, _jsonOptions)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling tool");
                return ErrorResponse(request.Id, -32603, $"Tool execution error: {ex.Message}");
            }
        }

        private McpResponse HandlePing(McpRequest request)
        {
            return new McpResponse
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToElement(new { }, _jsonOptions)
            };
        }

        private string GetOrCreateSession()
        {
            if (Request.Headers.TryGetValue("Mcp-Session-Id", out var sessionId) && !string.IsNullOrEmpty(sessionId))
            {
                var existingSession = _sessionManager.GetSession(sessionId!);
                if (existingSession != null)
                {
                    return sessionId!;
                }
            }

            return _sessionManager.CreateSession();
        }

        private static McpResponse ErrorResponse(string? id, int code, string message)
        {
            return new McpResponse
            {
                Id = id,
                Error = new McpError
                {
                    Code = code,
                    Message = message
                }
            };
        }

    }
}
