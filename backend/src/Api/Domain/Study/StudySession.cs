namespace StudyCoach.BackendApi.Domain.Study;

public sealed record StudySession(Guid SessionId, string Mode, string SkillArea, string UserId, DateTimeOffset StartedAt);
