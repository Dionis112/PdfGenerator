namespace PdfGenerator;


/// <summary>
/// <para>Provides the FopOptions contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию FopOptions для преобразования PDF.</para>
/// </summary>
public sealed record FopOptions
{
    /// <summary><para>Gets the isolated SVG font aliases.</para><para>Изолированные псевдонимы шрифтов SVG.</para></summary>
    internal IReadOnlyDictionary<string, string>? SvgFontAliases
    {
        get; init;
    }

    /// <summary>
    /// <para>Gets or defines the ExecutablePath value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение ExecutablePath, используемое FopOptions.</para>
    /// </summary>
    public string ExecutablePath { get; init; } = "fop";

    /// <summary>
    /// <para>Gets or defines the PrefixArguments value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение PrefixArguments, используемое FopOptions.</para>
    /// </summary>
    public IReadOnlyList<string> PrefixArguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// <para>Gets or defines the ConfigurationPath value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение ConfigurationPath, используемое FopOptions.</para>
    /// </summary>
    public string? ConfigurationPath
    {
        get; init;
    }

    /// <summary>
    /// <para>Gets or defines the Timeout value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение Timeout, используемое FopOptions.</para>
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// <para>Gets or defines the MaxConcurrency value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение MaxConcurrency, используемое FopOptions.</para>
    /// </summary>
    public int MaxConcurrency { get; init; } = 2;

    /// <summary>
    /// <para>Gets or defines the MaxPdfBytes value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение MaxPdfBytes, используемое FopOptions.</para>
    /// </summary>
    public long MaxPdfBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the MaxFoBytes value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение MaxFoBytes, используемое FopOptions.</para>
    /// </summary>
    public long MaxFoBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the MaxImageBytes value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение MaxImageBytes, используемое FopOptions.</para>
    /// </summary>
    public long MaxImageBytes { get; init; } = 32 * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the MaxImages value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение MaxImages, используемое FopOptions.</para>
    /// </summary>
    public int MaxImages { get; init; } = 1000;

    /// <summary>
    /// <para>Gets or defines the MaxTotalImageBytes value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение MaxTotalImageBytes, используемое FopOptions.</para>
    /// </summary>
    public long MaxTotalImageBytes { get; init; } = 128 * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the MaxTemporaryBytes value used by FopOptions.</para>
    /// <para>Возвращает или определяет значение MaxTemporaryBytes, используемое FopOptions.</para>
    /// </summary>
    public long MaxTemporaryBytes { get; init; } = 512 * 1024 * 1024;
}
