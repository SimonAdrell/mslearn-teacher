using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

namespace StudyCoach.BackendApi.Infrastructure.Foundry;

public interface IOnboardingCoach
{
    Task<CoachOnboardingResult> GetOnboardingOptionsAsync(Guid sessionId, CancellationToken cancellationToken);
}

public interface IChatCoach
{
    Task<CoachChatResult> GetChatReplyAsync(Guid sessionId, string skillArea, string message, CancellationToken cancellationToken);
}

public interface IQuizCoach
{
    Task<QuizQuestionResponse> GetNextQuizQuestionAsync(Guid sessionId, string skillArea, CancellationToken cancellationToken);
    Task<QuizAnswerResponse> GradeQuizAnswerAsync(Guid sessionId, string skillArea, Guid questionId, string answer, CancellationToken cancellationToken);
}

public sealed record CoachOnboardingResult(
    string Prompt,
    IReadOnlyList<string> AreaOptions,
    IReadOnlyList<string> ModeOptions,
    TokenUsageDto? Usage = null);

public sealed record CoachChatResult(
    string Answer,
    IReadOnlyList<Citation> Citations,
    ChatMeta? Meta,
    bool Refused,
    string? RefusalReason,
    TokenUsageDto? Usage = null);

internal sealed record FoundryParseWithUsageResult(ParsedCoachResponse ParseResult, TokenUsageDto? Usage);
