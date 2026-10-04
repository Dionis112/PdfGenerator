using System.Xml.Linq;
using Xunit;

namespace PdfGenerator.IntegrationTests;


/// <summary>
/// <para>Runs the ConversionTests validation scenario.</para>
/// <para>Выполняет проверку сценария ConversionTests.</para>
/// </summary>
public sealed class ConversionTests : IDisposable
{

    /// <summary>
    /// <para>Runs the directory validation scenario.</para>
    /// <para>Выполняет проверку сценария directory.</para>
    /// </summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "xmlfo-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// <para>Runs the Fixture validation scenario.</para>
    /// <para>Выполняет проверку сценария Fixture.</para>
    /// </summary>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "test-data/basic", name);

    /// <summary>
    /// <para>Runs the Output validation scenario.</para>
    /// <para>Выполняет проверку сценария Output.</para>
    /// </summary>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private string Output(string name = "result.fo")
    {
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name);
    }


    /// <summary>
    /// <para>Runs the Xslt2IncludesAndCyrillicMatchExpected validation scenario.</para>
    /// <para>Выполняет проверку сценария Xslt2IncludesAndCyrillicMatchExpected.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task Xslt2IncludesAndCyrillicMatchExpected()
    {
        using var transformer = new SaxonFoTransformer();
        string target = Output();
        await transformer.TransformFileAsync(Fixture("input.xml"), Fixture("document.xsl"), target);
        Assert.True(XNode.DeepEquals(XDocument.Load(Fixture("expected.fo")).Root, XDocument.Load(target).Root));
    }


    /// <summary>
    /// <para>Runs the EmptyDataProducesTitle validation scenario.</para>
    /// <para>Выполняет проверку сценария EmptyDataProducesTitle.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task EmptyDataProducesTitle()
    {
        using var transformer = new SaxonFoTransformer();
        string target = Output();
        await transformer.TransformFileAsync(Fixture("empty.xml"), Fixture("document.xsl"), target);
        Assert.Contains("Пустой документ", File.ReadAllText(target));
        Assert.DoesNotContain("Категория", File.ReadAllText(target));
    }


    /// <summary>
    /// <para>Runs the FailedInputPreservesPreviousFile validation scenario.</para>
    /// <para>Выполняет проверку сценария FailedInputPreservesPreviousFile.</para>
    /// </summary>
    /// <param name="fixture"><para>The fixture argument.</para><para>Аргумент fixture.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Theory]
    [InlineData("invalid.xml")]
    [InlineData("missing.xml")]
    public async Task FailedInputPreservesPreviousFile(string fixture)
    {
        using var transformer = new SaxonFoTransformer();
        string target = Output();
        File.WriteAllText(target, "previous");
        await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformFileAsync(Fixture(fixture), Fixture("document.xsl"), target));
        Assert.Equal("previous", File.ReadAllText(target));
        Assert.Single(Directory.GetFiles(directory));
    }


    /// <summary>
    /// <para>Runs the ParallelJobsDoNotMixInputs validation scenario.</para>
    /// <para>Выполняет проверку сценария ParallelJobsDoNotMixInputs.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task ParallelJobsDoNotMixInputs()
    {
        using var transformer = new SaxonFoTransformer();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async i =>
        {
            string target = Output($"{i}.fo");
            await transformer.TransformFileAsync(Fixture(i % 2 == 0 ? "input.xml" : "empty.xml"), Fixture("document.xsl"), target);
            Assert.Equal(i % 2 == 0, File.ReadAllText(target).Contains("Категория"));
        }));
    }


    /// <summary>
    /// <para>Runs the CancellationBeforeStartDoesNotPublish validation scenario.</para>
    /// <para>Выполняет проверку сценария CancellationBeforeStartDoesNotPublish.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task CancellationBeforeStartDoesNotPublish()
    {
        using var transformer = new SaxonFoTransformer();
        string target = Output();
        File.WriteAllText(target, "previous");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transformer.TransformFileAsync(Fixture("input.xml"), Fixture("document.xsl"), target, new CancellationToken(true)));
        Assert.Equal("previous", File.ReadAllText(target));
    }


    /// <summary>
    /// <para>Runs the DtdAndEmptyDocumentRejected validation scenario.</para>
    /// <para>Выполняет проверку сценария DtdAndEmptyDocumentRejected.</para>
    /// </summary>
    /// <param name="xml"><para>The xml argument.</para><para>Аргумент xml.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Theory]
    [InlineData("<!DOCTYPE document [<!ENTITY a SYSTEM 'file:///etc/passwd'>]><document>&a;</document>")]
    [InlineData("")]
    public async Task DtdAndEmptyDocumentRejected(string xml)
    {
        using var transformer = new SaxonFoTransformer();
        string source = Output("input.xml");
        File.WriteAllText(source, xml);
        await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformFileAsync(source, Fixture("document.xsl"), Output()));
        Assert.False(File.Exists(Output()));
    }


    /// <summary>
    /// <para>Runs the RuntimeResourcesAndSecondaryOutputsRejected validation scenario.</para>
    /// <para>Выполняет проверку сценария RuntimeResourcesAndSecondaryOutputsRejected.</para>
    /// </summary>
    /// <param name="body"><para>The body argument.</para><para>Аргумент body.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Theory]
    [InlineData("<xsl:value-of select=\"doc('https://example.com/private.xml')\"/>")]
    [InlineData("<xsl:value-of select=\"unparsed-text('file:///etc/passwd')\"/>")]
    [InlineData("<xsl:value-of select=\"collection('file:///tmp')\"/>")]
    [InlineData("<xsl:value-of select=\"uri-collection('file:///tmp')\"/>")]
    [InlineData("<xsl:result-document href=\"escape.xml\"><secret/></xsl:result-document>")]
    public async Task RuntimeResourcesAndSecondaryOutputsRejected(string body)
    {
        using var transformer = new SaxonFoTransformer();
        string xslt = Output("document.xsl");
        File.WriteAllText(xslt, $"<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:template match='/'><fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format'>{body}</fo:root></xsl:template></xsl:stylesheet>");
        await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformFileAsync(Fixture("input.xml"), xslt, Output()));
        Assert.False(File.Exists(Output("escape.xml")));
    }


    /// <summary>
    /// <para>Runs the UnsafeIncludesRejected validation scenario.</para>
    /// <para>Выполняет проверку сценария UnsafeIncludesRejected.</para>
    /// </summary>
    /// <param name="href"><para>The href argument.</para><para>Аргумент href.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Theory]
    [InlineData("https://example.com/module.xsl")]
    [InlineData("../outside.xsl")]
    public async Task UnsafeIncludesRejected(string href)
    {
        using var transformer = new SaxonFoTransformer();
        string xslt = Output("document.xsl");
        File.WriteAllText(xslt, $"<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:include href='{href}'/></xsl:stylesheet>");
        await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformFileAsync(Fixture("input.xml"), xslt, Output()));
    }


    /// <summary>
    /// <para>Runs the BadStylesheetRejected validation scenario.</para>
    /// <para>Выполняет проверку сценария BadStylesheetRejected.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task BadStylesheetRejected()
    {
        using var transformer = new SaxonFoTransformer();
        string xslt = Output("bad.xsl");
        File.WriteAllText(xslt, "<broken>");
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformFileAsync(Fixture("input.xml"), xslt, Output()));
        Assert.Equal(ConversionStage.Compile, error.Stage);
    }


    /// <summary>
    /// <para>Runs the MissingOutputDirectoryRejected validation scenario.</para>
    /// <para>Выполняет проверку сценария MissingOutputDirectoryRejected.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task MissingOutputDirectoryRejected()
    {
        using var transformer = new SaxonFoTransformer();
        await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformFileAsync(Fixture("input.xml"), Fixture("document.xsl"), Path.Combine(Output("missing"), "out.fo")));
    }


    /// <summary>
    /// <para>Runs the OutputAndInputLimitsPreservePreviousFile validation scenario.</para>
    /// <para>Выполняет проверку сценария OutputAndInputLimitsPreservePreviousFile.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task OutputAndInputLimitsPreservePreviousFile()
    {
        string target = Output();
        File.WriteAllText(target, "previous");
        using var outputLimited = new SaxonFoTransformer(new()
        {
            MaxOutputBytes = 10
        });
        await Assert.ThrowsAsync<ConversionException>(() => outputLimited.TransformFileAsync(Fixture("input.xml"), Fixture("document.xsl"), target));
        using var inputLimited = new SaxonFoTransformer(new()
        {
            MaxInputBytes = 10
        });
        await Assert.ThrowsAsync<ConversionException>(() => inputLimited.TransformFileAsync(Fixture("input.xml"), Fixture("document.xsl"), target));
        Assert.Equal("previous", File.ReadAllText(target));
    }


    /// <summary>
    /// <para>Runs the RendererFailurePreservesPdfAndCleansIntermediateFo validation scenario.</para>
    /// <para>Выполняет проверку сценария RendererFailurePreservesPdfAndCleansIntermediateFo.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task RendererFailurePreservesPdfAndCleansIntermediateFo()
    {
        using var transformer = new SaxonFoTransformer();
        using var renderer = new FopProcessRenderer(new()
        {
            ExecutablePath = Path.Combine(directory, "missing-fop")
        });
        string target = Output("result.pdf");
        File.WriteAllText(target, "previous");
        await Assert.ThrowsAsync<ConversionException>(() => new XmlPdfGenerator(transformer, renderer).GenerateFileAsync(Fixture("input.xml"), Fixture("document.xsl"), target));
        Assert.Equal("previous", File.ReadAllText(target));
        Assert.Single(Directory.GetFiles(directory));
    }


    /// <summary>
    /// <para>Runs the ChangedIncludeUsedOnNextCall validation scenario.</para>
    /// <para>Выполняет проверку сценария ChangedIncludeUsedOnNextCall.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task ChangedIncludeUsedOnNextCall()
    {
        using var transformer = new SaxonFoTransformer();
        string xslt = Output("document.xsl");
        string module = Output("module.xsl");
        File.WriteAllText(xslt, "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:include href='module.xsl'/><xsl:template match='/'><fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format'><xsl:call-template name='content'/></fo:root></xsl:template></xsl:stylesheet>");
        foreach (string? text in new[] { "first", "second" })
        {
            File.WriteAllText(module, $"<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:template name='content'>{text}</xsl:template></xsl:stylesheet>");
            await transformer.TransformFileAsync(Fixture("input.xml"), xslt, Output());
            Assert.Equal(text, XDocument.Load(Output()).Root!.Value);
        }
    }


    /// <summary>
    /// <para>Runs the FopTimeoutAndCancellationPreserveOutput validation scenario.</para>
    /// <para>Выполняет проверку сценария FopTimeoutAndCancellationPreserveOutput.</para>
    /// </summary>
    /// <param name="cancel"><para>The cancel argument.</para><para>Аргумент cancel.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FopTimeoutAndCancellationPreserveOutput(bool cancel)
    {
        // A real child process exercises pipe draining and kill-tree without installing FOP on Windows.
        bool windows = OperatingSystem.IsWindows();
        using var renderer = new FopProcessRenderer(new()
        {
            ExecutablePath = windows ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe") : "/bin/sh",
            PrefixArguments = windows ? new[] { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30; exit 7 #" } : ["-c", "sleep 30; exit 7"],
            Timeout = cancel ? TimeSpan.FromSeconds(10) : TimeSpan.FromMilliseconds(500)
        });
        string target = Output("result.pdf");
        File.WriteAllText(target, "previous");
        using var token = new CancellationTokenSource();
        if (cancel)
        {
            token.CancelAfter(500);
        }

        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderer.RenderFileAsync(Fixture("expected.fo"), target, token.Token));
        }
        else
        {
            ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => renderer.RenderFileAsync(Fixture("expected.fo"), target));
            Assert.IsType<TimeoutException>(error.InnerException);
        }
        Assert.Equal("previous", File.ReadAllText(target));
        Assert.Single(Directory.GetFiles(directory));
    }


    /// <summary>
    /// <para>Runs the DeterministicLargeInputTransforms validation scenario.</para>
    /// <para>Выполняет проверку сценария DeterministicLargeInputTransforms.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task DeterministicLargeInputTransforms()
    {
        using var transformer = new SaxonFoTransformer();
        string source = Output("large.xml");
        new XDocument(new XElement("document", new XElement("title", "Большой документ"),
            new XElement("items", Enumerable.Range(0, 2000).Select(i => new XElement("item", new XAttribute("category", i % 10), new XAttribute("amount", "1.25"), $"Товар {i}"))))).Save(source);
        await transformer.TransformFileAsync(source, Fixture("document.xsl"), Output());
        var result = XDocument.Load(Output());
        Assert.Equal(11, result.Descendants(XName.Get("block", "http://www.w3.org/1999/XSL/Format")).Count());
        Assert.Contains("сумма 250.00", result.Root!.Value);
    }


    /// <summary>
    /// <para>Runs the Dispose validation scenario.</para>
    /// <para>Выполняет проверку сценария Dispose.</para>
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
