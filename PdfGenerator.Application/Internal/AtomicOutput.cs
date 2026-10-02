namespace Company.XmlFo;

internal static class AtomicOutput
{
    public static string Prepare(string target, params string[] inputs)
    {
        string full = Path.GetFullPath(target);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (inputs.Any(p => string.Equals(Path.GetFullPath(p), full, comparison)))
        {
            throw new ArgumentException("Output must differ from input files.", nameof(target));
        }

        return Path.Combine(Path.GetDirectoryName(full)!, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");
    }

    public static void Publish(string temporary, string target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        File.Move(temporary, Path.GetFullPath(target), overwrite: true);
    }
}
