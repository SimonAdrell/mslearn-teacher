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
        "Return exactly one JSON object and nothing else. Do not return markdown, code fences, or prose outside JSON. " +
        "The JSON must include response_type, purpose, coach_text, and mcp_verified. " +
        "For teach, review, cram, quiz_question, and quiz_feedback responses also include skill_outline_area, non-empty must_know[], non-empty exam_traps[], and citations[{title,url,retrieved_at}] with at least one learn.microsoft.com citation using YYYY-MM-DD. " +
        "For onboarding responses, use response_type=onboarding_options and include onboarding.prompt, onboarding.area_options[], onboarding.mode_options[]. " +
        "For quiz_question include quiz.question and quiz.options with exactly A/B/C keys. " +
        "For quiz_feedback include quiz.correct_option (A/B/C), quiz.explanation, and quiz.memory_rule. " +
        "For refusal responses set mcp_verified=false. " +
        "Use AI-102 (not A1-102).";

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
                "Let's start your AI-102 session. Pick a Skill Outline Area.",
                [
                    "Implement natural language processing solutions (30-35%)",
                    "Implement computer vision solutions (15-20%)",
                    "Implement generative AI solutions (20-25%)"
                ],
                [.. StudyModes.All]);
        }

        var parseResult = await CreateAndParseResponseAsync(
            sessionId,
            BuildOnboardingPrompt(),
            cancellationToken);

        if (!parseResult.IsValid)
        {
            _logger.LogWarning("Onboarding generation parse error for session {SessionId}: {Error}", sessionId, parseResult.Error);
            return new FoundryOnboardingResult(
                "Let's start your AI-102 session. Pick a Skill Outline Area.",
                ["Implement natural language processing solutions (30-35%)"],
                [.. StudyModes.All]);
        }

        return parseResult.Response switch
        {
            CoachParsedOnboarding onboarding => new FoundryOnboardingResult(
                onboarding.Prompt,
                onboarding.AreaOptions,
                onboarding.ModeOptions),
            _ => new FoundryOnboardingResult(
                "Let's start your AI-102 session. Pick a Skill Outline Area.",
                ["Implement natural language processing solutions (30-35%)"],
                [.. StudyModes.All])
        };
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

        return parseResult.Response switch
        {
            CoachParsedTeachLike teachLike => new FoundryChatResult(teachLike.CoachText, teachLike.Citations, teachLike.Meta, false, null),
            CoachParsedRefusal refusal => new FoundryChatResult(refusal.CoachText, refusal.Citations, null, true, null),
            CoachParsedQuizFeedback feedback => new FoundryChatResult(feedback.CoachText, feedback.Citations, feedback.Meta, false, null),
            CoachParsedQuizQuestion question => new FoundryChatResult(question.CoachText, question.Citations, question.Meta, false, null),
            _ => new FoundryChatResult(LearnRefusalMessage, [], null, true, "Unexpected parser response type for chat.")
        };
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

        if (!parseResult.IsValid)
        {
            _logger.LogWarning("Quiz generation parse error for session {SessionId}: {Error}", sessionId, parseResult.Error);
            return new QuizQuestionResponse(Guid.NewGuid(), LearnRefusalMessage, null, []);
        }

        if (parseResult.Response is not CoachParsedQuizQuestion question)
        {
            _logger.LogWarning("Quiz generation parser returned unexpected response type for session {SessionId}", sessionId);
            return new QuizQuestionResponse(Guid.NewGuid(), LearnRefusalMessage, null, []);
        }

        var choices = ToLabeledChoices(question.Quiz.Options);
        return new QuizQuestionResponse(Guid.NewGuid(), question.Quiz.Question, choices, question.Citations);
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

        if (!parseResult.IsValid)
        {
            _logger.LogWarning("Quiz grading parse error for session {SessionId}: {Error}", sessionId, parseResult.Error);
            return new QuizAnswerResponse(false, LearnRefusalMessage, "Always verify from Learn MCP.", []);
        }

        if (parseResult.Response is not CoachParsedQuizFeedback feedback)
        {
            _logger.LogWarning("Quiz grading parser returned unexpected response type for session {SessionId}", sessionId);
            return new QuizAnswerResponse(false, LearnRefusalMessage, "Always verify from Learn MCP.", []);
        }

        var isCorrect = IsCorrectAnswer(answer, feedback.Quiz);
        var explanation = string.IsNullOrWhiteSpace(feedback.Quiz.Explanation)
            ? (isCorrect ? "Correct." : "Incorrect.")
            : feedback.Quiz.Explanation;
        var memoryRule = string.IsNullOrWhiteSpace(feedback.Quiz.MemoryRule)
            ? "Always verify from Learn MCP."
            : feedback.Quiz.MemoryRule;

        return new QuizAnswerResponse(isCorrect, explanation!, memoryRule!, feedback.Citations);
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

    private static string BuildOnboardingPrompt()
    {
        return
            "Task: Provide onboarding setup options for an AI-102 study chat session.\n" +
            "Return strict JSON only with response_type=onboarding_options, purpose, coach_text, mcp_verified, onboarding.prompt, onboarding.area_options, onboarding.mode_options.\n" +
            "Do not ask follow-up free-text questions. Return compact option arrays only.";
    }

    private static string BuildChatPrompt(string skillArea, string learnerMessage)
    {
        return
            "Task: Respond in AI-102 coaching mode with strict JSON only.\n" +
            $"Skill area focus: {skillArea}.\n" +
            $"Learner message: {learnerMessage}";
    }

    private static string BuildQuizPrompt(string skillArea)
    {
        return
            "Task: Generate one short AI-102 scenario-based multiple-choice question.\n" +
            "Return strict JSON only with response_type=quiz_question and include purpose, coach_text, mcp_verified, skill_outline_area, must_know, exam_traps, citations, quiz.question, and quiz.options with exactly A, B, and C keys.\n" +
            $"Skill area focus: {skillArea}.";
    }

    private static string BuildQuizFeedbackPrompt(string skillArea, string learnerAnswer)
    {
        return
            "Task: Evaluate the learner's answer to the latest quiz question in this conversation.\n" +
            "Return strict JSON only with response_type=quiz_feedback and include purpose, coach_text, mcp_verified, skill_outline_area, must_know, exam_traps, citations, quiz.correct_option, quiz.explanation, and quiz.memory_rule.\n" +
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

public record FoundryOnboardingResult(string Prompt, IReadOnlyList<string> AreaOptions, IReadOnlyList<string> ModeOptions);

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
