using Microsoft.Extensions.Options;
using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Infrastructure.LearnMcp;

namespace StudyCoach.BackendApi.Application.Skills;

public interface ISkillsOutlineProvider
{
    Task<SkillsOutlineResponse> GetOutlineAsync(CancellationToken cancellationToken);
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
