using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PdfGenerator;
using PdfGenerator.Demo;

try
{
    string Value(string flag, string? fallback = null)
    {
        int index = Array.IndexOf(args, flag);
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }

        return fallback ?? throw new ArgumentException($"Required argument: {flag}");
    }
    if (args.Contains("--diagnose-xslt"))
    {
        XsltDiagnosticReport report = new XsltDiagnostics().AnalyzeFile(Value("--diagnose-xslt"));
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
        return report.HasErrors ? 3 : 0;
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cancellation.Cancel();
    };
    using var transformer = new SaxonFoTransformer();
    IFoPdfRenderer renderer = args.Contains("--fop")
        ? new FopProcessRenderer(new FopOptions { ExecutablePath = Value("--fop"), ConfigurationPath = args.Contains("--config") ? Value("--config") : null })
        : new EmbeddedFopRenderer(new EmbeddedFopOptions { ConfigurationPath = args.Contains("--config") ? Value("--config") : null });
    using var rendererLifetime = (IDisposable)renderer;
    bool pdf = args.Contains("--pdf");
    if (args.Contains("--self-test"))
    {
        await DemoSelfTest.RunAsync(transformer, renderer, Value("--data", "/app/test-data"), Value("--output", "/tmp/xmlfo-self-test"), pdf, args.Contains("--verify-tools"));
    }
    else
    {
        var watch = Stopwatch.StartNew();
        string target = Value("--out");
        if (pdf)
        {
            await new XmlPdfGenerator(transformer, renderer).GenerateFileAsync(Value("--xml"), Value("--xslt"), target, cancellation.Token);
        }
        else
        {
            await transformer.TransformFileAsync(Value("--xml"), Value("--xslt"), target, cancellation.Token);
        }

        Console.WriteLine($"Created {Path.GetFullPath(target)} in {watch.Elapsed.TotalMilliseconds:F0} ms");
    }
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex is ConversionException conversion ? $"Conversion failed: {conversion.Stage}" : ex.Message);
    return 1;
}
