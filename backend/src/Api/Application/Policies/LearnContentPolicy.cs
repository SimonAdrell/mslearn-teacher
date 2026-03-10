using StudyCoach.BackendApi.Application.Contracts;
using StudyCoach.BackendApi.Infrastructure.Foundry;

namespace StudyCoach.BackendApi.Application.Policies;

internal static class LearnContentPolicy
{
    public const string LearnRefusalMessage =
        "I can't answer this from verified Microsoft Learn MCP sources right now. Please let me fetch relevant Learn MCP content first.";

    public static ChatResponse ToChatResponse(CoachChatResult result)
    {
        if (result.Refused || result.Citations.Count == 0)
        {
            return new ChatResponse(
                LearnRefusalMessage,
                [],
                true,
                result.RefusalReason ?? "Missing or invalid Learn MCP citations.",
                null,
                result.Usage);
        }

        return new ChatResponse(result.Answer, result.Citations, false, null, result.Meta, result.Usage);
    }

    public static QuizQuestionResponse ToQuestionResponse(QuizQuestionResponse question)
    {
        if ((question.Citations?.Count ?? 0) > 0)
        {
            return question;
        }

        return new QuizQuestionResponse(Guid.NewGuid(), LearnRefusalMessage, null, [], question.Usage);
    }

    public static QuizAnswerResponse ToAnswerResponse(QuizAnswerResponse feedback)
    {
        if (feedback.Citations.Count > 0)
        {
            return feedback;
        }

        return new QuizAnswerResponse(
            false,
            LearnRefusalMessage,
            "Always verify from Learn MCP.",
            [],
            feedback.Usage);
    }
}
