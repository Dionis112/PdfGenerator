namespace PdfGenerator;

/// <summary>
/// <para>Describes a Saxon error or warning without retaining document text, error messages or resource paths.</para>
/// <para>Описывает ошибку или предупреждение Saxon без текста документов, сообщений движка и путей ресурсов.</para>
/// </summary>
/// <param name="Code"><para>Standard engine error code, or SAXON_ERROR when unavailable.</para><para>Стандартный код движка либо SAXON_ERROR, если код недоступен.</para></param>
/// <param name="Stage"><para>Compilation or transformation stage.</para><para>Этап компиляции или преобразования.</para></param>
/// <param name="IsWarning"><para>Whether Saxon classified the diagnostic as a warning.</para><para>Признак предупреждения по классификации Saxon.</para></param>
/// <param name="Line"><para>One-based source line, or null when unavailable.</para><para>Строка исходного шаблона с единицы либо null, если недоступна.</para></param>
/// <param name="Column"><para>One-based source column, or null when unavailable.</para><para>Столбец исходного шаблона с единицы либо null, если недоступен.</para></param>
public sealed record XsltExecutionDiagnostic(string Code, ConversionStage Stage, bool IsWarning, int? Line, int? Column);
