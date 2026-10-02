using System.Diagnostics;

namespace Company.XmlFo;

/// <summary>Runs an externally installed Apache FOP. Only trusted FO and FOP configuration are supported.</summary>
public sealed class FopProcessRenderer : IFoPdfRenderer, IDisposable
{
    private readonly FopOptions options;
    private readonly SemaphoreSlim slots;

    public FopProcessRenderer(FopOptions? options = null)
    {
        this.options = options ?? new();
        if (this.options.Timeout <= TimeSpan.Zero || this.options.MaxConcurrency <= 0 || this.options.MaxPdfBytes <= 0 || this.options.MaxFoBytes <= 0 || this.options.MaxImageBytes <= 0 || this.options.MaxImageBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        slots = new(this.options.MaxConcurrency);
    }

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

    public void Dispose() => slots.Dispose();
}
