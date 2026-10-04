using System.Diagnostics;
using System.Text;
using Xunit;

namespace PdfGenerator.IntegrationTests;


/// <summary>
/// <para>Runs the WorkerTests validation scenario.</para>
/// <para>Выполняет проверку сценария WorkerTests.</para>
/// </summary>
public sealed class WorkerTests : IDisposable
{

    /// <summary>
    /// <para>Runs the directory validation scenario.</para>
    /// <para>Выполняет проверку сценария directory.</para>
    /// </summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pdf-worker-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// <para>Runs the Fixture validation scenario.</para>
    /// <para>Выполняет проверку сценария Fixture.</para>
    /// </summary>
    /// <param name="name"><para>The name argument.</para><para>Аргумент name.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "test-data/basic", name);

    /// <summary>
    /// <para>Runs the WorkerTests validation scenario.</para>
    /// <para>Выполняет проверку сценария WorkerTests.</para>
    /// </summary>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public WorkerTests() => Directory.CreateDirectory(directory);


    /// <summary>
    /// <para>Runs the WorkerPublishesAndReusesProcessThenRecoversAfterCrash validation scenario.</para>
    /// <para>Выполняет проверку сценария WorkerPublishesAndReusesProcessThenRecoversAfterCrash.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task WorkerPublishesAndReusesProcessThenRecoversAfterCrash()
    {
        await using var generator = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory
        });
        var request = new ConversionRequest(XmlSource.FromFile(Fixture("input.xml")), XmlSource.FromFile(Fixture("document.xsl")), jobId: "worker-test");
        ConversionResult first = await generator.GenerateAsync(request, Path.Combine(directory, "first.pdf"));
        Assert.Equal(1, first.PageCount);
        int pid = Assert.Single(generator.ProcessIds);
        using var output = new MemoryStream();
        ConversionResult second = await generator.GenerateAsync(request, output);
        Assert.Equal(pid, Assert.Single(generator.ProcessIds));
        Assert.Equal(first.PageCount, second.PageCount);
        Assert.Equal(output.Length, second.PdfBytes);
        using (var process = Process.GetProcessById(pid))
        {
            process.Kill(true);
            await process.WaitForExitAsync();
        }
        await generator.GenerateAsync(request, Path.Combine(directory, "third.pdf"));
        Assert.NotEqual(pid, Assert.Single(generator.ProcessIds));
        Assert.Equal(2, generator.Statistics.Starts);
        Assert.Equal(3, generator.Statistics.CompletedJobs);
    }


    /// <summary>
    /// <para>Runs the HardTimeoutAndCancellationKillBusyWorkerAndPreserveOutput validation scenario.</para>
    /// <para>Выполняет проверку сценария HardTimeoutAndCancellationKillBusyWorkerAndPreserveOutput.</para>
    /// </summary>
    /// <param name="cancel"><para>The cancel argument.</para><para>Аргумент cancel.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HardTimeoutAndCancellationKillBusyWorkerAndPreserveOutput(bool cancel)
    {
        await using var generator = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory,
            JobTimeout = TimeSpan.FromMilliseconds(300)
        });
        string xslt = Path.Combine(directory, "loop.xsl");
        File.WriteAllText(xslt, """
            <xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='2.0'>
            <xsl:template match='/'><fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format'><xsl:call-template name='loop'><xsl:with-param name='n' select='0'/></xsl:call-template></fo:root></xsl:template>
            <xsl:template name='loop'><xsl:param name='n'/><xsl:call-template name='loop'><xsl:with-param name='n' select='$n+1'/></xsl:call-template></xsl:template>
            </xsl:stylesheet>
            """);
        string target = Path.Combine(directory, "result.pdf");
        File.WriteAllText(target, "previous");
        using var cancellation = new CancellationTokenSource();
        if (cancel)
        {
            cancellation.CancelAfter(1000);
        }
        var request = new ConversionRequest(XmlSource.FromFile(Fixture("input.xml")), XmlSource.FromFile(xslt),
            settings: cancel ? new ConversionSettings { Timeout = TimeSpan.FromSeconds(10) } : null);
        var watch = Stopwatch.StartNew();
        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generator.GenerateAsync(request, target, cancellation.Token));
        }
        else
        {
            Assert.Equal(WorkerFailureKind.Timeout, (await Assert.ThrowsAsync<PdfWorkerException>(() => generator.GenerateAsync(request, target))).Kind);
        }
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(35));
        Assert.Equal("previous", File.ReadAllText(target));
        Assert.Empty(generator.ProcessIds);
    }


    /// <summary>
    /// <para>Runs the WorkerTypedParametersAndStreamsMatchInProcess validation scenario.</para>
    /// <para>Выполняет проверку сценария WorkerTypedParametersAndStreamsMatchInProcess.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task WorkerTypedParametersAndStreamsMatchInProcess()
    {
        const string template = "<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:p='urn:p' version='2.0'><xsl:param name='p:number' as='xs:integer'/><xsl:template match='/'><fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format'><fo:layout-master-set><fo:simple-page-master master-name='a'><fo:region-body/></fo:simple-page-master></fo:layout-master-set><fo:page-sequence master-reference='a'><fo:flow flow-name='xsl-region-body'><fo:block><xsl:value-of select='$p:number'/></fo:block></fo:flow></fo:page-sequence></fo:root></xsl:template></xsl:stylesheet>";
        await using var worker = new WorkerXmlPdfGenerator(new()
        {
            WorkingDirectory = directory
        });
        using var xml = new MemoryStream("<root/>"u8.ToArray());
        using var xslt = new MemoryStream(Encoding.UTF8.GetBytes(template));
        using var output = new MemoryStream();
        ConversionResult result = await worker.GenerateAsync(new(XmlSource.FromStream(xml), XmlSource.FromStream(xslt), [new("number", ulong.MaxValue, "urn:p")]), output);
        Assert.Equal(1, result.PageCount);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(output.ToArray()));
        Assert.True(xml.CanRead && xslt.CanRead && output.CanWrite);
    }


    /// <summary>
    /// <para>Runs the Dispose validation scenario.</para>
    /// <para>Выполняет проверку сценария Dispose.</para>
    /// </summary>
    public void Dispose() => Directory.Delete(directory, true);
}
