using System.Collections.Concurrent;

namespace HttpMcpServer.Services
{
    public class McpSessionManager
    {
        private readonly ConcurrentDictionary<string, McpSession> _sessions = new();
        private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

        public string CreateSession()
        {
            var sessionId = Guid.NewGuid().ToString("N");
            _sessions[sessionId] = new McpSession
            {
                Id = sessionId,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsInitialized = false
            };
            return sessionId;
        }

        public McpSession? GetSession(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (DateTime.UtcNow - session.LastActivity > _sessionTimeout)
                {
                    _sessions.TryRemove(sessionId, out _);
                    return null;
                }
                session.LastActivity = DateTime.UtcNow;
                return session;
            }
            return null;
        }

        public void SetInitialized(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.IsInitialized = true;
                session.LastActivity = DateTime.UtcNow;
            }
        }

        public void RemoveSession(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        public void CleanupExpiredSessions()
        {
            var expired = _sessions
                .Where(s => DateTime.UtcNow - s.Value.LastActivity > _sessionTimeout)
                .Select(s => s.Key);

            foreach (var sessionId in expired)
            {
                _sessions.TryRemove(sessionId, out _);
            }
        }
    }

    public class McpSession
    {
        public string Id { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public bool IsInitialized { get; set; }
    }
}
