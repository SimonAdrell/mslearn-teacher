using StudyCoach.BackendApi.Application.Contracts;

namespace StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

internal static class CitationMapper
{
    public static CitationMapResult Map(IReadOnlyList<CoachCitationPayload>? citations)
    {
        var parsed = new List<Citation>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (citations is null)
        {
            return CitationMapResult.Success(parsed);
        }

        foreach (var citation in citations)
        {
            if (string.IsNullOrWhiteSpace(citation.Title) ||
                string.IsNullOrWhiteSpace(citation.Url) ||
                string.IsNullOrWhiteSpace(citation.RetrievedAt))
            {
                return CitationMapResult.Failure("Foundry response citations must include title, url, and retrieved_at.");
            }

            if (!Uri.TryCreate(citation.Url, UriKind.Absolute, out var parsedUrl) ||
                (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
            {
                return CitationMapResult.Failure("Foundry response citations include an invalid URL.");
            }

            if (!IsLearnHost(parsedUrl.Host))
            {
                return CitationMapResult.Failure("Foundry response citations must point to Microsoft Learn domains.");
            }

            if (!DateOnly.TryParseExact(citation.RetrievedAt, "yyyy-MM-dd", out var retrievedAt))
            {
                return CitationMapResult.Failure("Foundry response citations retrieved_at must use YYYY-MM-DD.");
            }

            if (seenUrls.Add(parsedUrl.ToString()))
            {
                parsed.Add(new Citation(citation.Title.Trim(), parsedUrl.ToString(), retrievedAt));
            }
        }

        return CitationMapResult.Success(parsed);
    }

    private static bool IsLearnHost(string host)
    {
        return host.Equals("learn.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".learn.microsoft.com", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record CitationMapResult(IReadOnlyList<Citation> Citations, string? Error)
{
    public bool IsValid => string.IsNullOrWhiteSpace(Error);

    public static CitationMapResult Success(IReadOnlyList<Citation> citations) => new(citations, null);
    public static CitationMapResult Failure(string error) => new([], error);
}
