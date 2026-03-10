using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StudyCoach.BackendApi.Errors;
using StudyCoach.BackendApi.Application.Chat;
using StudyCoach.BackendApi.Application.Common;
using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Application.Quiz;
using StudyCoach.BackendApi.Application.Session;
using StudyCoach.BackendApi.Application.Skills;

namespace StudyCoach.BackendApi.Controllers;

[ApiController]
[Route("api/study")]
public sealed class StudyController : ControllerBase
{
    private readonly IStudySessionService _sessionService;
    private readonly IStudyChatService _chatService;
    private readonly IStudyQuizService _quizService;
    private readonly IStudySkillsService _skillsService;

    public StudyController(
        IStudySessionService sessionService,
        IStudyChatService chatService,
        IStudyQuizService quizService,
        IStudySkillsService skillsService)
    {
        _sessionService = sessionService;
        _chatService = chatService;
        _quizService = quizService;
        _skillsService = skillsService;
    }

    [HttpPost("session/start")]
    public IActionResult StartSession([FromBody] StartSessionRequest request)
    {
        var result = _sessionService.StartSession(request, GetCurrentUserId());
        return ToActionResult(result, BadRequest);
    }

    [HttpPost("session/bootstrap")]
    public async Task<IActionResult> BootstrapSession(CancellationToken cancellationToken)
    {
        var response = await _sessionService.BootstrapSessionAsync(GetCurrentUserId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var result = await _chatService.ReplyAsync(request, GetCurrentUserId(), cancellationToken);
        return ToActionResult(result, NotFound);
    }

    [HttpPost("quiz/next")]
    public async Task<IActionResult> NextQuizQuestion([FromBody] QuizNextRequest request, CancellationToken cancellationToken)
    {
        var result = await _quizService.NextQuestionAsync(request, GetCurrentUserId(), cancellationToken);
        return ToActionResult(result, NotFound);
    }

    [HttpPost("quiz/answer")]
    public async Task<IActionResult> GradeQuizAnswer([FromBody] QuizAnswerRequest request, CancellationToken cancellationToken)
    {
        var result = await _quizService.GradeAnswerAsync(request, GetCurrentUserId(), cancellationToken);
        return ToActionResult(result, NotFound);
    }

    [HttpGet("skills-outline")]
    public async Task<IActionResult> SkillsOutline(CancellationToken cancellationToken)
    {
        return Ok(await _skillsService.GetOutlineAsync(cancellationToken));
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "local-dev-user";
    }

    private IActionResult ToActionResult<T>(ApplicationResult<T> result, Func<object, IActionResult> failure)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.Error!;
        return failure(new ApiError(error.Code, error.Message));
    }
}

