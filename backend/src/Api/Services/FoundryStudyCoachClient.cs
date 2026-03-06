using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace StudyCoach.BackendApi.Services;

public interface IFoundryStudyCoachClient
{
    Task<FoundryChatResult> GetChatReplyAsync(Guid sessionId, string skillArea, string message, CancellationToken cancellationToken);
    Task<QuizQuestionResponse> GetNextQuizQuestionAsync(Guid sessionId, string skillArea, CancellationToken cancellationToken);
    Task<QuizAnswerResponse> GradeQuizAnswerAsync(Guid sessionId, string skillArea, Guid questionId, string answer, CancellationToken cancellationToken);
}

[RequiresPreviewFeatures]
public sealed class FoundryStudyCoachClient : IFoundryStudyCoachClient
{
    private const string LearnRefusalMessage =
        "I can't answer this from verified Microsoft Learn MCP sources right now. Please let me fetch relevant Learn MCP content first.";

    private const string ContractInstructions =
        "You are an AI-102 Study Coach. Use only Microsoft Learn MCP content and do not guess. " +
        "Return concise learner-facing content in plain text, then append a fenced ```coach_meta block with strict JSON. " +
        "The JSON must include response_type, purpose, skill_outline_area, must_know, exam_traps, citations[{title,url,retrieved_at}], mcp_verified, optional weak_areas_update, and optional quiz object. " +
        "Use AI-102 (not A1-102). For quiz questions include options A/B/C. For substantive responses, mcp_verified must be true and include at least one learn.microsoft.com citation with retrieved_at in YYYY-MM-DD.";

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

    public async Task<FoundryChatResult> GetChatReplyAsync(Guid sessionId, string skillArea, string message, CancellationToken cancellationToken)
    {
        if (_options.UseMockResponses)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var citations = new List<Citation>
            {
                new("AI-102 study guide", "https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-102", today)
            };

            var meta = new ChatMeta(
                skillArea,
                ["Match each scenario to the correct Azure AI service"],
                ["Confusing Azure AI Language with Azure OpenAI for classic intent/entity tasks"],
                true,
                ["Implement natural language processing solutions"]);

            return new FoundryChatResult(
                $"Purpose: Focus the current study step on {skillArea}.\n\nWhat to memorize: Azure AI Language handles intent/entity extraction.",
                citations,
                meta,
                false,
                null);
        }

        var parseResult = await CreateAndParseResponseAsync(
            sessionId,
            BuildChatPrompt(skillArea, message),
            cancellationToken);

        if (!parseResult.IsValid)
        {
            return new FoundryChatResult(LearnRefusalMessage, [], null, true, parseResult.Error);
        }

        return new FoundryChatResult(parseResult.CoachText, parseResult.Citations, parseResult.ChatMeta, false, null);
    }

    public async Task<QuizQuestionResponse> GetNextQuizQuestionAsync(Guid sessionId, string skillArea, CancellationToken cancellationToken)
    {
        if (_options.UseMockResponses)
        {
            return new QuizQuestionResponse(
                Guid.NewGuid(),
                "Which service best matches AI-102 intent and entity extraction scenarios?",
                [
                    "A) Azure AI Language",
                    "B) Azure AI Vision",
                    "C) Azure AI Search"
                ],
                [
                    new Citation(
                        "What is Azure AI Language?",
                        "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
                        DateOnly.FromDateTime(DateTime.UtcNow))
                ]);
        }

        var parseResult = await CreateAndParseResponseAsync(
            sessionId,
            BuildQuizPrompt(skillArea),
            cancellationToken);

        if (!parseResult.IsValid || parseResult.Quiz is null)
        {
            _logger.LogWarning("Quiz generation parse error for session {SessionId}: {Error}", sessionId, parseResult.Error);
            return new QuizQuestionResponse(Guid.NewGuid(), LearnRefusalMessage, null, []);
        }

        var choices = ToLabeledChoices(parseResult.Quiz.Options);
        return new QuizQuestionResponse(Guid.NewGuid(), parseResult.Quiz.Question, choices, parseResult.Citations);
    }

    public async Task<QuizAnswerResponse> GradeQuizAnswerAsync(Guid sessionId, string skillArea, Guid questionId, string answer, CancellationToken cancellationToken)
    {
        if (_options.UseMockResponses)
        {
            var correct = answer.StartsWith("A", StringComparison.OrdinalIgnoreCase) ||
                          answer.Contains("Azure AI Language", StringComparison.OrdinalIgnoreCase);

            var mockExplanation = correct
                ? "Correct. Azure AI Language is the expected service for intent/entity extraction scenarios."
                : "Incorrect. For intent/entity extraction in AI-102, Azure AI Language is the expected service.";

            return new QuizAnswerResponse(
                correct,
                mockExplanation,
                "Intent/entity extraction maps to Azure AI Language.",
                [
                    new Citation(
                        "What is Azure AI Language?",
                        "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
                        DateOnly.FromDateTime(DateTime.UtcNow))
                ]);
        }

        var parseResult = await CreateAndParseResponseAsync(
            sessionId,
            BuildQuizFeedbackPrompt(skillArea, answer),
            cancellationToken);

        if (!parseResult.IsValid || parseResult.Quiz is null)
        {
            _logger.LogWarning("Quiz grading parse error for session {SessionId}: {Error}", sessionId, parseResult.Error);
            return new QuizAnswerResponse(false, LearnRefusalMessage, "Always verify from Learn MCP.", []);
        }

        var isCorrect = IsCorrectAnswer(answer, parseResult.Quiz);
        var explanation = string.IsNullOrWhiteSpace(parseResult.Quiz.Explanation)
            ? (isCorrect ? "Correct." : "Incorrect.")
            : parseResult.Quiz.Explanation;
        var memoryRule = string.IsNullOrWhiteSpace(parseResult.Quiz.MemoryRule)
            ? "Always verify from Learn MCP."
            : parseResult.Quiz.MemoryRule;

        return new QuizAnswerResponse(isCorrect, explanation!, memoryRule!, parseResult.Citations);
    }

    private async Task<CoachParseResult> CreateAndParseResponseAsync(Guid sessionId, string message, CancellationToken cancellationToken)
    {
        if (_responsesClient is null ||
            string.IsNullOrWhiteSpace(_options.AgentName) ||
            string.IsNullOrWhiteSpace(_options.AgentVersion))
        {
            throw new InvalidOperationException("Foundry client is not configured. Set Foundry:AgentName and Foundry:AgentVersion and disable mock responses.");
        }

        _lastResponseIds.TryGetValue(sessionId, out var previousResponseId);

        var composedMessage = $"{ContractInstructions}\n\n{message}";
        var response = await _responsesClient.CreateResponseAsync(composedMessage, previousResponseId, cancellationToken);

        var output = response.Value.GetOutputText();
        if (string.IsNullOrWhiteSpace(output))
        {
            _logger.LogWarning("Foundry response for session {SessionId} did not include output text.", sessionId);
            return CoachParseResult.Invalid("Foundry response did not include output text.");
        }

        _lastResponseIds[sessionId] = response.Value.Id;

        return CoachResponseParser.Parse(output);
    }

    private static string BuildChatPrompt(string skillArea, string learnerMessage)
    {
        return
            "Task: Respond in AI-102 coaching mode with concise learner-facing content and a required coach_meta block.\n" +
            $"Skill area focus: {skillArea}.\n" +
            $"Learner message: {learnerMessage}";
    }

    private static string BuildQuizPrompt(string skillArea)
    {
        return
            "Task: Generate one short AI-102 scenario-based multiple-choice question.\n" +
            "Return response_type=quiz_question and include quiz.question and quiz.options with A, B, and C keys only.\n" +
            $"Skill area focus: {skillArea}.";
    }

    private static string BuildQuizFeedbackPrompt(string skillArea, string learnerAnswer)
    {
        return
            "Task: Evaluate the learner's answer to the latest quiz question in this conversation.\n" +
            "Return response_type=quiz_feedback and include quiz.correct_option, quiz.explanation, and quiz.memory_rule.\n" +
            $"Skill area focus: {skillArea}.\n" +
            $"Learner answer: {learnerAnswer}";
    }

    private static IReadOnlyList<string> ToLabeledChoices(IReadOnlyDictionary<string, string> options)
    {
        return
        [
            $"A) {options["A"]}",
            $"B) {options["B"]}",
            $"C) {options["C"]}"
        ];
    }

    private static bool IsCorrectAnswer(string learnerAnswer, CoachQuizMeta quiz)
    {
        if (string.IsNullOrWhiteSpace(quiz.CorrectOption))
        {
            return false;
        }

        var normalizedAnswer = learnerAnswer.Trim();
        if (normalizedAnswer.StartsWith(quiz.CorrectOption, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!quiz.Options.TryGetValue(quiz.CorrectOption, out var expectedText))
        {
            return false;
        }

        return normalizedAnswer.Contains(expectedText, StringComparison.OrdinalIgnoreCase);
    }
}

public record FoundryChatResult(
    string Answer,
    IReadOnlyList<Citation> Citations,
    ChatMeta? Meta,
    bool Refused,
    string? RefusalReason);

public sealed class FoundryOptions
{
    public string ProjectEndpoint { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string AgentVersion { get; set; } = "";
    public bool UseMockResponses { get; set; } = true;
}
