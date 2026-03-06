namespace StudyCoach.BackendApi.Services;

public interface ISkillsOutlineProvider
{
    SkillsOutlineResponse GetOutline();
}

public sealed class SkillsOutlineProvider : ISkillsOutlineProvider
{
    // In v1 this is static. Replace with MCP-backed retrieval orchestration in production.
    public SkillsOutlineResponse GetOutline() => new(
        [
            new SkillArea(
                "Plan and manage an Azure AI solution",
                "15-20%",
                ["Select service resources", "Plan responsible AI and governance"]),
            new SkillArea(
                "Implement generative AI solutions",
                "20-25%",
                ["Integrate Azure OpenAI", "Ground prompts with enterprise data"]),
            new SkillArea(
                "Implement computer vision solutions",
                "15-20%",
                ["Analyze images", "Extract text with OCR"]),
            new SkillArea(
                "Implement natural language processing solutions",
                "30-35%",
                ["Analyze text", "Build question answering solutions"]),
            new SkillArea(
                "Implement knowledge mining and information extraction solutions",
                "10-15%",
                ["Build Azure AI Search pipelines", "Manage indexers and enrichment"])
        ]);
}
