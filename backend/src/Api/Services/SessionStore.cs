using System.Collections.Concurrent;

namespace StudyCoach.BackendApi.Services;

public interface ISessionStore
{
    StudySession StartSession(string mode, string skillArea, string userId);
    bool ExistsForUser(Guid sessionId, string userId);
    bool TryGetSessionForUser(Guid sessionId, string userId, out StudySession? session);
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
        return TryGetSessionForUser(sessionId, userId, out _);
    }

    public bool TryGetSessionForUser(Guid sessionId, string userId, out StudySession? session)
    {
        if (!_sessions.TryGetValue(sessionId, out var existing))
        {
            session = null;
            return false;
        }

        if (!string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            session = null;
            return false;
        }

        session = existing;
        return true;
    }
}

public record StudySession(Guid SessionId, string Mode, string SkillArea, string UserId, DateTimeOffset StartedAt);
