using System.Collections.Concurrent;

namespace StudyCoach.BackendApi.Services;

public interface ISessionStore
{
    StudySession StartSession(string mode, string skillArea, string userId);
    bool ExistsForUser(Guid sessionId, string userId);
}

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<Guid, StudySession> _sessions = new();

    public StudySession StartSession(string mode, string skillArea, string userId)
    {
        var session = new StudySession(Guid.NewGuid(), mode, skillArea, userId, DateTimeOffset.UtcNow);
        _sessions[session.SessionId] = session;
        return session;
    }

    public bool ExistsForUser(Guid sessionId, string userId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        return string.Equals(session.UserId, userId, StringComparison.Ordinal);
    }
}

public record StudySession(Guid SessionId, string Mode, string SkillArea, string UserId, DateTimeOffset StartedAt);
