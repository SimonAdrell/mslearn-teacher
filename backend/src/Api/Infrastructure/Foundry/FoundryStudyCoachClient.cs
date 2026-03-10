using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Options;
using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Domain.Study;
using StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

namespace StudyCoach.BackendApi.Infrastructure.Foundry;

[RequiresPreviewFeatures]
public sealed class FoundryStudyCoachClient : IOnboardingCoach, IChatCoach, IQuizCoach
{
    private const string LearnRefusalMessage =
        "I can't answer this from verified Microsoft Learn MCP sources right now. Please let me fetch relevant Learn MCP content first.";

    private const string ContractInstructions =
        "You are an AI-102 Study Coach. Use only Microsoft Learn MCP content and do not guess. " +
        "Return exactly one JSON object and no markdown. " +
        "For teach/review/cram/quiz_question/quiz_feedback return coach_text, response_type, skill_outline_area, must_know, exam_traps, citations[{title,url,retrieved_at}], mcp_verified, optional weak_areas_update, and optional quiz object. " +
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
    private readonly ProjectResponsesClient _responsesClient;
    private readonly ConcurrentDictionary<Guid, string> _lastResponseIds = new();

    public FoundryStudyCoachClient(
        IOptions<FoundryOptions> options,
        ILogger<FoundryStudyCoachClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
        {
            throw new InvalidOperationException("Foundry:ProjectEndpoint must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.AgentName))
        {
            throw new InvalidOperationException("Foundry:AgentName must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.AgentVersion))
        {
            throw new InvalidOperationException("Foundry:AgentVersion must be configured.");
        }

        var projectClient = new AIProjectClient(new Uri(_options.ProjectEndpoint), new DefaultAzureCredential());
        var agentReference = ProjectsOpenAIModelFactory.AgentReference(_options.AgentName, _options.AgentVersion);
        _responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentReference);
    }

    public async Task<CoachOnboardingResult> GetOnboardingOptionsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await CreateAndParseResponseAsync(sessionId, BuildOnboardingPrompt(), cancellationToken);

        if (!result.ParseResult.IsValid || result.ParseResult.Onboarding is null)
        {
            _logger.LogWarning("Onboarding parse error for session {SessionId}: {Error}", sessionId, result.ParseResult.Error);
            return new CoachOnboardingResult(
                "Let's start your AI-102 session. Pick a skill area.",
                DefaultAreaOptions,
                [.. StudyModes.All],
                result.Usage);
        }

        return new CoachOnboardingResult(
            result.ParseResult.Onboarding.Prompt,
            result.ParseResult.Onboarding.AreaOptions,
            result.ParseResult.Onboarding.ModeOptions,
            result.Usage);
    }

    public async Task<CoachChatResult> GetChatReplyAsync(Guid sessionId, string skillArea, string message, CancellationToken cancellationToken)
    {
        var result = await CreateAndParseResponseAsync(sessionId, BuildChatPrompt(skillArea, message), cancellationToken);

        if (!result.ParseResult.IsValid)
        {
            return new CoachChatResult(LearnRefusalMessage, [], null, true, result.ParseResult.Error, result.Usage);
        }

        return new CoachChatResult(
            result.ParseResult.CoachText,
            result.ParseResult.Citations,
            result.ParseResult.ChatMeta,
            false,
            null,
            result.Usage);
    }

    public async Task<QuizQuestionResponse> GetNextQuizQuestionAsync(Guid sessionId, string skillArea, CancellationToken cancellationToken)
    {
        var result = await CreateAndParseResponseAsync(sessionId, BuildQuizPrompt(skillArea), cancellationToken);

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
        var result = await CreateAndParseResponseAsync(sessionId, BuildQuizFeedbackPrompt(skillArea, answer), cancellationToken);

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
        _lastResponseIds.TryGetValue(sessionId, out var previousResponseId);

        var composedMessage = $"{ContractInstructions}\n\n{message}";
        var response = await _responsesClient.CreateResponseAsync(composedMessage, previousResponseId, cancellationToken);
        var usage = ToTokenUsageDto(response.Value.Usage);

        var output = response.Value.GetOutputText();
        if (string.IsNullOrWhiteSpace(output))
        {
            _logger.LogWarning("Foundry response for session {SessionId} did not include output text.", sessionId);
            return new FoundryParseWithUsageResult(ParsedCoachResponse.Invalid("Foundry response did not include output text."), usage);
        }

        _lastResponseIds[sessionId] = response.Value.Id;

        return new FoundryParseWithUsageResult(FoundryResponseParser.Parse(output), usage);
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

    private static bool IsCorrectAnswer(string learnerAnswer, ParsedQuizMeta quiz)
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
