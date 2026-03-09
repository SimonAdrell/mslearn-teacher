namespace StudyCoach.BackendApi.Services;

public static class StudyModes
{
    public static readonly string[] All = ["Learn", "Quiz", "Review mistakes", "Rapid cram"];
}

public record StartSessionRequest(string Mode, string SkillArea);
public record StartSessionResponse(Guid SessionId, string Mode, string SkillArea, string WelcomeMessage);
public record BootstrapSessionResponse(
    Guid SessionId,
    string Message,
    IReadOnlyList<string> AreaOptions,
    IReadOnlyList<string> ModeOptions,
    TokenUsageDto? Usage = null);

public record ChatRequest(Guid SessionId, string Message);
public record ChatResponse(
    string Answer,
    IReadOnlyList<Citation> Citations,
    bool Refused,
    string? RefusalReason,
    ChatMeta? Meta = null,
    TokenUsageDto? Usage = null);

public record QuizNextRequest(Guid SessionId);
public record QuizQuestionResponse(
    Guid QuestionId,
    string Question,
    IReadOnlyList<string>? Choices,
    IReadOnlyList<Citation>? Citations = null,
    TokenUsageDto? Usage = null);

public record QuizAnswerRequest(Guid SessionId, Guid QuestionId, string Answer);
public record QuizAnswerResponse(
    bool Correct,
    string Explanation,
    string MemoryRule,
    IReadOnlyList<Citation> Citations,
    TokenUsageDto? Usage = null);

public record Citation(string Title, string Url, DateOnly RetrievedAt);

public record ChatMeta(
    string SkillOutlineArea,
    IReadOnlyList<string> MustKnow,
    IReadOnlyList<string> ExamTraps,
    bool McpVerified,
    IReadOnlyList<string>? WeakAreasUpdate = null);

public record SkillsOutlineResponse(
    IReadOnlyList<SkillArea> Areas,
    IReadOnlyList<Citation>? Citations = null,
    bool IsFromCache = false);
public record SkillArea(string Name, string WeightPercent, IReadOnlyList<string> Includes);

public record TokenUsageDto(int PromptTokens, int CompletionTokens, int TotalTokens);
