using System.Text;
using FluentAssertions;
using PdfGenerator.TestData;
using Xunit;

namespace PdfGenerator.IntegrationTests;


/// <summary>
/// <para>Runs the EmbeddedRendererTests validation scenario.</para>
/// <para>Выполняет проверку сценария EmbeddedRendererTests.</para>
/// </summary>
public sealed class EmbeddedRendererTests : IDisposable
{

    /// <summary>
    /// <para>Runs the directory validation scenario.</para>
    /// <para>Выполняет проверку сценария directory.</para>
    /// </summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "xmlfo-test-" + Guid.NewGuid().ToString("N"));


    /// <summary>
    /// <para>Runs the EmbeddedRendererTests validation scenario.</para>
    /// <para>Выполняет проверку сценария EmbeddedRendererTests.</para>
    /// </summary>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public EmbeddedRendererTests() => Directory.CreateDirectory(directory);


    /// <summary>
    /// <para>Runs the WriteFo validation scenario.</para>
    /// <para>Выполняет проверку сценария WriteFo.</para>
    /// </summary>
    /// <param name="content"><para>The content argument.</para><para>Аргумент content.</para></param>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    private string WriteFo(string content)
    {
        string path = Path.Combine(directory, "input.fo");
        File.WriteAllText(path, """
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format">
            <fo:layout-master-set><fo:simple-page-master master-name="a4" page-width="210mm" page-height="297mm" margin="20mm"><fo:region-body/></fo:simple-page-master></fo:layout-master-set>
            <fo:page-sequence master-reference="a4"><fo:flow flow-name="xsl-region-body" font-family="sans-serif">
            """ + content + "</fo:flow></fo:page-sequence></fo:root>");
        return path;
    }


    /// <summary>
    /// <para>Runs the BundledRendererHandlesCyrillicPngAlphaIccAndSvg validation scenario.</para>
    /// <para>Выполняет проверку сценария BundledRendererHandlesCyrillicPngAlphaIccAndSvg.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task BundledRendererHandlesCyrillicPngAlphaIccAndSvg()
    {
        byte[] original = PngFixture.Create(true, true);
        string png = Path.Combine(directory, "original.png");
        await File.WriteAllBytesAsync(png, original);
        string fo = WriteFo($"""
            <fo:block>Кириллица</fo:block>
            <fo:block><fo:external-graphic src="{new Uri(png).AbsoluteUri}"/></fo:block>
            <fo:block><fo:external-graphic src="data:image/png;base64,{Convert.ToBase64String(original)}"/></fo:block>
            <fo:block><fo:instream-foreign-object><svg xmlns="http://www.w3.org/2000/svg" width="160" height="80"><defs><filter id="blur"><feGaussianBlur stdDeviation="2"/></filter></defs><rect width="160" height="80" fill="green"/><rect x="5" y="5" width="20" height="20" fill="red" filter="url(#blur)"/><text x="5" y="70" font-family="sans-serif" font-size="12">SVG текст</text></svg></fo:instream-foreign-object></fo:block>
            """);
        using var renderer = new EmbeddedFopRenderer();
        string pdf = Path.Combine(directory, "output.pdf");
        await renderer.RenderFileAsync(fo, pdf);
        byte[] bytes = await File.ReadAllBytesAsync(pdf);
        Encoding.Latin1.GetString(bytes).Should().StartWith("%PDF-").And.Contain("/SMask");
        (await File.ReadAllBytesAsync(png)).Should().Equal(original);
        Directory.GetDirectories(directory).Should().BeEmpty();
    }


    /// <summary>
    /// <para>Runs the MissingImageFailsWithoutReplacingPreviousPdf validation scenario.</para>
    /// <para>Выполняет проверку сценария MissingImageFailsWithoutReplacingPreviousPdf.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task MissingImageFailsWithoutReplacingPreviousPdf()
    {
        string fo = WriteFo("<fo:block><fo:external-graphic src='missing.png'/></fo:block>");
        string pdf = Path.Combine(directory, "output.pdf");
        await File.WriteAllTextAsync(pdf, "previous");
        using var renderer = new EmbeddedFopRenderer();
        Func<Task> action = () => renderer.RenderFileAsync(fo, pdf);
        await action.Should().ThrowAsync<ConversionException>();
        (await File.ReadAllTextAsync(pdf)).Should().Be("previous");
    }


    /// <summary>
    /// <para>Runs the PreCancelledOperationDoesNotCreatePdf validation scenario.</para>
    /// <para>Выполняет проверку сценария PreCancelledOperationDoesNotCreatePdf.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task PreCancelledOperationDoesNotCreatePdf()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var renderer = new EmbeddedFopRenderer();
        string pdf = Path.Combine(directory, "output.pdf");
        Func<Task> action = () => renderer.RenderFileAsync(WriteFo("<fo:block>text</fo:block>"), pdf, cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(pdf).Should().BeFalse();
    }


    /// <summary>
    /// <para>Runs the Dispose validation scenario.</para>
    /// <para>Выполняет проверку сценария Dispose.</para>
    /// </summary>
    public void Dispose() => Directory.Delete(directory, true);
}
