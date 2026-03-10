using StudyCoach.BackendApi.Application.Common;
using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Domain.Study;
using StudyCoach.BackendApi.Infrastructure.Foundry;
using StudyCoach.BackendApi.Infrastructure.Sessions;

namespace StudyCoach.BackendApi.Application.Session;

public interface IStudySessionService
{
    ApplicationResult<StartSessionResponse> StartSession(StartSessionRequest request, string userId);
    Task<BootstrapSessionResponse> BootstrapSessionAsync(string userId, CancellationToken cancellationToken);
}

public sealed class StudySessionService : IStudySessionService
{
    private readonly IStudySessionRepository _sessions;
    private readonly IOnboardingCoach _onboardingCoach;

    public StudySessionService(IStudySessionRepository sessions, IOnboardingCoach onboardingCoach)
    {
        _sessions = sessions;
        _onboardingCoach = onboardingCoach;
    }

    public ApplicationResult<StartSessionResponse> StartSession(StartSessionRequest request, string userId)
    {
        if (!StudyModes.IsSupported(request.Mode))
        {
            return ApplicationResult<StartSessionResponse>.Failure("unsupported_mode", "Unsupported mode.");
        }

        if (string.IsNullOrWhiteSpace(request.SkillArea))
        {
            return ApplicationResult<StartSessionResponse>.Failure("invalid_skill_area", "Skill area is required.");
        }

        var session = _sessions.Start(request.Mode, request.SkillArea, userId);
        return ApplicationResult<StartSessionResponse>.Success(
            new StartSessionResponse(
                session.SessionId,
                session.Mode,
                session.SkillArea,
                $"Session started in {session.Mode} mode for {session.SkillArea}."));
    }

    public async Task<BootstrapSessionResponse> BootstrapSessionAsync(string userId, CancellationToken cancellationToken)
    {
        var session = _sessions.Start("Learn", "onboarding-pending", userId);
        var onboarding = await _onboardingCoach.GetOnboardingOptionsAsync(session.SessionId, cancellationToken);

        return new BootstrapSessionResponse(
            session.SessionId,
            onboarding.Prompt,
            onboarding.AreaOptions,
            onboarding.ModeOptions,
            onboarding.Usage);
    }
}
