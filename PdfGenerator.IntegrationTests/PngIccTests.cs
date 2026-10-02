using System.Buffers.Binary;
using System.Xml.Linq;
using Company.XmlFo;
using PdfGenerator.TestData;
using Xunit;

namespace PdfGenerator.IntegrationTests;

public sealed class PngIccTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "png-icc-tests-" + Guid.NewGuid().ToString("N"));
    public PngIccTests() => Directory.CreateDirectory(directory);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovesProfileIncludingBadCrcAndKeepsAllOtherBytes(bool badCrc)
    {
        Assert.Equal(PngFixture.Create(false), PngIccProfileRemover.RemoveIccProfile(PngFixture.Create(true, badCrc)));
    }

    [Fact]
    public void ImageWithoutProfileRemainsByteIdentical()
    {
        byte[] png = PngFixture.Create(false);
        Assert.Equal(png, PngIccProfileRemover.RemoveIccProfile(png));
    }

    [Fact]
    public void RejectsTruncationOverflowMissingEndAndCorruptPixelChunk()
    {
        byte[] png = PngFixture.Create(false);
        Assert.Throws<InvalidDataException>(() => PngIccProfileRemover.RemoveIccProfile(png.AsSpan(0, png.Length - 1)));
        Assert.Throws<InvalidDataException>(() => PngIccProfileRemover.RemoveIccProfile(png.AsSpan(0, png.Length - 12)));
        byte[] overflow = (byte[])png.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(overflow.AsSpan(8), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => PngIccProfileRemover.RemoveIccProfile(overflow));
        byte[] corrupt = (byte[])png.Clone();
        corrupt[45] ^= 1;
        Assert.Throws<InvalidDataException>(() => PngIccProfileRemover.RemoveIccProfile(corrupt));
    }

    [Theory]
    [InlineData("local")]
    [InlineData("base64")]
    [InlineData("percent")]
    public async Task PreparesCopiesAndKeepsOriginalFoAndPng(string kind)
    {
        byte[] png = PngFixture.Create();
        string image = Path.Combine(directory, "изображение без расширения");
        File.WriteAllBytes(image, png);
        string source = kind switch
        {
            "local" => "url('изображение без расширения')",
            "base64" => "data:image/png;base64," + Convert.ToBase64String(png),
            _ => "data:image/png," + string.Concat(png.Select(b => $"%{b:X2}"))
        };
        string fo = WriteFo(source);
        byte[] originalFo = File.ReadAllBytes(fo);
        string staged;
        using (FoImagePreparation prepared = await FoImagePreparation.CreateAsync(fo, directory, new(), CancellationToken.None))
        {
            staged = prepared.FoPath;
            var document = XDocument.Load(staged);
            string[] paths = document.Descendants().Attributes("src").Select(a => new Uri(a.Value).LocalPath).ToArray();
            Assert.Equal(2, paths.Length);
            Assert.Equal(paths[0], paths[1]);
            Assert.Equal(PngFixture.Create(false), File.ReadAllBytes(paths[0]));
        }
        Assert.False(File.Exists(staged));
        Assert.Equal(originalFo, File.ReadAllBytes(fo));
        Assert.Equal(png, File.ReadAllBytes(image));
        Assert.Empty(Directory.GetDirectories(directory));
    }

    [Fact]
    public async Task HonorsXmlBaseAndProcessesBackgroundAndInlineSvg()
    {
        string sub = Path.Combine(directory, "images");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "image.png"), PngFixture.Create());
        string fo = Path.Combine(directory, "document.fo");
        File.WriteAllText(fo, "<fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format' xml:base='images/'><fo:block background-image=\"url('image.png')\"><svg:svg xmlns:svg='http://www.w3.org/2000/svg'><svg:image href='image.png'/></svg:svg></fo:block></fo:root>");
        using FoImagePreparation prepared = await FoImagePreparation.CreateAsync(fo, directory, new(), CancellationToken.None);
        var document = XDocument.Load(prepared.FoPath);
        foreach (XAttribute? reference in document.Descendants().Attributes().Where(a => a.Name.LocalName is "href" or "background-image"))
        {
            Assert.Equal(PngFixture.Create(false), File.ReadAllBytes(new Uri(reference.Value).LocalPath));
        }
    }

    [Fact]
    public async Task InvalidImagePreservesPreviousPdfAndCleansPreparation()
    {
        File.WriteAllBytes(Path.Combine(directory, "bad.png"), [1, 2, 3]);
        string fo = WriteFo("bad.png");
        string pdf = Path.Combine(directory, "result.pdf");
        File.WriteAllText(pdf, "previous");
        using var renderer = new FopProcessRenderer(new()
        {
            ExecutablePath = "must-not-start"
        });
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(() => renderer.RenderFileAsync(fo, pdf));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal("previous", File.ReadAllText(pdf));
        Assert.Empty(Directory.GetDirectories(directory));
    }

    [Fact]
    public async Task LimitAndRemoteImageFailuresCleanPreparation()
    {
        File.WriteAllBytes(Path.Combine(directory, "image.png"), PngFixture.Create());
        await Assert.ThrowsAsync<IOException>(() => FoImagePreparation.CreateAsync(WriteFo("image.png"), directory, new() { MaxImageBytes = 10 }, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => FoImagePreparation.CreateAsync(WriteFo("https://example.com/image.png"), directory, new(), CancellationToken.None));
        Assert.Empty(Directory.GetDirectories(directory));
    }

    private string WriteFo(string source)
    {
        XNamespace fo = "http://www.w3.org/1999/XSL/Format";
        string path = Path.Combine(directory, "document.fo");
        new XDocument(new XElement(fo + "root", Enumerable.Range(0, 2).Select(_ => new XElement(fo + "external-graphic", new XAttribute("src", source))))).Save(path);
        return path;
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
