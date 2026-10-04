namespace PdfGenerator;


/// <summary>
/// <para>Provides the ConversionException contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию ConversionException для преобразования PDF.</para>
/// </summary>
/// <param name="stage"><para>The stage argument.</para><para>Аргумент stage.</para></param>
/// <param name="message"><para>The message argument.</para><para>Аргумент message.</para></param>
/// <param name="innerException"><para>The innerException argument.</para><para>Аргумент innerException.</para></param>
public sealed class ConversionException(ConversionStage stage, string message, Exception? innerException = null)
    : Exception(message, innerException)
{

    /// <summary>
    /// <para>Gets or defines the Stage value used by ConversionException.</para>
    /// <para>Возвращает или определяет значение Stage, используемое ConversionException.</para>
    /// </summary>
    public ConversionStage Stage { get; } = stage;
}
