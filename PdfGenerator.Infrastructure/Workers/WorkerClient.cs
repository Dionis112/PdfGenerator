using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the WorkerClient contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию WorkerClient для преобразования PDF.</para>
/// </summary>
/// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
/// <param name="root"><para>The root argument.</para><para>Аргумент root.</para></param>
/// <param name="settings"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
internal sealed class WorkerClient(PdfWorkerOptions options, string root, WorkerHostSettings settings) : IAsyncDisposable
{

    /// <summary>
    /// <para>Gets or defines the process value used by WorkerClient.</para>
    /// <para>Возвращает или определяет значение process, используемое WorkerClient.</para>
    /// </summary>
    private Process? process;

    /// <summary>
    /// <para>Gets or defines the stderr value used by WorkerClient.</para>
    /// <para>Возвращает или определяет значение stderr, используемое WorkerClient.</para>
    /// </summary>
    private Task? stderr;

    /// <summary><para>Continuously drains stdout into a bounded protocol mailbox.</para><para>Непрерывно вычитывает stdout в ограниченную очередь протокола.</para></summary>
    private Task? stdout;
    /// <summary><para>Owns cancellation of the current process pipe reader.</para><para>Владеет отменой чтения каналов текущего процесса.</para></summary>
    private CancellationTokenSource? pipes;
    /// <summary><para>Holds at most four parsed metadata responses.</para><para>Хранит не более четырёх разобранных ответов с метаданными.</para></summary>
    private Channel<WorkerMessage>? messages;

    /// <summary><para>Owns all runtime, cache and engine temporary files for the current worker incarnation.</para><para>Владеет временными файлами runtime, кэшей и движков текущего экземпляра worker.</para></summary>
    private string? temporaryDirectory;

    /// <summary>
    /// <para>Gets or defines the failures value used by WorkerClient.</para>
    /// <para>Возвращает или определяет значение failures, используемое WorkerClient.</para>
    /// </summary>
    private int failures;

    /// <summary>
    /// <para>Gets or defines the Jobs value used by WorkerClient.</para>
    /// <para>Возвращает или определяет значение Jobs, используемое WorkerClient.</para>
    /// </summary>
    public int Jobs
    {
        get; private set;
    }

    /// <summary>
    /// <para>Gets or defines the TemplateAffinity value used by WorkerClient.</para>
    /// <para>Возвращает или определяет значение TemplateAffinity, используемое WorkerClient.</para>
    /// </summary>
    public string? TemplateAffinity
    {
        get; set;
    }

    /// <summary>
    /// <para>Gets or defines the Busy value used by WorkerClient.</para>
    /// <para>Возвращает или определяет значение Busy, используемое WorkerClient.</para>
    /// </summary>
    public bool Busy
    {
        get; set;
    }

    /// <summary>
    /// <para>Gets or defines the Alive value used by WorkerClient.</para>
    /// <para>Возвращает или определяет значение Alive, используемое WorkerClient.</para>
    /// </summary>
    public bool Alive
    {
        get
        {
            Process? current = Volatile.Read(ref process);
            try
            {
                return current is not null && !current.HasExited;
            }
            catch (InvalidOperationException) { return false; }
        }
    }

    /// <summary>
    /// <para>Gets or defines the ProcessId value used by WorkerClient.</para>
    /// <para>Возвращает или определяет значение ProcessId, используемое WorkerClient.</para>
    /// </summary>
    public int? ProcessId
    {
        get
        {
            Process? current = Volatile.Read(ref process);
            try
            {
                return current is not null && !current.HasExited ? current.Id : null;
            }
            catch (InvalidOperationException) { return null; }
        }
    }


    /// <summary>
    /// <para>Executes StartAsync as part of the WorkerClient conversion workflow.</para>
    /// <para>Выполняет StartAsync в цепочке преобразования WorkerClient.</para>
    /// </summary>
    /// <param name="token"><para>Cancellation token for this operation; active worker cancellation terminates the process before its slot is reused.</para><para>Токен отмены операции; отмена активного worker завершает процесс до повторного использования слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public async Task<bool> StartAsync(CancellationToken token)
    {
        if (Alive)
        {
            return false;
        }
        await StopAsync().ConfigureAwait(false);
        if (failures >= options.MaxStartupFailures)
        {
            throw new PdfWorkerException(WorkerFailureKind.Startup, "Worker startup circuit is open.");
        }
        if (failures != 0)
        {
            await Task.Delay(options.StartupFailureBackoff, token).ConfigureAwait(false);
        }
        string assembly = typeof(WorkerClient).Assembly.Location;
        string runtimeConfig = Path.ChangeExtension(assembly, ".runtimeconfig.json");
        if (assembly.Length == 0 || !File.Exists(runtimeConfig))
        {
            failures++;
            throw new PdfWorkerException(WorkerFailureKind.Startup, "Worker runtimeconfig is missing; single-file/trimming is unsupported.");
        }
        string config = Path.Combine(root, "worker-" + Guid.NewGuid().ToString("N") + ".json");
        var start = new ProcessStartInfo(options.DotnetHostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = root,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false, true)
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(runtimeConfig);
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add("--pdf-worker");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add(config);
        temporaryDirectory = Path.Combine(root, "worker-runtime-" + Guid.NewGuid().ToString("N"));
        start.Environment["TEMP"] = temporaryDirectory;
        start.Environment["TMP"] = temporaryDirectory;
        start.Environment["TMPDIR"] = temporaryDirectory;
        process = new Process { StartInfo = start };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(options.StartupTimeout);
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            await File.WriteAllTextAsync(config, JsonSerializer.Serialize(settings), token).ConfigureAwait(false);
            if (!process.Start())
            {
                throw new IOException("Cannot start worker host.");
            }
            stderr = DrainAsync(process.StandardError);
            pipes = new CancellationTokenSource();
            messages = Channel.CreateBounded<WorkerMessage>(new BoundedChannelOptions(4) { SingleReader = true, SingleWriter = true });
            stdout = PumpAsync(process.StandardOutput, messages.Writer, pipes.Token);
            WorkerMessage? ready = await ReceiveAsync(timeout.Token).ConfigureAwait(false);
            if (ready?.Kind != "ready" || ready.Engine != WorkerProtocol.Engine)
            {
                throw new InvalidDataException("Worker handshake is incompatible.");
            }
            Jobs = 0;
            failures = 0;
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { await StopAsync().ConfigureAwait(false); throw; }
        catch (Exception ex) { failures++; await StopAsync().ConfigureAwait(false); throw new PdfWorkerException(WorkerFailureKind.Startup, "Worker did not complete startup handshake.", ex); }
        finally { if (File.Exists(config)) { File.Delete(config); } }
    }


    /// <summary>
    /// <para>Executes ExecuteAsync as part of the WorkerClient conversion workflow.</para>
    /// <para>Выполняет ExecuteAsync в цепочке преобразования WorkerClient.</para>
    /// </summary>
    /// <param name="job"><para>The job argument.</para><para>Аргумент job.</para></param>
    /// <param name="token"><para>Cancellation token for this operation; active worker cancellation terminates the process before its slot is reused.</para><para>Токен отмены операции; отмена активного worker завершает процесс до повторного использования слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    public async Task<WorkerMessage> ExecuteAsync(WorkerJob job, CancellationToken token)
    {
        if (!Alive)
        {
            throw new PdfWorkerException(WorkerFailureKind.Crash, "Worker exited before the job.");
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(job.Settings.Timeout ?? options.JobTimeout);
        using var monitoring = new CancellationTokenSource();
        Task? monitor = null;
        Task<WorkerMessage?>? response = null;
        try
        {
            await WorkerProtocol.WriteAsync(process!.StandardInput, new("ping"), timeout.Token).ConfigureAwait(false);
            WorkerMessage? pong = await ReceiveAsync(timeout.Token).ConfigureAwait(false);
            if (pong?.Kind != "pong")
            {
                throw new PdfWorkerException(WorkerFailureKind.Protocol, "Worker health check failed.");
            }
            await WorkerProtocol.WriteAsync(process.StandardInput, new("job", Job: job), timeout.Token).ConfigureAwait(false);
            response = ReceiveAsync(timeout.Token);
            monitor = MonitorAsync(monitoring.Token);
            Task completed = await Task.WhenAny(response, monitor).ConfigureAwait(false);
            if (completed == monitor)
            {
                await monitor.ConfigureAwait(false);
            }
            WorkerMessage? result = await response.ConfigureAwait(false);
            if (result is null)
            {
                throw new PdfWorkerException(WorkerFailureKind.Crash, "Worker exited without a result.");
            }
            if (result.Kind is not ("result" or "error"))
            {
                throw new PdfWorkerException(WorkerFailureKind.Protocol, "Unexpected worker response.");
            }
            Jobs++;
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { await StopAsync().ConfigureAwait(false); throw; }
        catch (OperationCanceledException ex) { await StopAsync().ConfigureAwait(false); throw new PdfWorkerException(WorkerFailureKind.Timeout, "Worker execution timed out.", ex); }
        catch (PdfWorkerException) { await StopAsync().ConfigureAwait(false); throw; }
        catch (Exception ex) { await StopAsync().ConfigureAwait(false); throw new PdfWorkerException(WorkerFailureKind.Protocol, "Invalid or truncated worker response.", ex); }
        finally
        {
            monitoring.Cancel();
            timeout.Cancel();
            if (monitor is not null)
            {
                try
                {
                    await monitor.ConfigureAwait(false);
                }
                catch { }
            }
            if (response is not null)
            {
                try
                {
                    await response.ConfigureAwait(false);
                }
                catch { }
            }
        }
    }


    /// <summary>
    /// <para>Executes MonitorAsync as part of the WorkerClient conversion workflow.</para>
    /// <para>Выполняет MonitorAsync в цепочке преобразования WorkerClient.</para>
    /// </summary>
    /// <param name="token"><para>Cancellation token for this operation; active worker cancellation terminates the process before its slot is reused.</para><para>Токен отмены операции; отмена активного worker завершает процесс до повторного использования слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    private async Task MonitorAsync(CancellationToken token)
    {
        while (true)
        {
            await Task.Delay(100, token).ConfigureAwait(false);
            if (!Alive)
            {
                throw new PdfWorkerException(WorkerFailureKind.Crash, "Worker process exited.");
            }
            process!.Refresh();
            if (options.MaxWorkingSetBytes > 0 && process.WorkingSet64 > options.MaxWorkingSetBytes)
            {
                throw new PdfWorkerException(WorkerFailureKind.MemoryLimit, "Observed worker working set exceeded its configured threshold.");
            }
        }
    }

    /// <summary>
    /// <para>Executes DrainAsync as part of the WorkerClient conversion workflow.</para>
    /// <para>Выполняет DrainAsync в цепочке преобразования WorkerClient.</para>
    /// </summary>
    /// <param name="reader"><para>The reader argument.</para><para>Аргумент reader.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static async Task DrainAsync(TextReader reader)
    {
        char[] buffer = new char[8192];
        while (await reader.ReadAsync(buffer).ConfigureAwait(false) != 0)
        {
        }
    }

    /// <summary><para>Reads every stdout frame and fails on corruption or mailbox overflow.</para><para>Читает каждый кадр stdout и отклоняет повреждение или переполнение очереди.</para></summary>
    /// <param name="reader"><para>Redirected worker stdout.</para><para>Перенаправленный stdout worker.</para></param>
    /// <param name="writer"><para>Bounded response mailbox.</para><para>Ограниченная очередь ответов.</para></param>
    /// <param name="token"><para>Pipe lifetime cancellation.</para><para>Отмена времени жизни канала.</para></param>
    /// <returns><para>Completion of pipe drainage.</para><para>Завершение вычитывания канала.</para></returns>
    private static async Task PumpAsync(TextReader reader, ChannelWriter<WorkerMessage> writer, CancellationToken token)
    {
        Exception? failure = null;
        try
        {
            while (await WorkerProtocol.ReadAsync(reader, token).ConfigureAwait(false) is { } message)
            {
                if (!writer.TryWrite(message))
                {
                    throw new InvalidDataException("Worker response mailbox overflow.");
                }
            }
        }
        catch (Exception ex) { failure = ex; }
        finally { writer.TryComplete(failure); }
    }

    /// <summary><para>Receives one metadata frame, or null after a clean pipe exit.</para><para>Получает один кадр метаданных либо null после корректного закрытия канала.</para></summary>
    /// <param name="token"><para>Timeout or caller cancellation.</para><para>Тайм-аут или отмена вызывающего кода.</para></param>
    /// <returns><para>The next frame or end of stream.</para><para>Следующий кадр либо конец потока.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The read was canceled.</para><para>Чтение отменено.</para></exception>
    private async Task<WorkerMessage?> ReceiveAsync(CancellationToken token)
    {
        try
        {
            return await messages!.Reader.ReadAsync(token).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex) when (ex.InnerException is null) { return null; }
    }

    /// <summary>
    /// <para>Executes StopAsync as part of the WorkerClient conversion workflow.</para>
    /// <para>Выполняет StopAsync в цепочке преобразования WorkerClient.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public async Task StopAsync()
    {
        Process? current = process;
        process = null;
        if (current is null)
        {
            return;
        }
        try
        {
            try
            {
                _ = current.Id;
            }
            catch (InvalidOperationException) { return; }
            if (!current.HasExited)
            {
                using var grace = new CancellationTokenSource(options.StopGracePeriod);
                try
                {
                    await WorkerProtocol.WriteAsync(current.StandardInput, new("stop"), grace.Token).ConfigureAwait(false);
                    await current.WaitForExitAsync(grace.Token).ConfigureAwait(false);
                }
                catch { if (!current.HasExited) { current.Kill(entireProcessTree: true); } }
            }
            await current.WaitForExitAsync().ConfigureAwait(false);
            if (stderr is not null)
            {
                await stderr.ConfigureAwait(false);
            }
        }
        finally
        {
            pipes?.Cancel();
            if (stdout is not null)
            {
                await stdout.ConfigureAwait(false);
            }
            pipes?.Dispose();
            pipes = null;
            stdout = null;
            messages = null;
            current.Dispose();
            stderr = null;
            if (temporaryDirectory is not null && Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
            temporaryDirectory = null;
        }
    }

    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    public ValueTask DisposeAsync() => new(StopAsync());
}
