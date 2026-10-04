using System.Diagnostics;

namespace PdfGenerator;


/// <summary>
/// <para>Provides the FopProcessRenderer contract or implementation for PDF conversion.</para>
/// <para>Предоставляет контракт или реализацию FopProcessRenderer для преобразования PDF.</para>
/// </summary>
public sealed class FopProcessRenderer : IFoPdfRenderer, IDisposable
{

    /// <summary>
    /// <para>Gets or defines the options value used by FopProcessRenderer.</para>
    /// <para>Возвращает или определяет значение options, используемое FopProcessRenderer.</para>
    /// </summary>
    private readonly FopOptions options;

    /// <summary>
    /// <para>Gets or defines the slots value used by FopProcessRenderer.</para>
    /// <para>Возвращает или определяет значение slots, используемое FopProcessRenderer.</para>
    /// </summary>
    private readonly SemaphoreSlim slots;


    /// <summary>
    /// <para>Initializes FopProcessRenderer with an immutable snapshot of service settings.</para>
    /// <para>Создаёт FopProcessRenderer со снимком настроек сервиса.</para>
    /// </summary>
    /// <param name="options"><para>Immutable configuration snapshot for this service or job.</para><para>Неизменяемый снимок конфигурации сервиса или задания.</para></param>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public FopProcessRenderer(FopOptions? options = null)
    {
        this.options = options is null ? new() : options with
        {
            PrefixArguments = Array.AsReadOnly(options.PrefixArguments.ToArray())
        };
        if (this.options.Timeout <= TimeSpan.Zero || this.options.MaxConcurrency <= 0 || this.options.MaxPdfBytes <= 0 || this.options.MaxFoBytes <= 0 || this.options.MaxImageBytes <= 0 || this.options.MaxImageBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        slots = new(this.options.MaxConcurrency);
    }


    /// <summary>
    /// <para>Prepares local image snapshots, renders a complete PDF and checks cancellation before atomic file publication.</para>
    /// <para>Подготавливает локальные снимки изображений, рендерит готовый PDF и проверяет отмену перед атомарной публикацией файла.</para>
    /// </summary>
    /// <param name="foPath"><para>The foPath argument.</para><para>Аргумент foPath.</para></param>
    /// <param name="pdfPath"><para>PDF destination, atomically replaced only after successful rendering.</para><para>Путь PDF, атомарно заменяемый после успешного рендеринга.</para></param>
    /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public async Task RenderFileAsync(string foPath, string pdfPath, CancellationToken cancellationToken = default)
    {
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        ConversionStage stage = ConversionStage.Render;
        try
        {
            temporary = AtomicOutput.Prepare(pdfPath, foPath);
            if (!File.Exists(foPath))
            {
                throw new FileNotFoundException("FO input not found.", foPath);
            }

            using FoImagePreparation images = await FoImagePreparation.CreateAsync(foPath, Path.GetDirectoryName(temporary)!, options, cancellationToken).ConfigureAwait(false);
            var start = new ProcessStartInfo(options.ExecutablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (string argument in options.PrefixArguments)
            {
                start.ArgumentList.Add(argument);
            }

            if (options.ConfigurationPath is not null)
            {
                start.ArgumentList.Add("-c");
                start.ArgumentList.Add(Path.GetFullPath(options.ConfigurationPath));
            }
            start.ArgumentList.Add("-fo");
            start.ArgumentList.Add(images.FoPath);
            start.ArgumentList.Add("-pdf");
            start.ArgumentList.Add(temporary);
            using Process process = Process.Start(start) ?? throw new IOException("Cannot start Apache FOP.");
            // Drain both pipes without retaining document contents in logs or memory.
            Task stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
            Task stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
            using var timeout = new CancellationTokenSource(options.Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Apache FOP exceeded the configured timeout.");
            }
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new IOException($"Apache FOP exited with code {process.ExitCode}.");
            }

            long size = new FileInfo(temporary).Length;
            if (size < 5 || size > options.MaxPdfBytes)
            {
                throw new IOException("Invalid PDF size.");
            }

            await using (FileStream stream = File.OpenRead(temporary))
            {
                byte[] header = new byte[5];
                await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                if (!header.AsSpan().SequenceEqual("%PDF-"u8))
                {
                    throw new IOException("Apache FOP did not produce a PDF.");
                }
            }
            stage = ConversionStage.Publish;
            images.Dispose();
            AtomicOutput.Publish(temporary, pdfPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConversionException(stage, $"FO → PDF failed during {stage}.", ex);
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            slots.Release();
        }
    }


    /// <summary>
    /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
    /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
    /// </summary>
    public void Dispose() => slots.Dispose();
}
