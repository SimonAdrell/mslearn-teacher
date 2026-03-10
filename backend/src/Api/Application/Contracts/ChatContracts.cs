namespace StudyCoach.BackendApi.Application.Contracts;

public sealed record ChatRequest(Guid SessionId, string Message);

public sealed record ChatResponse(
    string Answer,
    IReadOnlyList<Citation> Citations,
    bool Refused,
    string? RefusalReason,
    ChatMeta? Meta = null,
    TokenUsageDto? Usage = null);
