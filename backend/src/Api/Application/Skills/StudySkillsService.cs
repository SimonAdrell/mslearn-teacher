using StudyCoach.BackendApi.Application.Contracts;

namespace StudyCoach.BackendApi.Application.Skills;

public interface IStudySkillsService
{
    Task<SkillsOutlineResponse> GetOutlineAsync(CancellationToken cancellationToken);
}

public sealed class StudySkillsService : IStudySkillsService
{
    private readonly ISkillsOutlineProvider _provider;

    public StudySkillsService(ISkillsOutlineProvider provider)
    {
        _provider = provider;
    }

    public Task<SkillsOutlineResponse> GetOutlineAsync(CancellationToken cancellationToken)
    {
        return _provider.GetOutlineAsync(cancellationToken);
    }
}
