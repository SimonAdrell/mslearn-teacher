namespace StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

internal static class FoundryResponseParser
{
    public static ParsedCoachResponse Parse(string rawOutput)
    {
        var deserialized = FoundryPayloadDeserializer.Deserialize(rawOutput);
        if (!deserialized.IsValid)
        {
            return ParsedCoachResponse.Invalid(deserialized.Error!);
        }

        var payload = deserialized.Payload!;

        var validationError = FoundryPayloadValidator.Validate(payload);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return ParsedCoachResponse.Invalid(validationError);
        }

        var citationsResult = CitationMapper.Map(payload.Citations);
        if (!citationsResult.IsValid)
        {
            return ParsedCoachResponse.Invalid(citationsResult.Error!);
        }

        var responseType = payload.ResponseType?.Trim().ToLowerInvariant();
        var requiresLearnCitations = responseType is not ("refusal" or "onboarding_options");
        if (requiresLearnCitations && citationsResult.Citations.Count == 0)
        {
            return ParsedCoachResponse.Invalid("Foundry response citations must include at least one Learn citation.");
        }

        return FoundryResponseMapper.Map(payload, citationsResult.Citations);
    }
}
