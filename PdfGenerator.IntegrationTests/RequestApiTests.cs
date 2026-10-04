using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PdfGenerator.IntegrationTests;


/// <summary>
/// <para>Runs the RequestApiTests validation scenario.</para>
/// <para>Выполняет проверку сценария RequestApiTests.</para>
/// </summary>
public sealed class RequestApiTests : IDisposable
{

    /// <summary>
    /// <para>Runs the directory validation scenario.</para>
    /// <para>Выполняет проверку сценария directory.</para>
    /// </summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pdf-request-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// <para>Runs the Template validation scenario.</para>
    /// <para>Выполняет проверку сценария Template.</para>
    /// </summary>
    private const string Template = """
        <xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:p="urn:parameters" version="2.0">
        <xsl:param name="p:text" as="xs:string" select="'default'"/>
        <xsl:param name="flag" as="xs:boolean" select="false()"/>
        <xsl:param name="number" as="xs:integer" select="0"/>
        <xsl:param name="amount" as="xs:decimal" select="0"/>
        <xsl:param name="when" as="xs:dateTime" select="xs:dateTime('2026-01-01T00:00:00Z')"/>
        <xsl:template match="/">
        <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
        <fo:layout-master-set><fo:simple-page-master master-name="a4"><fo:region-body/></fo:simple-page-master></fo:layout-master-set>
        <fo:page-sequence master-reference="a4"><fo:flow flow-name="xsl-region-body"><fo:block>
        <xsl:value-of select="string-join(($p:text, string($flag), string($number), string($amount), string($when)), '|')"/>
        </fo:block></fo:flow></fo:page-sequence></fo:root>
        </xsl:template></xsl:stylesheet>
        """;


    /// <summary>
    /// <para>Runs the RequestApiTests validation scenario.</para>
    /// <para>Выполняет проверку сценария RequestApiTests.</para>
    /// </summary>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public RequestApiTests() => Directory.CreateDirectory(directory);

    /// <summary>
    /// <para>Runs the Output validation scenario.</para>
    /// <para>Выполняет проверку сценария Output.</para>
    /// </summary>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private string Output(string name) => Path.Combine(directory, name);

    /// <summary>
    /// <para>Runs the Text validation scenario.</para>
    /// <para>Выполняет проверку сценария Text.</para>
    /// </summary>
    /// <param name="text"><para>The text argument.</para><para>Аргумент text.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static MemoryStream Text(string text) => new(Encoding.UTF8.GetBytes(text));


    /// <summary>
    /// <para>Runs the ParallelTypedParametersAreIsolatedAndUnknownLengthInputsSpill validation scenario.</para>
    /// <para>Выполняет проверку сценария ParallelTypedParametersAreIsolatedAndUnknownLengthInputsSpill.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task ParallelTypedParametersAreIsolatedAndUnknownLengthInputsSpill()
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            MemoryBufferThresholdBytes = 1
        });
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async i =>
        {
            using var xml = new NonSeekableStream(Text("<root/>"));
            using var xslt = new NonSeekableStream(Text(Template));
            var request = new ConversionRequest(XmlSource.FromStream(xml), XmlSource.FromStream(xslt),
                [new("text", $"job-{i}", "urn:parameters"), new("flag", true), new("number", ulong.MaxValue), new("amount", 1.25m),
                    new("when", new DateTimeOffset(2026, 10, 2, 12, 30, 0, TimeSpan.FromHours(5)))]);
            string output = Output($"{i}.fo");
            await transformer.TransformAsync(request, output);
            Assert.Contains($"job-{i}|true|18446744073709551615|1.25|2026-10-02T12:30:00+05:00", XDocument.Load(output).Root!.Value);
            Assert.True(xml.CanRead && xslt.CanRead);
        }));
    }


    /// <summary>
    /// <para>Runs the AbsentAndEmptyParametersDiffer validation scenario.</para>
    /// <para>Выполняет проверку сценария AbsentAndEmptyParametersDiffer.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task AbsentAndEmptyParametersDiffer()
    {
        using var transformer = new SaxonFoTransformer();
        foreach (bool supplied in new[] { false, true })
        {
            using MemoryStream xml = Text("<root/>");
            using MemoryStream xslt = Text(Template);
            var request = new ConversionRequest(XmlSource.FromStream(xml), XmlSource.FromStream(xslt), supplied ? [new("text", "", "urn:parameters")] : null);
            await transformer.TransformAsync(request, Output("result.fo"));
            Assert.Equal(!supplied, XDocument.Load(Output("result.fo")).Root!.Value.Contains("default"));
        }
    }


    /// <summary>
    /// <para>Runs the StreamPdfReportsMetricsAndLeavesAllStreamsOpen validation scenario.</para>
    /// <para>Выполняет проверку сценария StreamPdfReportsMetricsAndLeavesAllStreamsOpen.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task StreamPdfReportsMetricsAndLeavesAllStreamsOpen()
    {
        using var transformer = new SaxonFoTransformer();
        using var renderer = new EmbeddedFopRenderer();
        var generator = new XmlPdfGenerator(transformer, renderer);
        using MemoryStream xml = Text("<root/>");
        using MemoryStream xslt = Text(Template);
        using var output = new MemoryStream();
        ConversionResult result = await generator.GenerateAsync(new(XmlSource.FromStream(xml), XmlSource.FromStream(xslt), jobId: "test"), output);
        Assert.Equal("test", result.JobId);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(output.Length, result.PdfBytes);
        Assert.True(result.TotalDuration >= result.RenderDuration);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(output.ToArray()));
        Assert.True(xml.CanRead && xslt.CanRead && output.CanWrite);
    }


    /// <summary>
    /// <para>Runs the StreamIncludesRequireBaseUriAndUseAllowedRoots validation scenario.</para>
    /// <para>Выполняет проверку сценария StreamIncludesRequireBaseUriAndUseAllowedRoots.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task StreamIncludesRequireBaseUriAndUseAllowedRoots()
    {
        File.WriteAllText(Output("module.xsl"), Template);
        const string include = "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'><xsl:include href='module.xsl'/></xsl:stylesheet>";
        using var transformer = new SaxonFoTransformer();
        foreach (bool hasBase in new[] { false, true })
        {
            using MemoryStream xml = Text("<root/>");
            using MemoryStream xslt = Text(include);
            var request = new ConversionRequest(XmlSource.FromStream(xml), XmlSource.FromStream(xslt, hasBase ? new Uri(Output("main.xsl")) : null));
            if (hasBase)
            {
                await transformer.TransformAsync(request, Output("result.fo"));
            }
            else
            {
                await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformAsync(request, Output("result.fo")));
            }
        }
    }


    /// <summary>
    /// <para>Runs the OversizedNonSeekableInputPreservesTarget validation scenario.</para>
    /// <para>Выполняет проверку сценария OversizedNonSeekableInputPreservesTarget.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task OversizedNonSeekableInputPreservesTarget()
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            MaxInputBytes = 8,
            MemoryBufferThresholdBytes = 1
        });
        using var xml = new NonSeekableStream(Text("<root>too long</root>"));
        using MemoryStream xslt = Text(Template);
        string target = Output("result.fo");
        File.WriteAllText(target, "previous");
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => transformer.TransformAsync(new(XmlSource.FromStream(xml), XmlSource.FromStream(xslt)), target));
        Assert.Equal(ConversionStage.Input, error.Stage);
        Assert.Equal("previous", File.ReadAllText(target));
        Assert.True(xml.CanRead);
    }


    /// <summary>
    /// <para>Runs the FailedOutputStreamIsPublishFailureAndRemainsOpen validation scenario.</para>
    /// <para>Выполняет проверку сценария FailedOutputStreamIsPublishFailureAndRemainsOpen.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task FailedOutputStreamIsPublishFailureAndRemainsOpen()
    {
        using var transformer = new SaxonFoTransformer();
        using var renderer = new EmbeddedFopRenderer();
        using MemoryStream xml = Text("<root/>");
        using MemoryStream xslt = Text(Template);
        using var output = new FailingWriteStream();
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => new XmlPdfGenerator(transformer, renderer)
            .GenerateAsync(new(XmlSource.FromStream(xml), XmlSource.FromStream(xslt)), output));
        Assert.Equal(ConversionStage.Publish, error.Stage);
        Assert.True(output.CanWrite);
    }


    /// <summary>
    /// <para>Runs the RejectsAmbiguousParametersAndUnavailableWorker validation scenario.</para>
    /// <para>Выполняет проверку сценария RejectsAmbiguousParametersAndUnavailableWorker.</para>
    /// </summary>
    [Fact]
    public void RejectsAmbiguousParametersAndUnavailableWorker()
    {
        Assert.Throws<ArgumentException>(() => new XsltParameter("date", DateTime.Now));
        Assert.Throws<ArgumentException>(() => new XsltParameter("number", double.NaN));
        Assert.Throws<ArgumentNullException>(() => new XsltParameter("null", null!));
        Assert.Throws<ArgumentException>(() => new ConversionRequest(XmlSource.FromFile("x.xml"), XmlSource.FromFile("x.xsl"), [new("x", 1), new("x", 2)]));
        Assert.NotEmpty(new ServiceCollection().AddXmlPdf(PdfExecutionMode.Worker));
        var policy = new LocalResourcePolicy([directory]);
        Assert.Throws<IOException>(() => policy.Resolve(new Uri("https://example.com/image.png")));
        Assert.Throws<IOException>(() => policy.Resolve(new Uri(Path.Combine(directory, "..", "outside.png"))));
    }


    /// <summary>
    /// <para>Runs the ProfileVersionIsStableAndChangesWithLimits validation scenario.</para>
    /// <para>Выполняет проверку сценария ProfileVersionIsStableAndChangesWithLimits.</para>
    /// </summary>
    [Fact]
    public void ProfileVersionIsStableAndChangesWithLimits()
    {
        using var first = PdfProcessingProfile.Create("normal");
        using var second = PdfProcessingProfile.Create("normal");
        using var changed = PdfProcessingProfile.Create("custom", xmlOptions: new()
        {
            MaxInputBytes = 1024
        });
        Assert.Equal(first.Version, second.Version);
        Assert.NotEqual(first.Version, changed.Version);
    }


    /// <summary>
    /// <para>Runs the ProfileKeepsFontSnapshotAfterSourceIsRemoved validation scenario.</para>
    /// <para>Выполняет проверку сценария ProfileKeepsFontSnapshotAfterSourceIsRemoved.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task ProfileKeepsFontSnapshotAfterSourceIsRemoved()
    {
        string font = Output("font.ttf");
        using (Stream resource = typeof(EmbeddedFopRenderer).Assembly.GetManifestResourceStream("PdfGenerator.Resources.Fonts.DejaVuSans.ttf")!)
        using (FileStream destination = File.Create(font))
        {
            resource.CopyTo(destination);
        }
        using var profile = PdfProcessingProfile.Create("custom-font", [new(font, "Report Sans")]);
        File.Delete(font);
        using var transformer = new SaxonFoTransformer(profile.XmlOptions);
        using var renderer = new EmbeddedFopRenderer(profile.RendererOptions);
        using MemoryStream xml = Text("<root/>");
        using MemoryStream xslt = Text(Template.Replace("<fo:block>", "<fo:block font-family='Report Sans'>"));
        using var output = new MemoryStream();
        ConversionResult result = await new XmlPdfGenerator(transformer, renderer).GenerateAsync(new(XmlSource.FromStream(xml), XmlSource.FromStream(xslt)), output);
        Assert.Equal(1, result.PageCount);
    }


    /// <summary>
    /// <para>Runs the InputReadFailureOrCancellationDoesNotTouchOutput validation scenario.</para>
    /// <para>Выполняет проверку сценария InputReadFailureOrCancellationDoesNotTouchOutput.</para>
    /// </summary>
    /// <param name="cancel"><para>The cancel argument.</para><para>Аргумент cancel.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InputReadFailureOrCancellationDoesNotTouchOutput(bool cancel)
    {
        using var transformer = new SaxonFoTransformer(new()
        {
            MemoryBufferThresholdBytes = 1
        });
        using var renderer = new EmbeddedFopRenderer();
        using var xml = new FailedReadStream(cancel);
        using MemoryStream xslt = Text(Template);
        using var output = new MemoryStream();
        output.WriteByte(42);
        Func<Task> action = () => new XmlPdfGenerator(transformer, renderer).GenerateAsync(new(XmlSource.FromStream(xml), XmlSource.FromStream(xslt)), output);
        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
        }
        else
        {
            Assert.Equal(ConversionStage.Input, (await Assert.ThrowsAsync<ConversionException>(action)).Stage);
        }
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        Assert.True(xml.CanRead && xslt.CanRead && output.CanWrite);
    }


    /// <summary>
    /// <para>Runs the NestedSvgExternalResourcesAreRejected validation scenario.</para>
    /// <para>Выполняет проверку сценария NestedSvgExternalResourcesAreRejected.</para>
    /// </summary>
    /// <param name="content"><para>The content argument.</para><para>Аргумент content.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Theory]
    [InlineData("<image href='https://example.com/private.png'/>")]
    [InlineData("<use href='https://example.com/private.svg#item'/>")]
    [InlineData("<style>@import 'https://example.com/style.css';</style>")]
    public async Task NestedSvgExternalResourcesAreRejected(string content)
    {
        File.WriteAllText(Output("nested.svg"), $"<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'>{content}</svg>");
        string template = Template.Replace("<fo:block>", "<fo:block><fo:external-graphic src='nested.svg'/>");
        using MemoryStream xml = Text("<root/>");
        using MemoryStream xslt = Text(template);
        using var transformer = new SaxonFoTransformer();
        using var renderer = new EmbeddedFopRenderer();
        string target = Output("result.pdf");
        File.WriteAllText(target, "previous");
        await Assert.ThrowsAsync<ConversionException>(() => new XmlPdfGenerator(transformer, renderer).GenerateAsync(
            new(XmlSource.FromStream(xml), XmlSource.FromStream(xslt, new Uri(Output("main.xsl")))), target));
        Assert.Equal("previous", File.ReadAllText(target));
        Assert.Empty(Directory.GetDirectories(directory));
    }


    /// <summary>
    /// <para>Runs the NestedLocalSvgAndPngRenderAndCleanTemporaryFiles validation scenario.</para>
    /// <para>Выполняет проверку сценария NestedLocalSvgAndPngRenderAndCleanTemporaryFiles.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task NestedLocalSvgAndPngRenderAndCleanTemporaryFiles()
    {
        File.WriteAllBytes(Output("image.png"), PdfGenerator.TestData.PngFixture.Create(true, true));
        File.WriteAllText(Output("nested.svg"), "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='20' height='20'><image width='20' height='20' xlink:href='image.png'/></svg>");
        string template = Template.Replace("<fo:block>", "<fo:block><fo:external-graphic src='nested.svg'/>");
        using MemoryStream xml = Text("<root/>");
        using MemoryStream xslt = Text(template);
        using var transformer = new SaxonFoTransformer();
        using var renderer = new EmbeddedFopRenderer();
        ConversionResult result = await new XmlPdfGenerator(transformer, renderer).GenerateAsync(
            new(XmlSource.FromStream(xml), XmlSource.FromStream(xslt, new Uri(Output("main.xsl")))), Output("result.pdf"));
        Assert.Equal(1, result.PageCount);
        Assert.Empty(Directory.GetDirectories(directory));
    }


    /// <summary>
    /// <para>Runs the Dispose validation scenario.</para>
    /// <para>Выполняет проверку сценария Dispose.</para>
    /// </summary>
    public void Dispose() => Directory.Delete(directory, true);


    /// <summary>
    /// <para>Runs the NonSeekableStream validation scenario.</para>
    /// <para>Выполняет проверку сценария NonSeekableStream.</para>
    /// </summary>
    /// <param name="inner"><para>The inner argument.</para><para>Аргумент inner.</para></param>
    private sealed class NonSeekableStream(Stream inner) : Stream
    {

        /// <summary>
        /// <para>Gets or defines the CanRead value used by NonSeekableStream.</para>
        /// <para>Возвращает или определяет значение CanRead, используемое NonSeekableStream.</para>
        /// </summary>
        public override bool CanRead => inner.CanRead;

        /// <summary>
        /// <para>Gets or defines the CanSeek value used by NonSeekableStream.</para>
        /// <para>Возвращает или определяет значение CanSeek, используемое NonSeekableStream.</para>
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// <para>Gets or defines the CanWrite value used by NonSeekableStream.</para>
        /// <para>Возвращает или определяет значение CanWrite, используемое NonSeekableStream.</para>
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// <para>Gets or defines the Length value used by NonSeekableStream.</para>
        /// <para>Возвращает или определяет значение Length, используемое NonSeekableStream.</para>
        /// </summary>
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// <para>Gets or defines the Position value used by NonSeekableStream.</para>
        /// <para>Возвращает или определяет значение Position, используемое NonSeekableStream.</para>
        /// </summary>
        public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException();
        }

        /// <summary>
        /// <para>Executes Read as part of the NonSeekableStream conversion workflow.</para>
        /// <para>Выполняет Read в цепочке преобразования NonSeekableStream.</para>
        /// </summary>
        /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
        /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
        /// <param name="count"><para>The count argument.</para><para>Аргумент count.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        /// <summary>
        /// <para>Executes ReadAsync as part of the NonSeekableStream conversion workflow.</para>
        /// <para>Выполняет ReadAsync в цепочке преобразования NonSeekableStream.</para>
        /// </summary>
        /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
        /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);

        /// <summary>
        /// <para>Executes Flush as part of the NonSeekableStream conversion workflow.</para>
        /// <para>Выполняет Flush в цепочке преобразования NonSeekableStream.</para>
        /// </summary>
        public override void Flush() => throw new NotSupportedException();

        /// <summary>
        /// <para>Executes Seek as part of the NonSeekableStream conversion workflow.</para>
        /// <para>Выполняет Seek в цепочке преобразования NonSeekableStream.</para>
        /// </summary>
        /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
        /// <param name="origin"><para>The origin argument.</para><para>Аргумент origin.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <summary>
        /// <para>Executes SetLength as part of the NonSeekableStream conversion workflow.</para>
        /// <para>Выполняет SetLength в цепочке преобразования NonSeekableStream.</para>
        /// </summary>
        /// <param name="value"><para>The value argument.</para><para>Аргумент value.</para></param>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// <para>Executes Write as part of the NonSeekableStream conversion workflow.</para>
        /// <para>Выполняет Write в цепочке преобразования NonSeekableStream.</para>
        /// </summary>
        /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
        /// <param name="offset"><para>The offset argument.</para><para>Аргумент offset.</para></param>
        /// <param name="count"><para>The count argument.</para><para>Аргумент count.</para></param>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <summary>
        /// <para>Releases owned resources; caller-owned input and output streams are not closed.</para>
        /// <para>Освобождает принадлежащие объекту ресурсы; входные и выходные потоки вызывающего кода не закрываются.</para>
        /// </summary>
        /// <param name="disposing"><para>The disposing argument.</para><para>Аргумент disposing.</para></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }


    /// <summary>
    /// <para>Runs the FailingWriteStream validation scenario.</para>
    /// <para>Выполняет проверку сценария FailingWriteStream.</para>
    /// </summary>
    private sealed class FailingWriteStream : MemoryStream
    {

        /// <summary>
        /// <para>Executes WriteAsync as part of the FailingWriteStream conversion workflow.</para>
        /// <para>Выполняет WriteAsync в цепочке преобразования FailingWriteStream.</para>
        /// </summary>
        /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
        /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
                    => ValueTask.FromException(new IOException("Output failure"));
    }


    /// <summary>
    /// <para>Runs the FailedReadStream validation scenario.</para>
    /// <para>Выполняет проверку сценария FailedReadStream.</para>
    /// </summary>
    /// <param name="cancel"><para>The cancel argument.</para><para>Аргумент cancel.</para></param>
    private sealed class FailedReadStream(bool cancel) : MemoryStream
    {

        /// <summary>
        /// <para>Executes ReadAsync as part of the FailedReadStream conversion workflow.</para>
        /// <para>Выполняет ReadAsync в цепочке преобразования FailedReadStream.</para>
        /// </summary>
        /// <param name="buffer"><para>The buffer argument.</para><para>Аргумент buffer.</para></param>
        /// <param name="cancellationToken"><para>Cancellation token; synchronous in-process engines finish before their slot is released.</para><para>Токен отмены; синхронный движок внутри процесса завершает работу до освобождения слота.</para></param>
        /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
        /// <exception cref="OperationCanceledException"><para>The supplied cancellation token is canceled.</para><para>Переданный токен отмены отменён.</para></exception>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                    => ValueTask.FromException<int>(cancel ? new OperationCanceledException() : new IOException("Read failed"));
    }
}
