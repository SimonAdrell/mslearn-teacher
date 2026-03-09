using FluentAssertions;
using StudyCoach.BackendApi.Services;

namespace Api.Tests;

public class CoachResponseParserTests
{
    [Fact]
    public void Parse_WithTeachResponse_ReturnsTeachVariant()
    {
        const string output = """
{
  "response_type": "teach",
  "purpose": "Teach core NLP mapping",
  "coach_text": "Drill NLP service selection.",
  "skill_outline_area": "Implement natural language processing solutions",
  "must_know": ["Use Azure AI Language for intent and entities"],
  "exam_traps": ["Choosing Azure AI Search for intent classification"],
  "citations": [
    {
      "title": "What is Azure AI Language?",
      "url": "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
      "retrieved_at": "2026-03-06"
    }
  ],
  "mcp_verified": true
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeTrue();
        var parsed = result.Response.Should().BeOfType<CoachParsedTeachLike>().Subject;
        parsed.Meta.SkillOutlineArea.Should().Be("Implement natural language processing solutions");
        parsed.CoachText.Should().Contain("Drill NLP");
    }

    [Fact]
    public void Parse_WithQuizFeedbackMissingCorrectOption_ReturnsInvalidResult()
    {
        const string output = """
{
  "response_type": "quiz_feedback",
  "purpose": "Grade learner answer",
  "coach_text": "Feedback.",
  "skill_outline_area": "Implement natural language processing solutions",
  "must_know": ["Use Azure AI Language"],
  "exam_traps": ["Confusing services"],
  "citations": [
    {
      "title": "What is Azure AI Language?",
      "url": "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
      "retrieved_at": "2026-03-06"
    }
  ],
  "mcp_verified": true,
  "quiz": {
    "explanation": "Because.",
    "memory_rule": "Remember A."
  }
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("quiz.correct_option");
    }

    [Fact]
    public void Parse_WithQuizQuestionExtraOption_ReturnsInvalidResult()
    {
        const string output = """
{
  "response_type": "quiz_question",
  "purpose": "Ask one question",
  "coach_text": "Question.",
  "skill_outline_area": "Implement natural language processing solutions",
  "must_know": ["Use Azure AI Language"],
  "exam_traps": ["Confusing services"],
  "citations": [
    {
      "title": "What is Azure AI Language?",
      "url": "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
      "retrieved_at": "2026-03-06"
    }
  ],
  "mcp_verified": true,
  "quiz": {
    "question": "What should you use?",
    "options": {
      "A": "Azure AI Language",
      "B": "Azure AI Vision",
      "C": "Azure AI Search",
      "D": "None"
    }
  }
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("exactly A, B, and C");
    }

    [Fact]
    public void Parse_WithOnboardingOptions_ReturnsOnboardingVariant()
    {
        const string output = """
{
  "response_type": "onboarding_options",
  "purpose": "Provide onboarding options",
  "coach_text": "Let's start your AI-102 session.",
  "mcp_verified": false,
  "onboarding": {
    "prompt": "Pick a skill area.",
    "area_options": [
      "Implement natural language processing solutions (30-35%)",
      "Implement computer vision solutions (15-20%)"
    ],
    "mode_options": ["Learn", "Quiz"]
  }
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeTrue();
        var parsed = result.Response.Should().BeOfType<CoachParsedOnboarding>().Subject;
        parsed.AreaOptions.Should().HaveCount(2);
        parsed.ModeOptions.Should().Contain("Learn");
    }

    [Fact]
    public void Parse_WithInvalidOnboardingMode_ReturnsInvalidResult()
    {
        const string output = """
{
  "response_type": "onboarding_options",
  "purpose": "Provide onboarding options",
  "coach_text": "Setup options.",
  "mcp_verified": false,
  "onboarding": {
    "prompt": "Pick.",
    "area_options": ["Area"],
    "mode_options": ["Bootcamp"]
  }
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("supported study modes");
    }

    [Fact]
    public void Parse_WithoutJson_ReturnsInvalidResult()
    {
        var result = CoachResponseParser.Parse("No structured object here");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("valid JSON");
    }

    [Fact]
    public void Parse_WithNonLearnCitation_ReturnsInvalidResult()
    {
        const string output = """
{
  "response_type": "teach",
  "purpose": "Teach references",
  "coach_text": "Check references.",
  "skill_outline_area": "Implement natural language processing solutions",
  "must_know": ["A"],
  "exam_traps": ["B"],
  "citations": [
    {
      "title": "External",
      "url": "https://example.com/docs",
      "retrieved_at": "2026-03-06"
    }
  ],
  "mcp_verified": true
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Microsoft Learn");
    }

    [Fact]
    public void Parse_WithRefusalAndMcpVerifiedTrue_ReturnsInvalidResult()
    {
        const string output = """
{
  "response_type": "refusal",
  "purpose": "Unable to verify",
  "coach_text": "I can't answer this from verified Microsoft Learn MCP sources right now. Please let me fetch relevant Learn MCP content first.",
  "mcp_verified": true
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("mcp_verified must be false");
    }
}
