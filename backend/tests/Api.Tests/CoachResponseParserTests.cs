using FluentAssertions;
using StudyCoach.BackendApi.Services;

namespace Api.Tests;

public class CoachResponseParserTests
{
    [Fact]
    public void Parse_WithValidJsonObject_ReturnsStructuredResult()
    {
        const string output = """
{
  "coach_text": "Purpose: Drill NLP service selection.",
  "response_type": "teach",
  "purpose": "Teach core NLP mapping",
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
        result.Citations.Should().HaveCount(1);
        result.ChatMeta.Should().NotBeNull();
        result.ChatMeta!.SkillOutlineArea.Should().Be("Implement natural language processing solutions");
        result.CoachText.Should().Contain("Drill NLP");
    }

    [Fact]
    public void Parse_WithNonJsonPayload_ReturnsInvalidResult()
    {
        var result = CoachResponseParser.Parse("No structured payload here");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("single JSON object");
    }

    [Fact]
    public void Parse_WithoutCoachText_ReturnsInvalidResult()
    {
        const string output = """
{
  "response_type": "teach",
  "purpose": "Teach core NLP mapping",
  "skill_outline_area": "Implement natural language processing solutions",
  "must_know": ["A"],
  "exam_traps": ["B"],
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

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("coach_text");
    }

    [Fact]
    public void Parse_WithNonLearnCitation_ReturnsInvalidResult()
    {
        const string output = """
{
  "coach_text": "Check references.",
  "response_type": "teach",
  "purpose": "Teach references",
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
    public void Parse_WithQuizMissingOptionC_ReturnsInvalidResult()
    {
        const string output = """
{
  "coach_text": "Question follows.",
  "response_type": "quiz_question",
  "purpose": "Check retention",
  "skill_outline_area": "Implement natural language processing solutions",
  "must_know": ["A"],
  "exam_traps": ["B"],
  "citations": [
    {
      "title": "What is Azure AI Language?",
      "url": "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
      "retrieved_at": "2026-03-06"
    }
  ],
  "mcp_verified": true,
  "quiz": {
    "question": "Pick one",
    "options": {
      "A": "Option A",
      "B": "Option B"
    }
  }
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("A, B, and C options");
    }

    [Fact]
    public void Parse_WithQuizInvalidCorrectOption_ReturnsInvalidResult()
    {
        const string output = """
{
  "coach_text": "Question follows.",
  "response_type": "quiz_feedback",
  "purpose": "Grade question",
  "skill_outline_area": "Implement natural language processing solutions",
  "must_know": ["A"],
  "exam_traps": ["B"],
  "citations": [
    {
      "title": "What is Azure AI Language?",
      "url": "https://learn.microsoft.com/en-us/azure/ai-services/language-service/overview",
      "retrieved_at": "2026-03-06"
    }
  ],
  "mcp_verified": true,
  "quiz": {
    "question": "Pick one",
    "options": {
      "A": "Option A",
      "B": "Option B",
      "C": "Option C"
    },
    "correct_option": "D"
  }
}
""";

        var result = CoachResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("must be A, B, or C");
    }
}
