namespace StudyCoach.BackendApi.Application.Contracts;

public sealed record SkillsOutlineResponse(
    IReadOnlyList<SkillArea> Areas,
    IReadOnlyList<Citation>? Citations = null,
    bool IsFromCache = false);

public sealed record SkillArea(string Name, string WeightPercent, IReadOnlyList<string> Includes);
