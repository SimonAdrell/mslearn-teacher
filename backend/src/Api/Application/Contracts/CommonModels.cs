namespace StudyCoach.BackendApi.Application.Contracts;

public sealed record Citation(string Title, string Url, DateOnly RetrievedAt);

public sealed record ChatMeta(
    string SkillOutlineArea,
    IReadOnlyList<string> MustKnow,
    IReadOnlyList<string> ExamTraps,
    bool McpVerified,
    IReadOnlyList<string>? WeakAreasUpdate = null);

public sealed record TokenUsageDto(int PromptTokens, int CompletionTokens, int TotalTokens);
