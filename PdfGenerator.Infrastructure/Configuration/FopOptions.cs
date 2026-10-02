namespace Company.XmlFo;

/// <summary>FOP executable must be directly launchable (for Windows use java.exe and PrefixArguments).</summary>
public sealed record FopOptions
{
    public string ExecutablePath { get; init; } = "fop";
    public IReadOnlyList<string> PrefixArguments { get; init; } = Array.Empty<string>();
    public string? ConfigurationPath
    {
        get; init;
    }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public int MaxConcurrency { get; init; } = 2;
    public long MaxPdfBytes { get; init; } = 64 * 1024 * 1024;
    public long MaxFoBytes { get; init; } = 64 * 1024 * 1024;
    public long MaxImageBytes { get; init; } = 32 * 1024 * 1024;
}
