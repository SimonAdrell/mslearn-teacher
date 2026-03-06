using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace StudyCoach.BackendApi.Services;

public interface IFoundryStudyCoachClient
{
    Task<FoundryChatResult> GetChatReplyAsync(Guid sessionId, string message, CancellationToken cancellationToken);
    Task<QuizQuestionResponse> GetNextQuizQuestionAsync(CancellationToken cancellationToken);
    Task<QuizAnswerResponse> GradeQuizAnswerAsync(Guid questionId, string answer, CancellationToken cancellationToken);
}

[RequiresPreviewFeatures]
public sealed class FoundryStudyCoachClient : IFoundryStudyCoachClient
{
    private static readonly Regex MarkdownLinkRegex = new(@"\[([^\]]+)\]\((https?://[^)\s]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new("https?://[^\\s\\]\\)\\\">]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly FoundryOptions _options;
    private readonly ILogger<FoundryStudyCoachClient> _logger;
    private readonly ProjectResponsesClient? _responsesClient;
    private readonly ConcurrentDictionary<Guid, string> _lastResponseIds = new();

    public FoundryStudyCoachClient(
        IOptions<FoundryOptions> options,
        ILogger<FoundryStudyCoachClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.UseMockResponses)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
        {
            throw new InvalidOperationException("Foundry:ProjectEndpoint must be configured when UseMockResponses=false.");
        }

        var projectClient = new AIProjectClient(new Uri(_options.ProjectEndpoint), new DefaultAzureCredential());
        var agentReference = ProjectsOpenAIModelFactory.AgentReference(_options.AgentName, _options.AgentVersion);
        _responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentReference);
    }

    public async Task<FoundryChatResult> GetChatReplyAsync(Guid sessionId, string message, CancellationToken cancellationToken)
    {
        if (_options.UseMockResponses)
        {
            return new FoundryChatResult(
                $"Study coach response for: {message}",
                [new Citation("AI-102 Study Guide", "https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-102", DateOnly.FromDateTime(DateTime.UtcNow))]);
        }

        if (_responsesClient is null ||
            string.IsNullOrWhiteSpace(_options.AgentName) ||
            string.IsNullOrWhiteSpace(_options.AgentVersion))
        {
            throw new InvalidOperationException("Foundry client is not configured. Set Foundry:AgentName and Foundry:AgentVersion and disable mock responses.");
        }

        _lastResponseIds.TryGetValue(sessionId, out var previousResponseId);

        var response = await _responsesClient.CreateResponseAsync(message, previousResponseId, cancellationToken);

        var answer = response.Value.GetOutputText();
        if (string.IsNullOrWhiteSpace(answer))
        {
            _logger.LogWarning("Foundry response for session {SessionId} did not include output text.", sessionId);
            return new FoundryChatResult("", []);
        }

        _lastResponseIds[sessionId] = response.Value.Id;

        var citations = ExtractCitations(answer);
        return new FoundryChatResult(answer, citations);
    }

    public Task<QuizQuestionResponse> GetNextQuizQuestionAsync(CancellationToken cancellationToken)
    {
        var question = new QuizQuestionResponse(
            Guid.NewGuid(),
            "Which Azure AI service should you choose for intent and entity extraction in AI-102 scenarios?",
            ["Azure AI Language", "Azure AI Vision", "Azure AI Search", "Azure OpenAI"]);

        return Task.FromResult(question);
    }

    public Task<QuizAnswerResponse> GradeQuizAnswerAsync(Guid questionId, string answer, CancellationToken cancellationToken)
    {
        var correct = string.Equals(answer.Trim(), "Azure AI Language", StringComparison.OrdinalIgnoreCase);
        var explanation = correct
            ? "Correct. Azure AI Language handles conversational language understanding tasks."
            : "Incorrect. For AI-102 NLP intent/entity extraction, Azure AI Language is the expected choice.";

        var response = new QuizAnswerResponse(
            correct,
            explanation,
            "LU/NLP in AI-102 maps first to Azure AI Language.",
            [new Citation("What is Azure AI Language?", "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview", DateOnly.FromDateTime(DateTime.UtcNow))]);

        return Task.FromResult(response);
    }

    private static IReadOnlyList<Citation> ExtractCitations(string answer)
    {
        var citations = new List<Citation>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (Match match in MarkdownLinkRegex.Matches(answer))
        {
            var title = match.Groups[1].Value.Trim();
            var url = match.Groups[2].Value.Trim();

            if (TryAddCitation(citations, seenUrls, url, string.IsNullOrWhiteSpace(title) ? "Reference" : title, today))
            {
                continue;
            }
        }

        foreach (Match match in UrlRegex.Matches(answer))
        {
            var url = match.Value.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var fallbackTitle = uri.Host;
            TryAddCitation(citations, seenUrls, uri.ToString(), fallbackTitle, today);
        }

        return citations;
    }

    private static bool TryAddCitation(List<Citation> citations, HashSet<string> seenUrls, string url, string title, DateOnly retrievedAt)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        var normalizedUrl = parsed.ToString();
        if (!seenUrls.Add(normalizedUrl))
        {
            return false;
        }

        citations.Add(new Citation(title, normalizedUrl, retrievedAt));
        return true;
    }
}

public record FoundryChatResult(string Answer, IReadOnlyList<Citation> Citations);

public sealed class FoundryOptions
{
    public string ProjectEndpoint { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string AgentVersion { get; set; } = "";
    public bool UseMockResponses { get; set; } = true;
}
