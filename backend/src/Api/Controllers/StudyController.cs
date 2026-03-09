using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StudyCoach.BackendApi.Services;

namespace StudyCoach.BackendApi.Controllers;

[ApiController]
[Route("api/study")]
public sealed class StudyController : ControllerBase
{
    private const string LearnRefusalMessage =
        "I can't answer this from verified Microsoft Learn MCP sources right now. Please let me fetch relevant Learn MCP content first.";

    private readonly ISessionStore _sessionStore;
    private readonly IFoundryStudyCoachClient _coachClient;
    private readonly ISkillsOutlineProvider _skillsOutlineProvider;

    public StudyController(
        ISessionStore sessionStore,
        IFoundryStudyCoachClient coachClient,
        ISkillsOutlineProvider skillsOutlineProvider)
    {
        _sessionStore = sessionStore;
        _coachClient = coachClient;
        _skillsOutlineProvider = skillsOutlineProvider;
    }

    [HttpPost("session/start")]
    public ActionResult<StartSessionResponse> StartSession([FromBody] StartSessionRequest request)
    {
        if (!StudyModes.All.Contains(request.Mode, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Unsupported mode." });
        }

        if (string.IsNullOrWhiteSpace(request.SkillArea))
        {
            return BadRequest(new { error = "Skill area is required." });
        }

        var userId = GetCurrentUserId();
        var session = _sessionStore.StartSession(request.Mode, request.SkillArea, userId);

        return Ok(new StartSessionResponse(
            session.SessionId,
            session.Mode,
            session.SkillArea,
            $"Session started in {session.Mode} mode for {session.SkillArea}."));
    }

    [HttpPost("session/bootstrap")]
    public async Task<ActionResult<BootstrapSessionResponse>> BootstrapSession(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var session = _sessionStore.StartSession("Learn", "onboarding-pending", userId);
        var onboarding = await _coachClient.GetOnboardingOptionsAsync(session.SessionId, cancellationToken);

        return Ok(new BootstrapSessionResponse(
            session.SessionId,
            onboarding.Prompt,
            onboarding.AreaOptions,
            onboarding.ModeOptions,
            onboarding.Usage));
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (!_sessionStore.TryGetSessionForUser(request.SessionId, GetCurrentUserId(), out var session) || session is null)
        {
            return NotFound(new { error = "Session not found." });
        }

        var chatResult = await _coachClient.GetChatReplyAsync(request.SessionId, session.SkillArea, request.Message, cancellationToken);
        if (chatResult.Refused || chatResult.Citations.Count == 0)
        {
            return Ok(new ChatResponse(
                LearnRefusalMessage,
                [],
                true,
                chatResult.RefusalReason ?? "Missing or invalid Learn MCP citations.",
                null,
                chatResult.Usage));
        }

        return Ok(new ChatResponse(chatResult.Answer, chatResult.Citations, false, null, chatResult.Meta, chatResult.Usage));
    }

    [HttpPost("quiz/next")]
    public async Task<ActionResult<QuizQuestionResponse>> NextQuizQuestion([FromBody] QuizNextRequest request, CancellationToken cancellationToken)
    {
        if (!_sessionStore.TryGetSessionForUser(request.SessionId, GetCurrentUserId(), out var session) || session is null)
        {
            return NotFound(new { error = "Session not found." });
        }

        var question = await _coachClient.GetNextQuizQuestionAsync(request.SessionId, session.SkillArea, cancellationToken);
        if ((question.Citations?.Count ?? 0) == 0)
        {
            return Ok(new QuizQuestionResponse(Guid.NewGuid(), LearnRefusalMessage, null, [], question.Usage));
        }

        return Ok(question);
    }

    [HttpPost("quiz/answer")]
    public async Task<ActionResult<QuizAnswerResponse>> GradeQuizAnswer([FromBody] QuizAnswerRequest request, CancellationToken cancellationToken)
    {
        if (!_sessionStore.TryGetSessionForUser(request.SessionId, GetCurrentUserId(), out var session) || session is null)
        {
            return NotFound(new { error = "Session not found." });
        }

        var feedback = await _coachClient.GradeQuizAnswerAsync(
            request.SessionId,
            session.SkillArea,
            request.QuestionId,
            request.Answer,
            cancellationToken);

        if (feedback.Citations.Count == 0)
        {
            return Ok(new QuizAnswerResponse(
                false,
                LearnRefusalMessage,
                "Always verify from Learn MCP.",
                [],
                feedback.Usage));
        }

        return Ok(feedback);
    }

    [HttpGet("skills-outline")]
    public async Task<ActionResult<SkillsOutlineResponse>> SkillsOutline(CancellationToken cancellationToken)
    {
        return Ok(await _skillsOutlineProvider.GetOutlineAsync(cancellationToken));
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "local-dev-user";
    }
}
