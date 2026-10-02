using System.Diagnostics;
using System.Xml.Linq;
using Company.XmlFo;
using PdfGenerator.TestData;

namespace PdfGenerator.Demo;

internal static class DemoSelfTest
{
    public static async Task RunAsync(SaxonFoTransformer transformer, IFoPdfRenderer renderer, string dataDirectory, string outputDirectory, bool pdf, bool verifyTools)
    {
        string data = Path.GetFullPath(dataDirectory);
        string output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        string xslt = Path.Combine(data, "basic/document.xsl");
        string fo = Path.Combine(output, "result.fo");
        await transformer.TransformFileAsync(Path.Combine(data, "basic/input.xml"), xslt, fo);
        if (!XNode.DeepEquals(XDocument.Load(fo).Root, XDocument.Load(Path.Combine(data, "basic/expected.fo")).Root))
        {
            throw new Exception("FO differs from expected structure/text.");
        }

        await transformer.TransformFileAsync(Path.Combine(data, "basic/empty.xml"), xslt, Path.Combine(output, "empty.fo"));
        if (!File.ReadAllText(Path.Combine(output, "empty.fo")).Contains("Пустой документ"))
        {
            throw new Exception("Empty fixture mismatch.");
        }

        byte[] previous = File.ReadAllBytes(fo);
        try
        {
            await transformer.TransformFileAsync(Path.Combine(data, "basic/invalid.xml"), xslt, fo);
            throw new Exception("Invalid XML was accepted.");
        }
        catch (ConversionException) { }
        if (!previous.SequenceEqual(File.ReadAllBytes(fo)))
        {
            throw new Exception("Previous output was modified.");
        }

        if (pdf)
        {
            string target = Path.Combine(output, "result.pdf");
            await new XmlPdfGenerator(transformer, renderer).GenerateFileAsync(Path.Combine(data, "basic/input.xml"), xslt, target);
            if (verifyTools)
            {
                var start = new ProcessStartInfo("pdftotext") { RedirectStandardOutput = true, UseShellExecute = false };
                start.ArgumentList.Add(target);
                start.ArgumentList.Add("-");
                using Process process = Process.Start(start)!;
                string extracted = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0 || !extracted.Contains("Первый товар") || !extracted.Contains("15.75"))
                {
                    throw new Exception("PDF text extraction mismatch (check Cyrillic fonts).");
                }
            }
            await new XmlPdfGenerator(transformer, renderer).GenerateFileAsync(Path.Combine(data, "basic/empty.xml"), xslt, Path.Combine(output, "empty.pdf"));
            byte[] originalPng = PngFixture.Create(withIcc: true, badProfileCrc: true);
            string pngPath = Path.Combine(output, "icc-original.png");
            await File.WriteAllBytesAsync(pngPath, originalPng);
            var imagesFo = XDocument.Load(fo);
            XNamespace foNamespace = "http://www.w3.org/1999/XSL/Format";
            XElement flow = imagesFo.Descendants(foNamespace + "flow").Single();
            flow.Add(new XElement(foNamespace + "block", "PNG without ICC: local file and embedded data URI"));
            foreach (string? imageSource in new[] { new Uri(pngPath).AbsoluteUri, "data:image/png;base64," + Convert.ToBase64String(originalPng) })
            {
                flow.Add(new XElement(foNamespace + "block", new XElement(foNamespace + "external-graphic", new XAttribute("src", imageSource), new XAttribute("content-width", "32mm"), new XAttribute("content-height", "32mm"))));
            }

            flow.Add(new XElement(foNamespace + "block", new XElement(foNamespace + "instream-foreign-object",
                XElement.Parse("<svg xmlns='http://www.w3.org/2000/svg' width='160' height='80'><defs><filter id='blur'><feGaussianBlur stdDeviation='2'/></filter></defs><rect width='160' height='80' fill='#228833'/><circle cx='80' cy='30' r='20' fill='#3344ee' fill-opacity='0.5'/><rect x='5' y='5' width='20' height='20' fill='red' filter='url(#blur)'/><text x='5' y='70' font-family='sans-serif' font-size='12'>SVG текст</text></svg>"))));
            string imageFoPath = Path.Combine(output, "images.fo");
            imagesFo.Save(imageFoPath);
            string imagePdf = Path.Combine(output, "images.pdf");
            await renderer.RenderFileAsync(imageFoPath, imagePdf);
            if (!originalPng.SequenceEqual(await File.ReadAllBytesAsync(pngPath)))
            {
                throw new Exception("Original PNG was modified.");
            }

            static async Task<string> ListImages(string path)
            {
                var imageInfo = new ProcessStartInfo("pdfimages") { RedirectStandardOutput = true, UseShellExecute = false };
                imageInfo.ArgumentList.Add("-list");
                imageInfo.ArgumentList.Add(path);
                using Process imageProcess = Process.Start(imageInfo)!;
                string result = await imageProcess.StandardOutput.ReadToEndAsync();
                await imageProcess.WaitForExitAsync();
                if (imageProcess.ExitCode != 0)
                {
                    throw new Exception("Cannot inspect PDF images.");
                }

                return result;
            }
            string? imageList = verifyTools ? await ListImages(imagePdf) : null;
            if (imageList is not null && ((!imageList.Contains("rgb") && !imageList.Contains("icc")) || !imageList.Contains("smask")))
            {
                throw new Exception("PNG pixels/transparency were not embedded in PDF.");
            }
            // FOP can add its own default sRGB ICC. Compare against an image initially without iCCP.
            byte[] baselinePng = PngFixture.Create(withIcc: false);
            string baselinePngPath = Path.Combine(output, "baseline-no-icc.png");
            await File.WriteAllBytesAsync(baselinePngPath, baselinePng);
            XElement[] graphics = imagesFo.Descendants(foNamespace + "external-graphic").ToArray();
            graphics[0].SetAttributeValue("src", new Uri(baselinePngPath).AbsoluteUri);
            graphics[1].SetAttributeValue("src", "data:image/png;base64," + Convert.ToBase64String(baselinePng));
            string baselineFo = Path.Combine(output, "baseline.fo");
            imagesFo.Save(baselineFo);
            string baselinePdf = Path.Combine(output, "baseline.pdf");
            await renderer.RenderFileAsync(baselineFo, baselinePdf);
            if (imageList is not null && imageList != await ListImages(baselinePdf))
            {
                throw new Exception("Sanitized PNG PDF differs from no-ICC baseline.");
            }
        }
        Console.WriteLine($"SELF-TEST PASS: FO{(pdf ? " + PDF + Cyrillic text + PNG without ICC (with alpha) + SVG" : "")}; output={output}");
    }
}
