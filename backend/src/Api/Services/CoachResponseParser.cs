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

        CoachResponsePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CoachResponsePayload>(rawOutput.Trim(), JsonOptions);
        }
        catch (JsonException)
        {
            return CoachParseResult.Invalid("Foundry response is not valid JSON.");
        }

        if (payload is null)
        {
            return CoachParseResult.Invalid("Foundry response JSON object is empty.");
        }

        if (payload is not { ResponseType: not null })
        {
            return CoachParseResult.Invalid("Foundry response response_type is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.Purpose))
        {
            return CoachParseResult.Invalid("Foundry response purpose is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.CoachText))
        {
            return CoachParseResult.Invalid("Foundry response coach_text is required.");
        }

        if (payload.McpVerified is null)
        {
            return CoachParseResult.Invalid("Foundry response mcp_verified is required.");
        }

        var citationsResult = ParseAndValidateCitations(payload.Citations);
        if (!citationsResult.IsValid)
        {
            return CoachParseResult.Invalid(citationsResult.Error!);
        }

        var classification = ClassifyResponse(payload, citationsResult.Citations);
        if (!classification.IsValid)
        {
            return CoachParseResult.Invalid(classification.Error!);
        }

        return CoachParseResult.Valid(classification.Response!);
    }

    private static CoachClassificationResult ClassifyResponse(
        CoachResponsePayload payload,
        IReadOnlyList<Citation> citations)
    {
        return payload switch
        {
            { ResponseType: CoachResponseType.OnboardingOptions, Onboarding: null } => CoachClassificationResult.Invalid(
                "Foundry response onboarding is required when response_type is onboarding_options."),

            { ResponseType: CoachResponseType.OnboardingOptions, Onboarding: not null } p =>
                ClassifyOnboarding(p, p.Onboarding!),

            { ResponseType: CoachResponseType.QuizQuestion, Quiz: null } => CoachClassificationResult.Invalid(
                "Foundry response quiz is required when response_type is quiz_question."),

            { ResponseType: CoachResponseType.QuizQuestion, Quiz: not null } p =>
                ClassifyQuizQuestion(p, citations, p.Quiz!),

            { ResponseType: CoachResponseType.QuizFeedback, Quiz: null } => CoachClassificationResult.Invalid(
                "Foundry response quiz is required when response_type is quiz_feedback."),

            { ResponseType: CoachResponseType.QuizFeedback, Quiz: not null } p =>
                ClassifyQuizFeedback(p, citations, p.Quiz!),

            { ResponseType: CoachResponseType.Refusal } p => ClassifyRefusal(p, citations),

            { ResponseType: CoachResponseType.Teach or CoachResponseType.Review or CoachResponseType.Cram } p =>
                ClassifyTeachLike(p, citations),

            _ => CoachClassificationResult.Invalid($"Foundry response response_type '{payload.ResponseType}' is unsupported.")
        };
    }

    private static CoachClassificationResult ClassifyOnboarding(CoachResponsePayload payload, CoachOnboardingPayload onboarding)
    {
        var areaOptions = NormalizeStringList(onboarding.AreaOptions);
        if (areaOptions.Count == 0)
        {
            return CoachClassificationResult.Invalid("Foundry response onboarding.area_options must include at least one option.");
        }

        var modeOptions = NormalizeStringList(onboarding.ModeOptions);
        if (modeOptions.Count == 0)
        {
            return CoachClassificationResult.Invalid("Foundry response onboarding.mode_options must include at least one option.");
        }

        if (modeOptions.Any(mode => !StudyModes.All.Contains(mode, StringComparer.OrdinalIgnoreCase)))
        {
            return CoachClassificationResult.Invalid("Foundry response onboarding.mode_options must use supported study modes.");
        }

        var prompt = string.IsNullOrWhiteSpace(onboarding.Prompt) ? payload.CoachText : onboarding.Prompt.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = "Let's start your AI-102 session. Pick a Skill Outline Area.";
        }

        return CoachClassificationResult.Valid(new CoachParsedOnboarding(prompt, areaOptions, modeOptions));
    }

    private static CoachClassificationResult ClassifyQuizQuestion(
        CoachResponsePayload payload,
        IReadOnlyList<Citation> citations,
        CoachQuizPayload quiz)
    {
        var guard = ValidateSubstantiveEnvelope(payload, citations);
        if (!guard.IsValid)
        {
            return guard;
        }

        if (string.IsNullOrWhiteSpace(quiz.Question))
        {
            return CoachClassificationResult.Invalid("Foundry response quiz.question is required when response_type is quiz_question.");
        }

        if (quiz.Options is null)
        {
            return CoachClassificationResult.Invalid("Foundry response quiz.options is required when response_type is quiz_question.");
        }

        if (quiz.Options.ExtensionData?.Count > 0)
        {
            return CoachClassificationResult.Invalid("Foundry response quiz.options must include exactly A, B, and C options.");
        }

        if (string.IsNullOrWhiteSpace(quiz.Options.A) ||
            string.IsNullOrWhiteSpace(quiz.Options.B) ||
            string.IsNullOrWhiteSpace(quiz.Options.C))
        {
            return CoachClassificationResult.Invalid("Foundry response quiz.options must include non-empty A, B, and C options.");
        }

        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = quiz.Options.A.Trim(),
            ["B"] = quiz.Options.B.Trim(),
            ["C"] = quiz.Options.C.Trim()
        };

        var meta = CreateChatMeta(payload);
        return CoachClassificationResult.Valid(new CoachParsedQuizQuestion(
            payload.CoachText!.Trim(),
            citations,
            meta,
            new CoachQuizMeta(
                quiz.Question.Trim(),
                options,
                null,
                quiz.Explanation?.Trim(),
                quiz.MemoryRule?.Trim())));
    }

    private static CoachClassificationResult ClassifyQuizFeedback(
        CoachResponsePayload payload,
        IReadOnlyList<Citation> citations,
        CoachQuizPayload quiz)
    {
        var guard = ValidateSubstantiveEnvelope(payload, citations);
        if (!guard.IsValid)
        {
            return guard;
        }

        if (quiz.CorrectOption is null)
        {
            return CoachClassificationResult.Invalid("Foundry response quiz.correct_option is required when response_type is quiz_feedback.");
        }

        if (string.IsNullOrWhiteSpace(quiz.Explanation))
        {
            return CoachClassificationResult.Invalid("Foundry response quiz.explanation is required when response_type is quiz_feedback.");
        }

        if (string.IsNullOrWhiteSpace(quiz.MemoryRule))
        {
            return CoachClassificationResult.Invalid("Foundry response quiz.memory_rule is required when response_type is quiz_feedback.");
        }

        var meta = CreateChatMeta(payload);
        return CoachClassificationResult.Valid(new CoachParsedQuizFeedback(
            payload.CoachText!.Trim(),
            citations,
            meta,
            new CoachQuizMeta(
                quiz.Question?.Trim() ?? string.Empty,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                quiz.CorrectOption.Value.ToString(),
                quiz.Explanation.Trim(),
                quiz.MemoryRule.Trim())));
    }

    private static CoachClassificationResult ClassifyTeachLike(
        CoachResponsePayload payload,
        IReadOnlyList<Citation> citations)
    {
        var guard = ValidateSubstantiveEnvelope(payload, citations);
        if (!guard.IsValid)
        {
            return guard;
        }

        var meta = CreateChatMeta(payload);
        return CoachClassificationResult.Valid(new CoachParsedTeachLike(
            payload.ResponseType!.Value.ToString().ToLowerInvariant(),
            payload.CoachText!.Trim(),
            citations,
            meta));
    }

    private static CoachClassificationResult ClassifyRefusal(CoachResponsePayload payload, IReadOnlyList<Citation> citations)
    {
        if (payload.McpVerified is not false)
        {
            return CoachClassificationResult.Invalid("Foundry response mcp_verified must be false when response_type is refusal.");
        }

        return CoachClassificationResult.Valid(new CoachParsedRefusal(payload.CoachText!.Trim(), citations));
    }

    private static CoachClassificationResult ValidateSubstantiveEnvelope(CoachResponsePayload payload, IReadOnlyList<Citation> citations)
    {
        if (payload.McpVerified is not true)
        {
            return CoachClassificationResult.Invalid("Foundry response mcp_verified must be true for substantive responses.");
        }

        if (citations.Count == 0)
        {
            return CoachClassificationResult.Invalid("Foundry response citations must include at least one Learn citation.");
        }

        if (string.IsNullOrWhiteSpace(payload.SkillOutlineArea))
        {
            return CoachClassificationResult.Invalid("Foundry response skill_outline_area is required.");
        }

        var mustKnow = NormalizeStringList(payload.MustKnow);
        if (mustKnow.Count == 0)
        {
            return CoachClassificationResult.Invalid("Foundry response must_know must include at least one item.");
        }

        var examTraps = NormalizeStringList(payload.ExamTraps);
        if (examTraps.Count == 0)
        {
            return CoachClassificationResult.Invalid("Foundry response exam_traps must include at least one item.");
        }

        return CoachClassificationResult.Valid(null);
    }

    private static ChatMeta CreateChatMeta(CoachResponsePayload payload)
    {
        var mustKnow = NormalizeStringList(payload.MustKnow);
        var examTraps = NormalizeStringList(payload.ExamTraps);
        var weakAreasUpdate = NormalizeStringList(payload.WeakAreasUpdate);

        return new ChatMeta(
            payload.SkillOutlineArea!.Trim(),
            mustKnow,
            examTraps,
            payload.McpVerified ?? false,
            weakAreasUpdate.Count > 0 ? weakAreasUpdate : null);
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
                return CoachCitationsResult.Invalid("Foundry response citations entries must include title, url, and retrieved_at.");
            }

            if (!Uri.TryCreate(citation.Url, UriKind.Absolute, out var parsedUrl) ||
                (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
            {
                return CoachCitationsResult.Invalid("Foundry response citations includes an invalid URL.");
            }

            if (!IsLearnHost(parsedUrl.Host))
            {
                return CoachCitationsResult.Invalid("Foundry response citations must point to Microsoft Learn domains.");
            }

            if (!DateOnly.TryParseExact(citation.RetrievedAt, "yyyy-MM-dd", out var retrievedAt))
            {
                return CoachCitationsResult.Invalid("Foundry response citations retrieved_at must use YYYY-MM-DD.");
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
}

internal sealed record CoachParseResult(CoachParsedResponse? Response, string? Error)
{
    public bool IsValid => string.IsNullOrWhiteSpace(Error);

    public static CoachParseResult Valid(CoachParsedResponse response) => new(response, null);

    public static CoachParseResult Invalid(string error) => new(null, error);
}

internal abstract record CoachParsedResponse(string CoachText, IReadOnlyList<Citation> Citations);

internal sealed record CoachParsedOnboarding(
    string Prompt,
    IReadOnlyList<string> AreaOptions,
    IReadOnlyList<string> ModeOptions) : CoachParsedResponse(Prompt, []);

internal sealed record CoachParsedQuizQuestion(
    string CoachText,
    IReadOnlyList<Citation> Citations,
    ChatMeta Meta,
    CoachQuizMeta Quiz) : CoachParsedResponse(CoachText, Citations);

internal sealed record CoachParsedQuizFeedback(
    string CoachText,
    IReadOnlyList<Citation> Citations,
    ChatMeta Meta,
    CoachQuizMeta Quiz) : CoachParsedResponse(CoachText, Citations);

internal sealed record CoachParsedTeachLike(
    string ResponseType,
    string CoachText,
    IReadOnlyList<Citation> Citations,
    ChatMeta Meta) : CoachParsedResponse(CoachText, Citations);

internal sealed record CoachParsedRefusal(
    string CoachText,
    IReadOnlyList<Citation> Citations) : CoachParsedResponse(CoachText, Citations);

internal sealed record CoachQuizMeta(
    string Question,
    IReadOnlyDictionary<string, string> Options,
    string? CorrectOption,
    string? Explanation,
    string? MemoryRule);

internal sealed record CoachClassificationResult(CoachParsedResponse? Response, bool IsValid, string? Error)
{
    public static CoachClassificationResult Valid(CoachParsedResponse? response) => new(response, true, null);

    public static CoachClassificationResult Invalid(string error) => new(null, false, error);
}

internal sealed record CoachCitationsResult(IReadOnlyList<Citation> Citations, bool IsValid, string? Error)
{
    public static CoachCitationsResult Invalid(string error) => new([], false, error);
}

internal enum CoachResponseType
{
    Teach,
    QuizQuestion,
    QuizFeedback,
    Review,
    Cram,
    Refusal,
    OnboardingOptions
}

internal enum CoachQuizOption
{
    A,
    B,
    C
}

internal sealed class CoachResponseTypeConverter : JsonConverter<CoachResponseType>
{
    public override CoachResponseType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("response_type must be a string.");
        }

        return reader.GetString()?.Trim().ToLowerInvariant() switch
        {
            "teach" => CoachResponseType.Teach,
            "quiz_question" => CoachResponseType.QuizQuestion,
            "quiz_feedback" => CoachResponseType.QuizFeedback,
            "review" => CoachResponseType.Review,
            "cram" => CoachResponseType.Cram,
            "refusal" => CoachResponseType.Refusal,
            "onboarding_options" => CoachResponseType.OnboardingOptions,
            _ => throw new JsonException("response_type is unsupported.")
        };
    }

    public override void Write(Utf8JsonWriter writer, CoachResponseType value, JsonSerializerOptions options)
    {
        var raw = value switch
        {
            CoachResponseType.Teach => "teach",
            CoachResponseType.QuizQuestion => "quiz_question",
            CoachResponseType.QuizFeedback => "quiz_feedback",
            CoachResponseType.Review => "review",
            CoachResponseType.Cram => "cram",
            CoachResponseType.Refusal => "refusal",
            CoachResponseType.OnboardingOptions => "onboarding_options",
            _ => throw new JsonException("response_type is unsupported.")
        };

        writer.WriteStringValue(raw);
    }
}

internal sealed class CoachQuizOptionConverter : JsonConverter<CoachQuizOption>
{
    public override CoachQuizOption Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("quiz.correct_option must be a string.");
        }

        return reader.GetString()?.Trim().ToUpperInvariant() switch
        {
            "A" => CoachQuizOption.A,
            "B" => CoachQuizOption.B,
            "C" => CoachQuizOption.C,
            _ => throw new JsonException("quiz.correct_option must be A, B, or C.")
        };
    }

    public override void Write(Utf8JsonWriter writer, CoachQuizOption value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class CoachResponsePayload
{
    [JsonPropertyName("response_type")]
    [JsonConverter(typeof(CoachResponseTypeConverter))]
    public CoachResponseType? ResponseType { get; init; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }

    [JsonPropertyName("coach_text")]
    public string? CoachText { get; init; }

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
    public CoachQuizOptions? Options { get; init; }

    [JsonPropertyName("correct_option")]
    [JsonConverter(typeof(CoachQuizOptionConverter))]
    public CoachQuizOption? CorrectOption { get; init; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; init; }

    [JsonPropertyName("memory_rule")]
    public string? MemoryRule { get; init; }
}

internal sealed class CoachQuizOptions
{
    [JsonPropertyName("A")]
    public string? A { get; init; }

    [JsonPropertyName("B")]
    public string? B { get; init; }

    [JsonPropertyName("C")]
    public string? C { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
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
