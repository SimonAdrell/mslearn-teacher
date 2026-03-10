using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Domain.Study;

namespace StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

internal static class FoundryPayloadValidator
{
    public static string? Validate(CoachPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.CoachText))
        {
            return "Foundry response coach_text is required.";
        }

        var responseType = payload.ResponseType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(responseType))
        {
            return "Foundry response response_type is required.";
        }

        if (responseType == "onboarding_options")
        {
            return ValidateOnboarding(payload.Onboarding);
        }

        if (responseType != "refusal")
        {
            if (payload.McpVerified is not true)
            {
                return "Foundry response mcp_verified must be true for substantive responses.";
            }

            if (string.IsNullOrWhiteSpace(payload.SkillOutlineArea))
            {
                return "Foundry response skill_outline_area is required.";
            }
        }

        if (payload.Quiz is not null)
        {
            var quizValidationError = ValidateQuiz(payload.Quiz);
            if (quizValidationError is not null)
            {
                return quizValidationError;
            }
        }

        return null;
    }

    private static string? ValidateOnboarding(CoachOnboardingPayload? onboarding)
    {
        if (onboarding is null)
        {
            return "Foundry response onboarding is required when response_type=onboarding_options.";
        }

        if (string.IsNullOrWhiteSpace(onboarding.Prompt))
        {
            return "Foundry response onboarding.prompt is required.";
        }

        if (NormalizeList(onboarding.AreaOptions).Count == 0)
        {
            return "Foundry response onboarding.area_options must include at least one option.";
        }

        var modes = NormalizeList(onboarding.ModeOptions);
        if (modes.Count == 0)
        {
            return "Foundry response onboarding.mode_options must include at least one option.";
        }

        if (modes.Any(mode => !StudyModes.IsSupported(mode)))
        {
            return "Foundry response onboarding.mode_options must use supported study modes.";
        }

        return null;
    }

    private static string? ValidateQuiz(CoachQuizPayload quiz)
    {
        if (string.IsNullOrWhiteSpace(quiz.Question))
        {
            return "Foundry response quiz.question is required when quiz is present.";
        }

        if (quiz.Options is null)
        {
            return "Foundry response quiz.options is required when quiz is present.";
        }

        foreach (var key in new[] { "A", "B", "C" })
        {
            if (!quiz.Options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return "Foundry response quiz.options must include non-empty A, B, and C options.";
            }
        }

        if (!string.IsNullOrWhiteSpace(quiz.CorrectOption))
        {
            var normalized = quiz.CorrectOption.Trim().ToUpperInvariant();
            if (normalized is not ("A" or "B" or "C"))
            {
                return "Foundry response quiz.correct_option must be A, B, or C when provided.";
            }
        }

        return null;
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
