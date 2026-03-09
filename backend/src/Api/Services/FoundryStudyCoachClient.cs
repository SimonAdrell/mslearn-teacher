using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Options;


namespace StudyCoach.BackendApi.Services;

public interface IFoundryStudyCoachClient
{
    Task<FoundryOnboardingResult> GetOnboardingOptionsAsync(Guid sessionId, CancellationToken cancellationToken);
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
        "Return structured output only and no markdown outside required formats. " +
        "For teach/review/cram/quiz_question/quiz_feedback return exactly one JSON object with coach_text, response_type, purpose, skill_outline_area, must_know, exam_traps, citations[{title,url,retrieved_at}], mcp_verified, optional weak_areas_update, and optional quiz object. " +
        "For onboarding_options, return response_type=onboarding_options with onboarding.prompt, onboarding.area_options[], and onboarding.mode_options[] using valid modes. " +
        "Use AI-102 (not A1-102). For substantive responses, mcp_verified must be true and include at least one learn.microsoft.com citation with retrieved_at in YYYY-MM-DD.";

    private static readonly IReadOnlyList<string> DefaultAreaOptions =
    [
        "Implement natural language processing solutions (30-35%)",
        "Implement computer vision solutions (15-20%)",
        "Implement generative AI solutions (20-25%)"
    ];

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

    public async Task<FoundryOnboardingResult> GetOnboardingOptionsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (_options.UseMockResponses)
        {
            return new FoundryOnboardingResult(
                "Let's start your AI-102 session. Pick a skill area.",
                DefaultAreaOptions,
                [.. StudyModes.All],
                new TokenUsageDto(50, 25, 75));
        }

        var result = await CreateAndParseResponseAsync(
            sessionId,
            BuildOnboardingPrompt(),
            cancellationToken);

        if (!result.ParseResult.IsValid || result.ParseResult.Onboarding is null)
        {
            _logger.LogWarning("Onboarding parse error for session {SessionId}: {Error}", sessionId, result.ParseResult.Error);
            return new FoundryOnboardingResult(
                "Let's start your AI-102 session. Pick a skill area.",
                DefaultAreaOptions,
                [.. StudyModes.All],
                result.Usage);
        }

        return new FoundryOnboardingResult(
            result.ParseResult.Onboarding.Prompt,
            result.ParseResult.Onboarding.AreaOptions,
            result.ParseResult.Onboarding.ModeOptions,
            result.Usage);
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
                null,
                new TokenUsageDto(80, 45, 125));
        }

        var result = await CreateAndParseResponseAsync(
            sessionId,
            BuildChatPrompt(skillArea, message),
            cancellationToken);

        if (!result.ParseResult.IsValid)
        {
            return new FoundryChatResult(LearnRefusalMessage, [], null, true, result.ParseResult.Error, result.Usage);
        }

        return new FoundryChatResult(
            result.ParseResult.CoachText,
            result.ParseResult.Citations,
            result.ParseResult.ChatMeta,
            false,
            null,
            result.Usage);
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
                ],
                new TokenUsageDto(70, 35, 105));
        }

        var result = await CreateAndParseResponseAsync(
            sessionId,
            BuildQuizPrompt(skillArea),
            cancellationToken);

        if (!result.ParseResult.IsValid || result.ParseResult.Quiz is null)
        {
            _logger.LogWarning("Quiz generation parse error for session {SessionId}: {Error}", sessionId, result.ParseResult.Error);
            return new QuizQuestionResponse(Guid.NewGuid(), LearnRefusalMessage, null, [], result.Usage);
        }

        var choices = ToLabeledChoices(result.ParseResult.Quiz.Options);
        return new QuizQuestionResponse(
            Guid.NewGuid(),
            result.ParseResult.Quiz.Question,
            choices,
            result.ParseResult.Citations,
            result.Usage);
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
                ],
                new TokenUsageDto(60, 30, 90));
        }

        var result = await CreateAndParseResponseAsync(
            sessionId,
            BuildQuizFeedbackPrompt(skillArea, answer),
            cancellationToken);

        if (!result.ParseResult.IsValid || result.ParseResult.Quiz is null)
        {
            _logger.LogWarning("Quiz grading parse error for session {SessionId}: {Error}", sessionId, result.ParseResult.Error);
            return new QuizAnswerResponse(false, LearnRefusalMessage, "Always verify from Learn MCP.", [], result.Usage);
        }

        var isCorrect = IsCorrectAnswer(answer, result.ParseResult.Quiz);
        var explanation = string.IsNullOrWhiteSpace(result.ParseResult.Quiz.Explanation)
            ? (isCorrect ? "Correct." : "Incorrect.")
            : result.ParseResult.Quiz.Explanation;
        var memoryRule = string.IsNullOrWhiteSpace(result.ParseResult.Quiz.MemoryRule)
            ? "Always verify from Learn MCP."
            : result.ParseResult.Quiz.MemoryRule;

        return new QuizAnswerResponse(
            isCorrect,
            explanation!,
            memoryRule!,
            result.ParseResult.Citations,
            result.Usage);
    }

    private async Task<FoundryParseWithUsageResult> CreateAndParseResponseAsync(Guid sessionId, string message, CancellationToken cancellationToken)
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
        var usage = ToTokenUsageDto(response.Value.Usage);

        var output = response.Value.GetOutputText();
        if (string.IsNullOrWhiteSpace(output))
        {
            _logger.LogWarning("Foundry response for session {SessionId} did not include output text.", sessionId);
            return new FoundryParseWithUsageResult(CoachParseResult.Invalid("Foundry response did not include output text."), usage);
        }

        _lastResponseIds[sessionId] = response.Value.Id;

        return new FoundryParseWithUsageResult(CoachResponseParser.Parse(output), usage);
    }

    private static TokenUsageDto? ToTokenUsageDto(object? usage)
    {
        if (usage is null)
        {
            return null;
        }

        var usageType = usage.GetType();
        var prompt = usageType.GetProperty("InputTokens")?.GetValue(usage) as int?;
        var completion = usageType.GetProperty("OutputTokens")?.GetValue(usage) as int?;
        var total = usageType.GetProperty("TotalTokens")?.GetValue(usage) as int?;

        if (prompt is null || completion is null || total is null)
        {
            return null;
        }

        return new TokenUsageDto(prompt.Value, completion.Value, total.Value);
    }

    private static string BuildOnboardingPrompt()
    {
        return
            "Task: Provide onboarding setup options for an AI-102 study session.\n" +
            "Return response_type=onboarding_options with onboarding.prompt, onboarding.area_options, and onboarding.mode_options.\n" +
            "Include only supported study modes.";
    }

    private static string BuildChatPrompt(string skillArea, string learnerMessage)
    {
        return
            "Task: Respond in AI-102 coaching mode. Return exactly one JSON object and put concise learner-facing prose in coach_text.\n" +
            $"Skill area focus: {skillArea}.\n" +
            $"Learner message: {learnerMessage}";
    }

    private static string BuildQuizPrompt(string skillArea)
    {
        return
            "Task: Generate one short AI-102 scenario-based multiple-choice question.\n" +
            "Return exactly one JSON object only. Set response_type=quiz_question, put concise learner-facing prose in coach_text, and include quiz.question and quiz.options with A, B, and C keys only.\n" +
            $"Skill area focus: {skillArea}.";
    }

    private static string BuildQuizFeedbackPrompt(string skillArea, string learnerAnswer)
    {
        return
            "Task: Evaluate the learner's answer to the latest quiz question in this conversation.\n" +
            "Return exactly one JSON object only. Set response_type=quiz_feedback, put concise learner-facing prose in coach_text, and include quiz.correct_option, quiz.explanation, and quiz.memory_rule.\n" +
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

public record FoundryOnboardingResult(
    string Prompt,
    IReadOnlyList<string> AreaOptions,
    IReadOnlyList<string> ModeOptions,
    TokenUsageDto? Usage = null);

public record FoundryChatResult(
    string Answer,
    IReadOnlyList<Citation> Citations,
    ChatMeta? Meta,
    bool Refused,
    string? RefusalReason,
    TokenUsageDto? Usage = null);

internal sealed record FoundryParseWithUsageResult(CoachParseResult ParseResult, TokenUsageDto? Usage);

public sealed class FoundryOptions
{
    public string ProjectEndpoint { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string AgentVersion { get; set; } = "";
    public bool UseMockResponses { get; set; } = true;
}



