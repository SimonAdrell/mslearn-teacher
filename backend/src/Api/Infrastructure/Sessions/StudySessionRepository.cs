using System.Collections.Concurrent;
using StudyCoach.BackendApi.Domain.Study;

namespace StudyCoach.BackendApi.Infrastructure.Sessions;

public interface IStudySessionRepository
{
    StudySession Start(string mode, string skillArea, string userId);
    bool TryGetForUser(Guid sessionId, string userId, out StudySession? session);
}

public sealed class InMemoryStudySessionRepository : IStudySessionRepository
{
    private readonly ConcurrentDictionary<Guid, StudySession> _sessions = new();

    public StudySession Start(string mode, string skillArea, string userId)
    {
        var session = new StudySession(Guid.NewGuid(), mode, skillArea, userId, DateTimeOffset.UtcNow);
        _sessions[session.SessionId] = session;
        return session;
    }

    public bool TryGetForUser(Guid sessionId, string userId, out StudySession? session)
    {
        if (!_sessions.TryGetValue(sessionId, out var existing) || !string.Equals(existing.UserId, userId, StringComparison.Ordinal))
        {
            session = null;
            return false;
        }

        session = existing;
        return true;
    }
}
