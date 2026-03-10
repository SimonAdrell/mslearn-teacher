namespace StudyCoach.BackendApi.Application.Contracts;

public sealed record QuizNextRequest(Guid SessionId);

public sealed record QuizQuestionResponse(
    Guid QuestionId,
    string Question,
    IReadOnlyList<string>? Choices,
    IReadOnlyList<Citation>? Citations = null,
    TokenUsageDto? Usage = null);

public sealed record QuizAnswerRequest(Guid SessionId, Guid QuestionId, string Answer);

public sealed record QuizAnswerResponse(
    bool Correct,
    string Explanation,
    string MemoryRule,
    IReadOnlyList<Citation> Citations,
    TokenUsageDto? Usage = null);
