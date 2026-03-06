using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StudyCoach.BackendApi.Services;

namespace StudyCoach.BackendApi.Controllers;

[ApiController]
[Route("api/study")]
public sealed class StudyController : ControllerBase
{
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

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (!OwnsSession(request.SessionId))
        {
            return NotFound(new { error = "Session not found." });
        }

        var chatResult = await _coachClient.GetChatReplyAsync(request.SessionId, request.Message, cancellationToken);
        if (chatResult.Citations.Count == 0)
        {
            return Ok(new ChatResponse(
                "I can't answer this from verified Microsoft Learn MCP sources right now. Please let me fetch relevant Learn MCP content first.",
                [],
                true,
                "Missing Learn MCP citations."));
        }

        return Ok(new ChatResponse(chatResult.Answer, chatResult.Citations, false, null));
    }

    [HttpPost("quiz/next")]
    public async Task<ActionResult<QuizQuestionResponse>> NextQuizQuestion([FromBody] QuizNextRequest request, CancellationToken cancellationToken)
    {
        if (!OwnsSession(request.SessionId))
        {
            return NotFound(new { error = "Session not found." });
        }

        var question = await _coachClient.GetNextQuizQuestionAsync(cancellationToken);
        return Ok(question);
    }

    [HttpPost("quiz/answer")]
    public async Task<ActionResult<QuizAnswerResponse>> GradeQuizAnswer([FromBody] QuizAnswerRequest request, CancellationToken cancellationToken)
    {
        if (!OwnsSession(request.SessionId))
        {
            return NotFound(new { error = "Session not found." });
        }

        var feedback = await _coachClient.GradeQuizAnswerAsync(request.QuestionId, request.Answer, cancellationToken);
        if (feedback.Citations.Count == 0)
        {
            return Ok(new QuizAnswerResponse(
                false,
                "I can't answer this from verified Microsoft Learn MCP sources right now. Please let me fetch relevant Learn MCP content first.",
                "Always verify from Learn MCP.",
                []));
        }

        return Ok(feedback);
    }

    [HttpGet("skills-outline")]
    public ActionResult<SkillsOutlineResponse> SkillsOutline()
    {
        return Ok(_skillsOutlineProvider.GetOutline());
    }

    private bool OwnsSession(Guid sessionId)
    {
        return _sessionStore.ExistsForUser(sessionId, GetCurrentUserId());
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "local-dev-user";
    }
}
