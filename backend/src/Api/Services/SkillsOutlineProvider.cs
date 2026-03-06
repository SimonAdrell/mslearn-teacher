using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace StudyCoach.BackendApi.Services;

public interface ISkillsOutlineProvider
{
    Task<SkillsOutlineResponse> GetOutlineAsync(CancellationToken cancellationToken);
}

public interface ILearnMcpClient
{
    Task<SkillsOutlineResponse> GetAi102SkillsOutlineAsync(CancellationToken cancellationToken);
}

public sealed class CachedSkillsOutlineProvider : ISkillsOutlineProvider
{
    private readonly ILearnMcpClient _learnMcpClient;
    private readonly LearnMcpOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CachedSkillsOutlineProvider> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private SkillsOutlineResponse? _cached;
    private DateTimeOffset _cacheExpiresAt;
    private SkillsOutlineResponse? _lastKnownGood;

    public CachedSkillsOutlineProvider(
        ILearnMcpClient learnMcpClient,
        IOptions<LearnMcpOptions> options,
        TimeProvider timeProvider,
        ILogger<CachedSkillsOutlineProvider> logger)
    {
        _learnMcpClient = learnMcpClient;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SkillsOutlineResponse> GetOutlineAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_cached is not null && _cacheExpiresAt > now)
        {
            return _cached with { IsFromCache = true };
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_cached is not null && _cacheExpiresAt > now)
            {
                return _cached with { IsFromCache = true };
            }

            try
            {
                var refreshed = await _learnMcpClient.GetAi102SkillsOutlineAsync(cancellationToken);
                _cached = refreshed with { IsFromCache = false };
                _lastKnownGood = _cached;
                _cacheExpiresAt = now.Add(GetCacheDuration());
                return _cached;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falling back to last-known skills outline after Learn MCP retrieval failure.");
                var fallback = _lastKnownGood ?? BuildStaticFallback();
                _cached = fallback with { IsFromCache = true };
                _cacheExpiresAt = now.Add(GetCacheDuration());
                return _cached;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private TimeSpan GetCacheDuration()
    {
        var configuredHours = _options.CacheHours <= 0 ? 24 : _options.CacheHours;
        return TimeSpan.FromHours(configuredHours);
    }

    private SkillsOutlineResponse BuildStaticFallback()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        return new SkillsOutlineResponse(
            [
                new SkillArea(
                    "Plan and manage an Azure AI solution",
                    "15-20%",
                    ["Select service resources", "Plan responsible AI and governance"]),
                new SkillArea(
                    "Implement generative AI solutions",
                    "20-25%",
                    ["Integrate Azure OpenAI", "Ground prompts with enterprise data"]),
                new SkillArea(
                    "Implement computer vision solutions",
                    "15-20%",
                    ["Analyze images", "Extract text with OCR"]),
                new SkillArea(
                    "Implement natural language processing solutions",
                    "30-35%",
                    ["Analyze text", "Build question answering solutions"]),
                new SkillArea(
                    "Implement knowledge mining and information extraction solutions",
                    "10-15%",
                    ["Build Azure AI Search pipelines", "Manage indexers and enrichment"])
            ],
            [
                new Citation(
                    "AI-102 study guide",
                    "https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-102",
                    today)
            ],
            true);
    }
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
