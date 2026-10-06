using System.Text.RegularExpressions;
using Saxon.Api;

namespace PdfGenerator;

/// <summary>
/// <para>Collects bounded, job-local diagnostics; raw Saxon messages and custom error names are discarded.</para>
/// <para>Собирает ограниченные диагностики задания; сообщения Saxon и пользовательские имена ошибок отбрасываются.</para>
/// </summary>
/// <param name="stage"><para>Stage reported by this collector.</para><para>Этап, фиксируемый этим сборщиком.</para></param>
internal sealed class SaxonDiagnosticCollector(ConversionStage stage)
{
    /// <summary><para>Maximum diagnostics retained for each stage.</para><para>Максимум диагностик каждого этапа.</para></summary>
    internal const int Limit = 64;

    /// <summary><para>Recognizes standard Saxon and W3C error codes.</para><para>Распознаёт стандартные коды Saxon и W3C.</para></summary>
    private static readonly Regex StandardCode = new("^[A-Z]{4}[0-9]{4}$", RegexOptions.CultureInvariant);

    /// <summary><para>Diagnostics retained by this collector.</para><para>Диагностики, сохранённые сборщиком.</para></summary>
    private readonly List<XsltExecutionDiagnostic> diagnostics = [];

    /// <summary><para>Records safe fields from a Saxon callback.</para><para>Сохраняет безопасные поля из вызова Saxon.</para></summary>
    /// <param name="error"><para>Engine diagnostic.</para><para>Диагностика движка.</para></param>
    public void Report(Error error)
    {
        if (diagnostics.Count >= Limit)
        {
            int warning = error.IsWarning ? -1 : diagnostics.FindLastIndex(d => d.IsWarning);
            if (warning < 0)
            {
                return;
            }
            diagnostics.RemoveAt(warning);
        }
        string code = error.ErrorCode?.LocalName ?? "";
        bool standardNamespace = error.ErrorCode?.Uri is "http://www.w3.org/2005/xqt-errors" or "http://saxon.sf.net/";
        diagnostics.Add(new(standardNamespace && StandardCode.IsMatch(code) ? code : "SAXON_ERROR", stage, error.IsWarning,
            error.Location?.LineNumber > 0 ? error.Location.LineNumber : null,
            error.Location?.ColumnNumber > 0 ? error.Location.ColumnNumber : null));
    }

    /// <summary><para>Returns an immutable snapshot independent of the callback lifetime.</para><para>Возвращает неизменяемый снимок, независимый от времени жизни обработчика.</para></summary>
    /// <returns><para>Collected diagnostics.</para><para>Собранные диагностики.</para></returns>
    public IReadOnlyList<XsltExecutionDiagnostic> Snapshot() => Array.AsReadOnly(diagnostics.ToArray());
}

/// <summary><para>Shares an executable and its immutable compilation warnings across jobs.</para><para>Хранит исполняемый шаблон и неизменяемые предупреждения компиляции для заданий.</para></summary>
/// <param name="Executable"><para>Compiled stylesheet.</para><para>Скомпилированный шаблон.</para></param>
/// <param name="Diagnostics"><para>Compilation diagnostics.</para><para>Диагностики компиляции.</para></param>
internal sealed record CompiledStylesheet(XsltExecutable Executable, IReadOnlyList<XsltExecutionDiagnostic> Diagnostics);
