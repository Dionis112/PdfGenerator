namespace PdfGenerator;


/// <summary>
/// <para>Provides the EmbeddedFopOptions contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию EmbeddedFopOptions для преобразования PDF.</para>
/// </summary>
public sealed record EmbeddedFopOptions
{

    /// <summary>
    /// <para>Gets or defines the ConfigurationPath value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение ConfigurationPath, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public string? ConfigurationPath
    {
        get; init;
    }

    /// <summary>
    /// <para>Gets or defines the MaxConcurrency value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение MaxConcurrency, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public int MaxConcurrency { get; init; } = 2;

    /// <summary>
    /// <para>Gets or defines the MaxPdfBytes value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение MaxPdfBytes, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public long MaxPdfBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the MaxFoBytes value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение MaxFoBytes, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public long MaxFoBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the MaxImageBytes value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение MaxImageBytes, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public long MaxImageBytes { get; init; } = 32 * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the FactoryCacheEntries value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение FactoryCacheEntries, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public int FactoryCacheEntries { get; init; } = 4;

    /// <summary>
    /// <para>Maximum estimated retained factory/font bytes; also bounds one font snapshot.</para>
    /// <para>Максимальный оценочный объём сохраняемых фабрик и шрифтов; ограничивает также один снимок шрифтов.</para>
    /// </summary>
    public long FactoryCacheBytes { get; init; } = 128L * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the MaxImages value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение MaxImages, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public int MaxImages { get; init; } = 1000;

    /// <summary>
    /// <para>Gets or defines the MaxTotalImageBytes value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение MaxTotalImageBytes, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public long MaxTotalImageBytes { get; init; } = 128 * 1024 * 1024;

    /// <summary>
    /// <para>Gets or defines the MaxTemporaryBytes value used by EmbeddedFopOptions.</para>
    /// <para>Возвращает или определяет значение MaxTemporaryBytes, используемое EmbeddedFopOptions.</para>
    /// </summary>
    public long MaxTemporaryBytes { get; init; } = 512 * 1024 * 1024;
}
