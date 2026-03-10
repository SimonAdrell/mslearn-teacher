using System.Text.Json.Serialization;

namespace StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

internal sealed class CoachPayload
{
    [JsonPropertyName("coach_text")]
    public string? CoachText { get; init; }

    [JsonPropertyName("response_type")]
    public string? ResponseType { get; init; }

    [JsonPropertyName("skill_outline_area")]
    public string? SkillOutlineArea { get; init; }

    [JsonPropertyName("must_know")]
    public IReadOnlyList<string>? MustKnow { get; init; }

    [JsonPropertyName("exam_traps")]
    public IReadOnlyList<string>? ExamTraps { get; init; }

    [JsonPropertyName("citations")]
    public IReadOnlyList<CoachCitationPayload>? Citations { get; init; }

    [JsonPropertyName("quiz")]
    public CoachQuizPayload? Quiz { get; init; }

    [JsonPropertyName("onboarding")]
    public CoachOnboardingPayload? Onboarding { get; init; }

    [JsonPropertyName("weak_areas_update")]
    public IReadOnlyList<string>? WeakAreasUpdate { get; init; }

    [JsonPropertyName("mcp_verified")]
    public bool? McpVerified { get; init; }
}

internal sealed class CoachCitationPayload
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("retrieved_at")]
    public string? RetrievedAt { get; init; }
}

internal sealed class CoachQuizPayload
{
    [JsonPropertyName("question")]
    public string? Question { get; init; }

    [JsonPropertyName("options")]
    public Dictionary<string, string>? Options { get; init; }

    [JsonPropertyName("correct_option")]
    public string? CorrectOption { get; init; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; init; }

    [JsonPropertyName("memory_rule")]
    public string? MemoryRule { get; init; }
}

internal sealed class CoachOnboardingPayload
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    [JsonPropertyName("area_options")]
    public IReadOnlyList<string>? AreaOptions { get; init; }

    [JsonPropertyName("mode_options")]
    public IReadOnlyList<string>? ModeOptions { get; init; }
}
