namespace StudyCoach.BackendApi.Services;

public static class StudyModes
{
    public static readonly string[] All = ["Learn", "Quiz", "Review mistakes", "Rapid cram"];
}

public record StartSessionRequest(string Mode, string SkillArea);
public record StartSessionResponse(Guid SessionId, string Mode, string SkillArea, string WelcomeMessage);

public record ChatRequest(Guid SessionId, string Message);
public record ChatResponse(string Answer, IReadOnlyList<Citation> Citations, bool Refused, string? RefusalReason);

public record QuizNextRequest(Guid SessionId);
public record QuizQuestionResponse(Guid QuestionId, string Question, IReadOnlyList<string>? Choices);

public record QuizAnswerRequest(Guid SessionId, Guid QuestionId, string Answer);
public record QuizAnswerResponse(bool Correct, string Explanation, string MemoryRule, IReadOnlyList<Citation> Citations);

public record Citation(string Title, string Url, DateOnly RetrievedAt);

public record SkillsOutlineResponse(IReadOnlyList<SkillArea> Areas);
public record SkillArea(string Name, string WeightPercent, IReadOnlyList<string> Includes);
