namespace Company.XmlFo;

/// <summary>Settings for the bundled FOP renderer. Cancellation is observed between synchronous render operations.</summary>
public sealed record EmbeddedFopOptions
{
    public string? ConfigurationPath
    {
        get; init;
    }
    public int MaxConcurrency { get; init; } = 2;
    public long MaxPdfBytes { get; init; } = 64 * 1024 * 1024;
    public long MaxFoBytes { get; init; } = 64 * 1024 * 1024;
    public long MaxImageBytes { get; init; } = 32 * 1024 * 1024;
}
