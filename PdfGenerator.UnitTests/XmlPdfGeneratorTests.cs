using AutoFixture;
using AutoFixture.AutoMoq;
using Company.XmlFo;
using FluentAssertions;
using Moq;
using Xunit;

namespace PdfGenerator.UnitTests;

public sealed class XmlPdfGeneratorTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "xmlpdf-unit-" + Guid.NewGuid().ToString("N"));
    private readonly IFixture fixture = new Fixture().Customize(new AutoMoqCustomization());

    public XmlPdfGeneratorTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task PassesFoAndCancellationToRendererAndCleansIntermediate()
    {
        using var cancellation = new CancellationTokenSource();
        Mock<IXsltFoTransformer> transformer = fixture.Freeze<Mock<IXsltFoTransformer>>();
        Mock<IFoPdfRenderer> renderer = fixture.Freeze<Mock<IFoPdfRenderer>>();
        string? intermediate = null;
        string target = Path.Combine(directory, "result.pdf");

        transformer.Setup(x => x.TransformFileAsync("input.xml", "template.xsl", It.IsAny<string>(), cancellation.Token))
            .Callback<string, string, string, CancellationToken>((_, _, fo, _) =>
            {
                intermediate = fo;
                File.WriteAllText(fo, "generated FO");
            })
            .Returns(Task.CompletedTask);
        renderer.Setup(x => x.RenderFileAsync(It.IsAny<string>(), target, cancellation.Token))
            .Callback<string, string, CancellationToken>((fo, _, _) =>
            {
                fo.Should().Be(intermediate);
                File.ReadAllText(fo).Should().Be("generated FO");
            })
            .Returns(Task.CompletedTask);
        XmlPdfGenerator generator = fixture.Create<XmlPdfGenerator>();

        await generator.GenerateFileAsync("input.xml", "template.xsl", target, cancellation.Token);

        transformer.Verify(x => x.TransformFileAsync("input.xml", "template.xsl", It.IsAny<string>(), cancellation.Token), Times.Once);
        renderer.Verify(x => x.RenderFileAsync(intermediate!, target, cancellation.Token), Times.Once);
        intermediate.Should().NotBeNull();
        File.Exists(intermediate).Should().BeFalse();
        transformer.VerifyNoOtherCalls();
        renderer.VerifyNoOtherCalls();
    }

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

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
