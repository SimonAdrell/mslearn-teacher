using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using StudyCoach.BackendApi.Application.Contracts;

namespace StudyCoach.BackendApi.Infrastructure.LearnMcp;

public interface ILearnMcpClient
{
    Task<SkillsOutlineResponse> GetAi102SkillsOutlineAsync(CancellationToken cancellationToken);
}

public sealed class LearnMcpHttpClient : ILearnMcpClient
{
    private readonly HttpClient _httpClient;
    private readonly LearnMcpOptions _options;

    public LearnMcpHttpClient(HttpClient httpClient, IOptions<LearnMcpOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<SkillsOutlineResponse> GetAi102SkillsOutlineAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("LearnMcp:BaseUrl must be configured for MCP-backed skills outline retrieval.");
        }

        var endpoint = string.IsNullOrWhiteSpace(_options.SkillsOutlinePath)
            ? "/ai-102/skills-outline"
            : _options.SkillsOutlinePath;

        var payload = await _httpClient.GetFromJsonAsync<LearnMcpSkillsOutlinePayload>(endpoint, cancellationToken)
                      ?? throw new InvalidOperationException("Learn MCP returned an empty skills-outline payload.");

        if (payload.Areas is null || payload.Areas.Count == 0)
        {
            throw new InvalidOperationException("Learn MCP returned no skills-outline areas.");
        }

        var areas = payload.Areas
            .Where(area => !string.IsNullOrWhiteSpace(area.Name) && !string.IsNullOrWhiteSpace(area.WeightPercent))
            .Select(area => new SkillArea(area.Name!.Trim(), area.WeightPercent!.Trim(), area.Includes ?? []))
            .ToArray();

        var citations = new List<Citation>();
        foreach (var citation in payload.Citations ?? [])
        {
            if (string.IsNullOrWhiteSpace(citation.Title) ||
                string.IsNullOrWhiteSpace(citation.Url) ||
                string.IsNullOrWhiteSpace(citation.RetrievedAt))
            {
                continue;
            }

            if (!Uri.TryCreate(citation.Url, UriKind.Absolute, out _))
            {
                continue;
            }

            if (!DateOnly.TryParseExact(citation.RetrievedAt, "yyyy-MM-dd", out var parsedDate))
            {
                continue;
            }

            citations.Add(new Citation(citation.Title.Trim(), citation.Url.Trim(), parsedDate));
        }

        return new SkillsOutlineResponse(areas, citations, false);
    }
}

public sealed class LearnMcpOptions
{
    public string BaseUrl { get; set; } = "";
    public string SkillsOutlinePath { get; set; } = "/ai-102/skills-outline";
    public int CacheHours { get; set; } = 24;
}

public sealed class LearnMcpSkillsOutlinePayload
{
    public IReadOnlyList<LearnMcpSkillAreaPayload>? Areas { get; set; }
    public IReadOnlyList<LearnMcpCitationPayload>? Citations { get; set; }
}

public sealed class LearnMcpSkillAreaPayload
{
    public string? Name { get; set; }
    public string? WeightPercent { get; set; }
    public IReadOnlyList<string>? Includes { get; set; }
}

public sealed class LearnMcpCitationPayload
{
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? RetrievedAt { get; set; }
}
