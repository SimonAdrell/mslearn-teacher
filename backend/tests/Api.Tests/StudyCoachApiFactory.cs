using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Infrastructure.Foundry;

namespace Api.Tests;

public sealed class StudyCoachApiFactory(object? replacementCoach = null) : WebApplicationFactory<Program>
{
    public HttpClient CreateAuthenticatedClient(string userId = "test-user")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });

            var coach = replacementCoach ?? new DefaultTestCoach();

            if (coach is IOnboardingCoach onboardingCoach)
            {
                services.RemoveAll<IOnboardingCoach>();
                services.AddSingleton(onboardingCoach);
            }

            if (coach is IChatCoach chatCoach)
            {
                services.RemoveAll<IChatCoach>();
                services.AddSingleton(chatCoach);
            }

            if (coach is IQuizCoach quizCoach)
            {
                services.RemoveAll<IQuizCoach>();
                services.AddSingleton(quizCoach);
            }
        });
    }

    private sealed class DefaultTestCoach : IOnboardingCoach, IChatCoach, IQuizCoach
    {
        private static readonly TokenUsageDto Usage = new(10, 15, 25);
        private static readonly IReadOnlyList<Citation> Citations =
        [
            new Citation(
                "AI-102 study guide",
                "https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-102",
                DateOnly.Parse("2026-03-06"))
        ];

        public Task<CoachOnboardingResult> GetOnboardingOptionsAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CoachOnboardingResult(
                "Pick a skill area.",
                ["Implement natural language processing solutions (30-35%)"],
                ["Learn", "Quiz"],
                Usage));
        }

        public Task<CoachChatResult> GetChatReplyAsync(Guid sessionId, string skillArea, string message, CancellationToken cancellationToken)
        {
            var meta = new ChatMeta(skillArea, ["Know X"], ["Trap Y"], true, null);
            return Task.FromResult(new CoachChatResult("Here is a grounded answer.", Citations, meta, false, null, Usage));
        }

        public Task<QuizQuestionResponse> GetNextQuizQuestionAsync(Guid sessionId, string skillArea, CancellationToken cancellationToken)
        {
            return Task.FromResult(new QuizQuestionResponse(
                Guid.NewGuid(),
                "Which service is best for intent classification?",
                ["A) Azure AI Language", "B) Azure AI Search", "C) Azure AI Vision"],
                Citations,
                Usage));
        }

        public Task<QuizAnswerResponse> GradeQuizAnswerAsync(Guid sessionId, string skillArea, Guid questionId, string answer, CancellationToken cancellationToken)
        {
            return Task.FromResult(new QuizAnswerResponse(true, "Correct.", "Map task to service first.", Citations, Usage));
        }
    }
}

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeaderName = "X-Test-User-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing authorization header."));
        }

        var userId = Request.Headers.TryGetValue(UserIdHeaderName, out var userHeaderValue) &&
                     !string.IsNullOrWhiteSpace(userHeaderValue)
            ? userHeaderValue.ToString()
            : "test-user";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Test User")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
