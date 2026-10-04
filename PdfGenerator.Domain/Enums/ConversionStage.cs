namespace PdfGenerator;


/// <summary>
/// <para>Provides the ConversionStage contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию ConversionStage для преобразования PDF.</para>
/// </summary>
public enum ConversionStage
{

    /// <summary>
    /// <para>Gets or defines the Input value used by ConversionStage.</para>
    /// <para>Возвращает или определяет значение Input, используемое ConversionStage.</para>
    /// </summary>
    Input,
    /// <summary>
    /// <para>Gets or defines the Compile value used by ConversionStage.</para>
    /// <para>Возвращает или определяет значение Compile, используемое ConversionStage.</para>
    /// </summary>
    Compile,
    /// <summary>
    /// <para>Gets or defines the Transform value used by ConversionStage.</para>
    /// <para>Возвращает или определяет значение Transform, используемое ConversionStage.</para>
    /// </summary>
    Transform,
    /// <summary>
    /// <para>Gets or defines the Render value used by ConversionStage.</para>
    /// <para>Возвращает или определяет значение Render, используемое ConversionStage.</para>
    /// </summary>
    Render,
    /// <summary>
    /// <para>Gets or defines the Publish value used by ConversionStage.</para>
    /// <para>Возвращает или определяет значение Publish, используемое ConversionStage.</para>
    /// </summary>
    Publish
}
