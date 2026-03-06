using System.Net;
using System.Net.Http.Json;
using StudyCoach.BackendApi.Services;
using FluentAssertions;

namespace Api.Tests;

public class StudyEndpointsTests
{
    [Fact]
    public async Task Chat_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new StudyCoachApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/study/chat", new ChatRequest(Guid.NewGuid(), "hello"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartSession_WithAuth_ReturnsSession()
    {
        await using var factory = new StudyCoachApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/study/session/start", new StartSessionRequest("Learn", "Implement natural language processing solutions"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<StartSessionResponse>();
        payload.Should().NotBeNull();
        payload!.Mode.Should().Be("Learn");
    }

    [Fact]
    public async Task Chat_WithoutCitations_ReturnsRefusalMessage()
    {
        await using var factory = new StudyCoachApiFactory(new NoCitationFoundryClient());
        using var client = factory.CreateAuthenticatedClient();

        var session = await (await client.PostAsJsonAsync("/api/study/session/start", new StartSessionRequest("Quiz", "Implement generative AI solutions")))
            .Content.ReadFromJsonAsync<StartSessionResponse>();

        var response = await client.PostAsJsonAsync("/api/study/chat", new ChatRequest(session!.SessionId, "teach me"));
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

        payload.Should().NotBeNull();
        payload!.Refused.Should().BeTrue();
        payload.Answer.Should().Contain("I can't answer this from verified Microsoft Learn MCP sources right now");
    }

    [Fact]
    public async Task Chat_WithDifferentUserSession_ReturnsNotFound()
    {
        await using var factory = new StudyCoachApiFactory();
        using var ownerClient = factory.CreateAuthenticatedClient("user-a");
        using var otherClient = factory.CreateAuthenticatedClient("user-b");

        var session = await (await ownerClient.PostAsJsonAsync(
                "/api/study/session/start",
                new StartSessionRequest("Learn", "Implement natural language processing solutions")))
            .Content.ReadFromJsonAsync<StartSessionResponse>();

        var response = await otherClient.PostAsJsonAsync("/api/study/chat", new ChatRequest(session!.SessionId, "teach me"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class NoCitationFoundryClient : IFoundryStudyCoachClient
    {
        public Task<FoundryChatResult> GetChatReplyAsync(Guid sessionId, string message, CancellationToken cancellationToken) =>
            Task.FromResult(new FoundryChatResult("answer-without-source", []));

        public Task<QuizQuestionResponse> GetNextQuizQuestionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new QuizQuestionResponse(Guid.NewGuid(), "q", ["a"]));

        public Task<QuizAnswerResponse> GradeQuizAnswerAsync(Guid questionId, string answer, CancellationToken cancellationToken) =>
            Task.FromResult(new QuizAnswerResponse(false, "e", "m", []));
    }
}
