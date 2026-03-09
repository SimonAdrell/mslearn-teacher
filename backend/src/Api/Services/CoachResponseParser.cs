using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudyCoach.BackendApi.Services;

internal static class CoachResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CoachParseResult Parse(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return CoachParseResult.Invalid("Foundry response did not include content.");
        }

        var trimmed = rawOutput.Trim();

        CoachMetaPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CoachMetaPayload>(trimmed, JsonOptions);
        }
        catch (JsonException)
        {
            return CoachParseResult.Invalid("Foundry response must be a single JSON object.");
        }

        if (payload is null)
        {
            return CoachParseResult.Invalid("Foundry response JSON object is empty.");
        }

        var coachText = payload.CoachText?.Trim();
        if (string.IsNullOrWhiteSpace(coachText))
        {
            return CoachParseResult.Invalid("Foundry response JSON coach_text is required.");
        }

        var responseType = payload.ResponseType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(responseType))
        {
            return CoachParseResult.Invalid("Foundry response JSON response_type is required.");
        }

        var citationsResult = ParseAndValidateCitations(payload.Citations);
        if (!citationsResult.IsValid)
        {
            return CoachParseResult.Invalid(citationsResult.Error!);
        }

        if (!string.Equals(responseType, "refusal", StringComparison.Ordinal) && payload.McpVerified is not true)
        {
            return CoachParseResult.Invalid("Foundry response JSON mcp_verified must be true for substantive responses.");
        }

        if (!string.Equals(responseType, "refusal", StringComparison.Ordinal) && citationsResult.Citations.Count == 0)
        {
            return CoachParseResult.Invalid("Foundry response JSON citations must include at least one Learn citation.");
        }

        if (string.IsNullOrWhiteSpace(payload.SkillOutlineArea) && !string.Equals(responseType, "refusal", StringComparison.Ordinal))
        {
            return CoachParseResult.Invalid("Foundry response JSON skill_outline_area is required.");
        }

        var mustKnow = NormalizeStringList(payload.MustKnow);
        var examTraps = NormalizeStringList(payload.ExamTraps);
        var weakAreasUpdate = NormalizeStringList(payload.WeakAreasUpdate);

        ChatMeta? chatMeta = null;
        if (!string.Equals(responseType, "refusal", StringComparison.Ordinal))
        {
            chatMeta = new ChatMeta(
                payload.SkillOutlineArea!.Trim(),
                mustKnow,
                examTraps,
                payload.McpVerified ?? false,
                weakAreasUpdate.Count > 0 ? weakAreasUpdate : null);
        }

        var quizResult = ParseQuiz(payload.Quiz);
        if (!quizResult.IsValid)
        {
            return CoachParseResult.Invalid(quizResult.Error!);
        }

        return new CoachParseResult(
            coachText,
            citationsResult.Citations,
            chatMeta,
            quizResult.Quiz,
            responseType,
            null);
    }

    private static CoachCitationsResult ParseAndValidateCitations(IReadOnlyList<CoachCitationPayload>? citations)
    {
        var parsed = new List<Citation>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (citations is null)
        {
            return new CoachCitationsResult(parsed, true, null);
        }

        foreach (var citation in citations)
        {
            if (string.IsNullOrWhiteSpace(citation.Title) ||
                string.IsNullOrWhiteSpace(citation.Url) ||
                string.IsNullOrWhiteSpace(citation.RetrievedAt))
            {
                return CoachCitationsResult.Invalid("Foundry response JSON citations entries must include title, url, and retrieved_at.");
            }

            if (!Uri.TryCreate(citation.Url, UriKind.Absolute, out var parsedUrl) ||
                (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
            {
                return CoachCitationsResult.Invalid("Foundry response JSON citations includes an invalid URL.");
            }

            if (!IsLearnHost(parsedUrl.Host))
            {
                return CoachCitationsResult.Invalid("Foundry response JSON citations must point to Microsoft Learn domains.");
            }

            if (!DateOnly.TryParseExact(citation.RetrievedAt, "yyyy-MM-dd", out var retrievedAt))
            {
                return CoachCitationsResult.Invalid("Foundry response JSON citations retrieved_at must use YYYY-MM-DD.");
            }

            if (!seenUrls.Add(parsedUrl.ToString()))
            {
                continue;
            }

            parsed.Add(new Citation(citation.Title.Trim(), parsedUrl.ToString(), retrievedAt));
        }

        return new CoachCitationsResult(parsed, true, null);
    }

    private static bool IsLearnHost(string host)
    {
        return host.Equals("learn.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".learn.microsoft.com", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> NormalizeStringList(IReadOnlyList<string>? source)
    {
        if (source is null || source.Count == 0)
        {
            return [];
        }

        return source
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CoachQuizResult ParseQuiz(CoachQuizPayload? quiz)
    {
        if (quiz is null)
        {
            return CoachQuizResult.Valid(null);
        }

        if (string.IsNullOrWhiteSpace(quiz.Question))
        {
            return CoachQuizResult.Invalid("Foundry response JSON quiz.question is required when quiz is present.");
        }

        if (quiz.Options is null)
        {
            return CoachQuizResult.Invalid("Foundry response JSON quiz.options is required when quiz is present.");
        }

        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "A", "B", "C" })
        {
            if (!quiz.Options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return CoachQuizResult.Invalid("Foundry response JSON quiz.options must include non-empty A, B, and C options.");
            }

            options[key] = value.Trim();
        }

        string? correctOption = null;
        if (!string.IsNullOrWhiteSpace(quiz.CorrectOption))
        {
            correctOption = quiz.CorrectOption.Trim().ToUpperInvariant();
            if (correctOption is not ("A" or "B" or "C"))
            {
                return CoachQuizResult.Invalid("Foundry response JSON quiz.correct_option must be A, B, or C when provided.");
            }
        }

        return CoachQuizResult.Valid(new CoachQuizMeta(
            quiz.Question.Trim(),
            options,
            correctOption,
            quiz.Explanation?.Trim(),
            quiz.MemoryRule?.Trim()));
    }
}

internal sealed record CoachParseResult(
    string CoachText,
    IReadOnlyList<Citation> Citations,
    ChatMeta? ChatMeta,
    CoachQuizMeta? Quiz,
    string ResponseType,
    string? Error)
{
    public bool IsValid => string.IsNullOrWhiteSpace(Error);

    public static CoachParseResult Invalid(string error) =>
        new(string.Empty, [], null, null, string.Empty, error);
}

internal sealed record CoachQuizMeta(
    string Question,
    IReadOnlyDictionary<string, string> Options,
    string? CorrectOption,
    string? Explanation,
    string? MemoryRule);

internal sealed record CoachCitationsResult(IReadOnlyList<Citation> Citations, bool IsValid, string? Error)
{
    public static CoachCitationsResult Invalid(string error) => new([], false, error);
}

internal sealed record CoachQuizResult(CoachQuizMeta? Quiz, bool IsValid, string? Error)
{
    public static CoachQuizResult Valid(CoachQuizMeta? quiz) => new(quiz, true, null);

    public static CoachQuizResult Invalid(string error) => new(null, false, error);
}

internal sealed class CoachMetaPayload
{
    [JsonPropertyName("coach_text")]
    public string? CoachText { get; init; }

    [JsonPropertyName("response_type")]
    public string? ResponseType { get; init; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }

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
