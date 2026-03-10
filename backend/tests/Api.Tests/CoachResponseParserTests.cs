using FluentAssertions;
using StudyCoach.BackendApi.Infrastructure.Foundry.Parsing;

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

        var result = FoundryResponseParser.Parse(output);

        result.IsValid.Should().BeTrue();
        result.Citations.Should().HaveCount(1);
        result.ChatMeta.Should().NotBeNull();
        result.ChatMeta!.SkillOutlineArea.Should().Be("Implement natural language processing solutions");
        result.CoachText.Should().Contain("Drill NLP");
    }

    [Fact]
    public void Parse_WithOnboardingOptions_ReturnsOnboardingVariant()
    {
        const string output = """
{
  "coach_text": "Let's start your AI-102 session.",
  "response_type": "onboarding_options",
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

        var result = FoundryResponseParser.Parse(output);

        result.IsValid.Should().BeTrue();
        result.Onboarding.Should().NotBeNull();
        result.Onboarding!.AreaOptions.Should().HaveCount(2);
        result.Onboarding.ModeOptions.Should().Contain("Learn");
    }

    [Fact]
    public void Parse_WithInvalidOnboardingMode_ReturnsInvalidResult()
    {
        const string output = """
{
  "coach_text": "Setup options.",
  "response_type": "onboarding_options",
  "mcp_verified": false,
  "onboarding": {
    "prompt": "Pick.",
    "area_options": ["Area"],
    "mode_options": ["Bootcamp"]
  }
}
""";

        var result = FoundryResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("supported study modes");
    }

    [Fact]
    public void Parse_WithNonJsonResponse_ReturnsInvalidResult()
    {
        var result = FoundryResponseParser.Parse("coach_text: not valid json");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("single JSON object");
    }

    [Fact]
    public void Parse_WithJsonMissingCoachText_ReturnsInvalidResult()
    {
        const string output = """
{
  "response_type": "teach",
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

        var result = FoundryResponseParser.Parse(output);

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

        var result = FoundryResponseParser.Parse(output);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Microsoft Learn");
    }
}
