namespace StudyCoach.BackendApi.Domain.Study;

public static class StudyModes
{
    private static readonly HashSet<string> SupportedModes =
    [
        "Learn",
        "Quiz",
        "Review mistakes",
        "Rapid cram"
    ];

    public static IReadOnlyCollection<string> All => SupportedModes;

    public static bool IsSupported(string mode)
    {
        return SupportedModes.Contains(mode, StringComparer.OrdinalIgnoreCase);
    }
}
