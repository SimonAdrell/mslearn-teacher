using System.Text.Json;

namespace StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

internal static class FoundryPayloadDeserializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static DeserializeResult Deserialize(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return DeserializeResult.Failure("Foundry response did not include content.");
        }

        var trimmed = rawOutput.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return DeserializeResult.Failure("Foundry response must be a single JSON object.");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<CoachPayload>(trimmed, JsonOptions);
            return payload is null
                ? DeserializeResult.Failure("Foundry response JSON object is empty.")
                : DeserializeResult.Success(payload);
        }
        catch (JsonException)
        {
            return DeserializeResult.Failure("Foundry response must be a valid JSON object.");
        }
    }
}

internal sealed record DeserializeResult(CoachPayload? Payload, string? Error)
{
    public bool IsValid => string.IsNullOrWhiteSpace(Error) && Payload is not null;

    public static DeserializeResult Success(CoachPayload payload) => new(payload, null);
    public static DeserializeResult Failure(string error) => new(null, error);
}
