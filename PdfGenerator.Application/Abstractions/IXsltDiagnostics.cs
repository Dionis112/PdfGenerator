namespace PdfGenerator;


/// <summary>
/// <para>Provides the IXsltDiagnostics contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию IXsltDiagnostics для преобразования PDF.</para>
/// </summary>
public interface IXsltDiagnostics
{

    /// <summary>
    /// <para>Executes Analyze as part of the IXsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет Analyze в цепочке преобразования IXsltDiagnostics.</para>
    /// </summary>
    /// <param name="xslt"><para>The xslt argument.</para><para>Аргумент xslt.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    XsltDiagnosticReport Analyze(string xslt);


    /// <summary>
    /// <para>Executes AnalyzeFile as part of the IXsltDiagnostics conversion workflow.</para>
    /// <para>Выполняет AnalyzeFile в цепочке преобразования IXsltDiagnostics.</para>
    /// </summary>
    /// <param name="xsltPath"><para>Local stylesheet path; relative dependencies require a permitted base directory.</para><para>Путь локального шаблона; относительным зависимостям требуется разрешённый базовый каталог.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    XsltDiagnosticReport AnalyzeFile(string xsltPath);
}


/// <summary>
/// <para>Provides the XsltDiagnosticSeverity contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XsltDiagnosticSeverity для преобразования PDF.</para>
/// </summary>
public enum XsltDiagnosticSeverity
{

    /// <summary>
    /// <para>Gets or defines the Info value used by XsltDiagnosticSeverity.</para>
    /// <para>Возвращает или определяет значение Info, используемое XsltDiagnosticSeverity.</para>
    /// </summary>
    Info,

    /// <summary>
    /// <para>Gets or defines the Warning value used by XsltDiagnosticSeverity.</para>
    /// <para>Возвращает или определяет значение Warning, используемое XsltDiagnosticSeverity.</para>
    /// </summary>
    Warning,

    /// <summary>
    /// <para>Gets or defines the Error value used by XsltDiagnosticSeverity.</para>
    /// <para>Возвращает или определяет значение Error, используемое XsltDiagnosticSeverity.</para>
    /// </summary>
    Error
}


/// <summary>
/// <para>Provides the XsltFunctionSupport contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XsltFunctionSupport для преобразования PDF.</para>
/// </summary>
public enum XsltFunctionSupport
{

    /// <summary>
    /// <para>Gets or defines the Supported value used by XsltFunctionSupport.</para>
    /// <para>Возвращает или определяет значение Supported, используемое XsltFunctionSupport.</para>
    /// </summary>
    Supported,

    /// <summary>
    /// <para>Gets or defines the Unsupported value used by XsltFunctionSupport.</para>
    /// <para>Возвращает или определяет значение Unsupported, используемое XsltFunctionSupport.</para>
    /// </summary>
    Unsupported,

    /// <summary>
    /// <para>Gets or defines the ReviewRequired value used by XsltFunctionSupport.</para>
    /// <para>Возвращает или определяет значение ReviewRequired, используемое XsltFunctionSupport.</para>
    /// </summary>
    ReviewRequired
}


/// <summary>
/// <para>Provides the XsltFunctionUsage contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XsltFunctionUsage для преобразования PDF.</para>
/// </summary>
/// <param name="NamespaceUri"><para>The NamespaceUri argument.</para><para>Аргумент NamespaceUri.</para></param>
/// <param name="Name"><para>The Name argument.</para><para>Аргумент Name.</para></param>
/// <param name="Occurrences"><para>The Occurrences argument.</para><para>Аргумент Occurrences.</para></param>
/// <param name="Support"><para>The Support argument.</para><para>Аргумент Support.</para></param>
/// <param name="Explanation"><para>The Explanation argument.</para><para>Аргумент Explanation.</para></param>
public sealed record XsltFunctionUsage(
    string NamespaceUri,
    string Name,
    int Occurrences,
    XsltFunctionSupport Support,
    string Explanation);


/// <summary>
/// <para>Provides the XsltDiagnostic contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XsltDiagnostic для преобразования PDF.</para>
/// </summary>
/// <param name="Code"><para>The Code argument.</para><para>Аргумент Code.</para></param>
/// <param name="Severity"><para>The Severity argument.</para><para>Аргумент Severity.</para></param>
/// <param name="Message"><para>The Message argument.</para><para>Аргумент Message.</para></param>
/// <param name="Source"><para>The Source argument.</para><para>Аргумент Source.</para></param>
/// <param name="Line"><para>The Line argument.</para><para>Аргумент Line.</para></param>
public sealed record XsltDiagnostic(
    string Code,
    XsltDiagnosticSeverity Severity,
    string Message,
    string? Source = null,
    int? Line = null);


/// <summary>
/// <para>Provides the XsltDiagnosticReport contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию XsltDiagnosticReport для преобразования PDF.</para>
/// </summary>
/// <param name="Functions"><para>The Functions argument.</para><para>Аргумент Functions.</para></param>
/// <param name="Diagnostics"><para>The Diagnostics argument.</para><para>Аргумент Diagnostics.</para></param>
/// <param name="StylesheetsInspected"><para>The StylesheetsInspected argument.</para><para>Аргумент StylesheetsInspected.</para></param>
public sealed record XsltDiagnosticReport(
    IReadOnlyList<XsltFunctionUsage> Functions,
    IReadOnlyList<XsltDiagnostic> Diagnostics,
    int StylesheetsInspected)
{

    /// <summary>
    /// <para>Gets or defines the HasErrors value used by XsltDiagnosticReport.</para>
    /// <para>Возвращает или определяет значение HasErrors, используемое XsltDiagnosticReport.</para>
    /// </summary>
    public bool HasErrors => Diagnostics.Any(item => item.Severity == XsltDiagnosticSeverity.Error);
}
