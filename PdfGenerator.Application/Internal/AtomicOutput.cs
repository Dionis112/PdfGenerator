namespace PdfGenerator;


/// <summary>
/// <para>Provides the AtomicOutput contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию AtomicOutput для преобразования PDF.</para>
/// </summary>
internal static class AtomicOutput
{

    /// <summary>
    /// <para>Executes Prepare as part of the AtomicOutput conversion workflow.</para>
    /// <para>Выполняет Prepare в цепочке преобразования AtomicOutput.</para>
    /// </summary>
    /// <param name="target"><para>The target argument.</para><para>Аргумент target.</para></param>
    /// <param name="inputs"><para>The inputs argument.</para><para>Аргумент inputs.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
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


    /// <summary>
    /// <para>Executes Publish as part of the AtomicOutput conversion workflow.</para>
    /// <para>Выполняет Publish в цепочке преобразования AtomicOutput.</para>
    /// </summary>
    /// <param name="temporary"><para>The temporary argument.</para><para>Аргумент temporary.</para></param>
    /// <param name="target"><para>The target argument.</para><para>Аргумент target.</para></param>
    /// <param name="token"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public static void Publish(string temporary, string target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        File.Move(temporary, Path.GetFullPath(target), overwrite: true);
    }
}
