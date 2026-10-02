namespace Company.XmlFo;

public sealed record XmlFoOptions
{
    public int MaxConcurrency { get; init; } = 2;
    public long MaxInputBytes { get; init; } = 16 * 1024 * 1024;
    public long MaxOutputBytes { get; init; } = 64 * 1024 * 1024;
}
