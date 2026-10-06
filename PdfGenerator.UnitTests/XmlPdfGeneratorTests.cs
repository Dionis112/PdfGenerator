namespace PdfGenerator.UnitTests;


/// <summary>
/// <para>Runs the XmlPdfGeneratorTests validation scenario.</para>
/// <para>Выполняет проверку сценария XmlPdfGeneratorTests.</para>
/// </summary>
public sealed class XmlPdfGeneratorTests : IDisposable
{

    /// <summary>
    /// <para>Runs the directory validation scenario.</para>
    /// <para>Выполняет проверку сценария directory.</para>
    /// </summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "xmlpdf-unit-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// <para>Runs the fixture validation scenario.</para>
    /// <para>Выполняет проверку сценария fixture.</para>
    /// </summary>
    private readonly IFixture fixture = new Fixture().Customize(new AutoMoqCustomization());


    /// <summary>
    /// <para>Runs the XmlPdfGeneratorTests validation scenario.</para>
    /// <para>Выполняет проверку сценария XmlPdfGeneratorTests.</para>
    /// </summary>
    /// <exception cref="ArgumentException"><para>An argument violates the documented contract.</para><para>Аргумент нарушает описанный контракт.</para></exception>
    public XmlPdfGeneratorTests() => Directory.CreateDirectory(directory);


    /// <summary>
    /// <para>Runs the PassesFoAndCancellationToRendererAndCleansIntermediate validation scenario.</para>
    /// <para>Выполняет проверку сценария PassesFoAndCancellationToRendererAndCleansIntermediate.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task PassesFoAndCancellationToRendererAndCleansIntermediate()
    {
        using var cancellation = new CancellationTokenSource();
        Mock<IXsltFoTransformer> transformer = fixture.Freeze<Mock<IXsltFoTransformer>>();
        Mock<IFoPdfRenderer> renderer = fixture.Freeze<Mock<IFoPdfRenderer>>();
        string? intermediate = null;
        string target = Path.Combine(directory, "result.pdf");

        transformer.Setup(x => x.TransformFileAsync(Path.GetFullPath("input.xml"), Path.GetFullPath("template.xsl"), It.IsAny<string>(), cancellation.Token))
            .Callback<string, string, string, CancellationToken>((_, _, fo, _) =>
            {
                intermediate = fo;
                File.WriteAllText(fo, "generated FO");
            })
            .Returns(Task.CompletedTask);
        renderer.Setup(x => x.RenderFileAsync(It.IsAny<string>(), It.IsAny<string>(), cancellation.Token))
            .Callback<string, string, CancellationToken>((fo, pdf, _) =>
            {
                fo.Should().Be(intermediate);
                File.ReadAllText(fo).Should().Be("generated FO");
                File.WriteAllText(pdf, "generated PDF");
            })
            .Returns(Task.CompletedTask);
        XmlPdfGenerator generator = fixture.Create<XmlPdfGenerator>();

        await generator.GenerateFileAsync("input.xml", "template.xsl", target, cancellation.Token);

        transformer.Verify(x => x.TransformFileAsync(Path.GetFullPath("input.xml"), Path.GetFullPath("template.xsl"), It.IsAny<string>(), cancellation.Token), Times.Once);
        renderer.Verify(x => x.RenderFileAsync(intermediate!, It.IsAny<string>(), cancellation.Token), Times.Once);
        File.ReadAllText(target).Should().Be("generated PDF");
        intermediate.Should().NotBeNull();
        File.Exists(intermediate).Should().BeFalse();
        transformer.VerifyNoOtherCalls();
        renderer.VerifyNoOtherCalls();
    }


    /// <summary>
    /// <para>Runs the FailedTransformDoesNotCallRendererAndCleansPartialFo validation scenario.</para>
    /// <para>Выполняет проверку сценария FailedTransformDoesNotCallRendererAndCleansPartialFo.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task FailedTransformDoesNotCallRendererAndCleansPartialFo()
    {
        Mock<IXsltFoTransformer> transformer = fixture.Freeze<Mock<IXsltFoTransformer>>();
        Mock<IFoPdfRenderer> renderer = fixture.Freeze<Mock<IFoPdfRenderer>>();
        transformer.Setup(x => x.TransformFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, fo, _) => File.WriteAllText(fo, "partial"))
            .Throws(new ConversionException(ConversionStage.Transform, "failure"));
        XmlPdfGenerator generator = fixture.Create<XmlPdfGenerator>();

        Func<Task> action = () => generator.GenerateFileAsync("input.xml", "template.xsl", Path.Combine(directory, "result.pdf"));
        await action.Should().ThrowAsync<ConversionException>().Where(x => x.Stage == ConversionStage.Transform);

        renderer.VerifyNoOtherCalls();
        Directory.GetFiles(directory).Should().BeEmpty();
    }


    /// <summary>
    /// <para>Runs the RejectsOutputOverwritingInputBeforeCallingEngines validation scenario.</para>
    /// <para>Выполняет проверку сценария RejectsOutputOverwritingInputBeforeCallingEngines.</para>
    /// </summary>
    /// <returns><para>The operation result or task representing its completion.</para><para>Результат операции или задача, представляющая её завершение.</para></returns>
    [Fact]
    public async Task RejectsOutputOverwritingInputBeforeCallingEngines()
    {
        string source = Path.Combine(directory, "input.xml");
        Mock<IXsltFoTransformer> transformer = fixture.Freeze<Mock<IXsltFoTransformer>>();
        Mock<IFoPdfRenderer> renderer = fixture.Freeze<Mock<IFoPdfRenderer>>();
        XmlPdfGenerator generator = fixture.Create<XmlPdfGenerator>();

        Func<Task> action = () => generator.GenerateFileAsync(source, "template.xsl", source);
        await action.Should().ThrowAsync<ArgumentException>();

        transformer.VerifyNoOtherCalls();
        renderer.VerifyNoOtherCalls();
    }


    /// <summary>
    /// <para>Runs the Dispose validation scenario.</para>
    /// <para>Выполняет проверку сценария Dispose.</para>
    /// </summary>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
