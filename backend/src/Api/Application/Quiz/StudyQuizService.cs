using StudyCoach.BackendApi.Application.Common;
using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Application.Policies;
using StudyCoach.BackendApi.Infrastructure.Foundry;
using StudyCoach.BackendApi.Infrastructure.Sessions;

namespace StudyCoach.BackendApi.Application.Quiz;

public interface IStudyQuizService
{
    Task<ApplicationResult<QuizQuestionResponse>> NextQuestionAsync(QuizNextRequest request, string userId, CancellationToken cancellationToken);
    Task<ApplicationResult<QuizAnswerResponse>> GradeAnswerAsync(QuizAnswerRequest request, string userId, CancellationToken cancellationToken);
}

public sealed class StudyQuizService : IStudyQuizService
{
    private readonly IStudySessionRepository _sessions;
    private readonly IQuizCoach _quizCoach;

    public StudyQuizService(IStudySessionRepository sessions, IQuizCoach quizCoach)
    {
        _sessions = sessions;
        _quizCoach = quizCoach;
    }

    public async Task<ApplicationResult<QuizQuestionResponse>> NextQuestionAsync(QuizNextRequest request, string userId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetForUser(request.SessionId, userId, out var session) || session is null)
        {
            return ApplicationResult<QuizQuestionResponse>.Failure("session_not_found", "Session not found.");
        }

        var question = await _quizCoach.GetNextQuizQuestionAsync(request.SessionId, session.SkillArea, cancellationToken);
        return ApplicationResult<QuizQuestionResponse>.Success(LearnContentPolicy.ToQuestionResponse(question));
    }

    public async Task<ApplicationResult<QuizAnswerResponse>> GradeAnswerAsync(QuizAnswerRequest request, string userId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetForUser(request.SessionId, userId, out var session) || session is null)
        {
            return ApplicationResult<QuizAnswerResponse>.Failure("session_not_found", "Session not found.");
        }

        var feedback = await _quizCoach.GradeQuizAnswerAsync(request.SessionId, session.SkillArea, request.QuestionId, request.Answer, cancellationToken);
        return ApplicationResult<QuizAnswerResponse>.Success(LearnContentPolicy.ToAnswerResponse(feedback));
    }
}
