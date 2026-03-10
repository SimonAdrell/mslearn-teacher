using StudyCoach.BackendApi.Application.Contracts;

namespace StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

internal static class FoundryResponseMapper
{
    public static ParsedCoachResponse Map(CoachPayload payload, IReadOnlyList<Citation> citations)
    {
        var responseType = payload.ResponseType!.Trim().ToLowerInvariant();
        var coachText = payload.CoachText!.Trim();

        var onboarding = responseType == "onboarding_options"
            ? new ParsedOnboardingMeta(
                payload.Onboarding!.Prompt!.Trim(),
                NormalizeList(payload.Onboarding.AreaOptions),
                NormalizeList(payload.Onboarding.ModeOptions))
            : null;

        ChatMeta? chatMeta = null;
        if (responseType != "onboarding_options" && responseType != "refusal")
        {
            chatMeta = new ChatMeta(
                payload.SkillOutlineArea!.Trim(),
                NormalizeList(payload.MustKnow),
                NormalizeList(payload.ExamTraps),
                payload.McpVerified ?? false,
                NormalizeList(payload.WeakAreasUpdate) is { Count: > 0 } weakAreas ? weakAreas : null);
        }

        ParsedQuizMeta? quiz = null;
        if (payload.Quiz is not null)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["A"] = payload.Quiz.Options!["A"].Trim(),
                ["B"] = payload.Quiz.Options!["B"].Trim(),
                ["C"] = payload.Quiz.Options!["C"].Trim()
            };

            quiz = new ParsedQuizMeta(
                payload.Quiz.Question!.Trim(),
                options,
                payload.Quiz.CorrectOption?.Trim().ToUpperInvariant(),
                payload.Quiz.Explanation?.Trim(),
                payload.Quiz.MemoryRule?.Trim());
        }

        return new ParsedCoachResponse(
            coachText,
            citations,
            chatMeta,
            quiz,
            onboarding,
            responseType,
            null);
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? source)
    {
        if (source is null)
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

internal sealed record ParsedCoachResponse(
    string CoachText,
    IReadOnlyList<Citation> Citations,
    ChatMeta? ChatMeta,
    ParsedQuizMeta? Quiz,
    ParsedOnboardingMeta? Onboarding,
    string ResponseType,
    string? Error)
{
    public bool IsValid => string.IsNullOrWhiteSpace(Error);

    public static ParsedCoachResponse Invalid(string error) =>
        new(string.Empty, [], null, null, null, string.Empty, error);
}

internal sealed record ParsedQuizMeta(
    string Question,
    IReadOnlyDictionary<string, string> Options,
    string? CorrectOption,
    string? Explanation,
    string? MemoryRule);

internal sealed record ParsedOnboardingMeta(
    string Prompt,
    IReadOnlyList<string> AreaOptions,
    IReadOnlyList<string> ModeOptions);
