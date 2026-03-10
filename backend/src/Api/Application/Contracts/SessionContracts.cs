namespace StudyCoach.BackendApi.Application.Contracts;

public sealed record StartSessionRequest(string Mode, string SkillArea);
public sealed record StartSessionResponse(Guid SessionId, string Mode, string SkillArea, string WelcomeMessage);

public sealed record BootstrapSessionResponse(
    Guid SessionId,
    string Message,
    IReadOnlyList<string> AreaOptions,
    IReadOnlyList<string> ModeOptions,
    TokenUsageDto? Usage = null);
