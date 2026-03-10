using StudyCoach.BackendApi.Application.Common;
using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Application.Policies;
using StudyCoach.BackendApi.Infrastructure.Foundry;
using StudyCoach.BackendApi.Infrastructure.Sessions;

namespace StudyCoach.BackendApi.Application.Chat;

public interface IStudyChatService
{
    Task<ApplicationResult<ChatResponse>> ReplyAsync(ChatRequest request, string userId, CancellationToken cancellationToken);
}

public sealed class StudyChatService : IStudyChatService
{
    private readonly IStudySessionRepository _sessions;
    private readonly IChatCoach _chatCoach;

    public StudyChatService(IStudySessionRepository sessions, IChatCoach chatCoach)
    {
        _sessions = sessions;
        _chatCoach = chatCoach;
    }

    public async Task<ApplicationResult<ChatResponse>> ReplyAsync(ChatRequest request, string userId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetForUser(request.SessionId, userId, out var session) || session is null)
        {
            return ApplicationResult<ChatResponse>.Failure("session_not_found", "Session not found.");
        }

        var chatResult = await _chatCoach.GetChatReplyAsync(request.SessionId, session.SkillArea, request.Message, cancellationToken);
        return ApplicationResult<ChatResponse>.Success(LearnContentPolicy.ToChatResponse(chatResult));
    }
}
