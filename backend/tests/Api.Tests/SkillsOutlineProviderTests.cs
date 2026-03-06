using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudyCoach.BackendApi.Services;

namespace Api.Tests;

public class SkillsOutlineProviderTests
{
    [Fact]
    public async Task GetOutlineAsync_UsesCacheWithinTtl()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-03-06T10:00:00Z"));
        var client = new FakeLearnMcpClient(CreateOutline("20-25%"));
        var provider = new CachedSkillsOutlineProvider(
            client,
            Options.Create(new LearnMcpOptions { CacheHours = 24 }),
            timeProvider,
            NullLogger<CachedSkillsOutlineProvider>.Instance);

        var first = await provider.GetOutlineAsync(CancellationToken.None);
        var second = await provider.GetOutlineAsync(CancellationToken.None);

        first.IsFromCache.Should().BeFalse();
        second.IsFromCache.Should().BeTrue();
        client.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOutlineAsync_WhenFetchFails_ReturnsFallback()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-03-06T10:00:00Z"));
        var client = new FakeLearnMcpClient(new InvalidOperationException("down"));
        var provider = new CachedSkillsOutlineProvider(
            client,
            Options.Create(new LearnMcpOptions { CacheHours = 24 }),
            timeProvider,
            NullLogger<CachedSkillsOutlineProvider>.Instance);

        var result = await provider.GetOutlineAsync(CancellationToken.None);

        result.IsFromCache.Should().BeTrue();
        result.Areas.Should().NotBeEmpty();
        result.Citations.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOutlineAsync_AfterSuccess_FallsBackToLastKnownGoodOnFailure()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-03-06T10:00:00Z"));
        var client = new FakeLearnMcpClient(CreateOutline("20-25%"));
        var provider = new CachedSkillsOutlineProvider(
            client,
            Options.Create(new LearnMcpOptions { CacheHours = 1 }),
            timeProvider,
            NullLogger<CachedSkillsOutlineProvider>.Instance);

        var first = await provider.GetOutlineAsync(CancellationToken.None);

        client.SetFailure(new InvalidOperationException("down"));
        timeProvider.Advance(TimeSpan.FromHours(2));

        var second = await provider.GetOutlineAsync(CancellationToken.None);

        first.Areas[0].WeightPercent.Should().Be("20-25%");
        second.Areas[0].WeightPercent.Should().Be("20-25%");
        second.IsFromCache.Should().BeTrue();
    }

    private static SkillsOutlineResponse CreateOutline(string weight) =>
        new(
            [
                new SkillArea("Implement generative AI solutions", weight, ["Integrate Azure OpenAI"])
            ],
            [
                new Citation(
                    "AI-102 study guide",
                    "https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-102",
                    DateOnly.Parse("2026-03-06"))
            ],
            false);

    private sealed class FakeLearnMcpClient : ILearnMcpClient
    {
        private readonly Queue<object> _results = new();

        public int CallCount { get; private set; }

        public FakeLearnMcpClient(params object[] results)
        {
            foreach (var result in results)
            {
                _results.Enqueue(result);
            }
        }

        public Task<SkillsOutlineResponse> GetAi102SkillsOutlineAsync(CancellationToken cancellationToken)
        {
            CallCount++;

            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No queued responses.");
            }

            var next = _results.Dequeue();
            if (next is Exception ex)
            {
                throw ex;
            }

            return Task.FromResult((SkillsOutlineResponse)next);
        }

        public void SetFailure(Exception exception)
        {
            _results.Clear();
            _results.Enqueue(exception);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by)
        {
            _utcNow = _utcNow.Add(by);
        }
    }
}
