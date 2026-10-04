using Xunit;

namespace PdfGenerator.IntegrationTests;


/// <summary>
/// <para>Runs the XsltDiagnosticsTests validation scenario.</para>
/// <para>Выполняет проверку сценария XsltDiagnosticsTests.</para>
/// </summary>
public sealed class XsltDiagnosticsTests : IDisposable
{

    /// <summary>
    /// <para>Runs the directory validation scenario.</para>
    /// <para>Выполняет проверку сценария directory.</para>
    /// </summary>
    private readonly string directory = Path.Combine(Path.GetTempPath(), "xslt-diagnostics-" + Guid.NewGuid().ToString("N"));


    /// <summary>
    /// <para>Runs the InlineAnalysisReportsAltovaAndRecognizesLocalFunction validation scenario.</para>
    /// <para>Выполняет проверку сценария InlineAnalysisReportsAltovaAndRecognizesLocalFunction.</para>
    /// </summary>
    [Fact]
    public void InlineAnalysisReportsAltovaAndRecognizesLocalFunction()
    {
        const string stylesheet = """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
              xmlns:a="http://www.altova.com/xslt-extensions" xmlns:local="urn:local">
              <xsl:function name="local:id"><xsl:param name="v"/><xsl:sequence select="$v"/></xsl:function>
              <xsl:template match="/">
                <xsl:value-of select="a:distinct-nodes(//item), local:id('ok')"/>
                <xsl:value-of select="'a:evaluate(1)'"/>
              </xsl:template>
            </xsl:stylesheet>
            """;
        XsltDiagnosticReport report = new XsltDiagnostics().Analyze(stylesheet);
        Assert.True(report.HasErrors);
        Assert.Single(report.Functions, f => f.NamespaceUri == "http://www.altova.com/xslt-extensions");
        Assert.Equal(XsltFunctionSupport.Unsupported, report.Functions.Single(f => f.Name == "distinct-nodes").Support);
        Assert.Equal(XsltFunctionSupport.Supported, report.Functions.Single(f => f.Name == "id").Support);
        Assert.DoesNotContain(report.Functions, f => f.Name == "evaluate");
    }


    /// <summary>
    /// <para>Runs the FileAnalysisFollowsIncludesAndReportsBlockedFeatures validation scenario.</para>
    /// <para>Выполняет проверку сценария FileAnalysisFollowsIncludesAndReportsBlockedFeatures.</para>
    /// </summary>
    [Fact]
    public void FileAnalysisFollowsIncludesAndReportsBlockedFeatures()
    {
        Directory.CreateDirectory(directory);
        string main = Path.Combine(directory, "main.xsl");
        File.WriteAllText(main, """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:include href="part.xsl"/>
            </xsl:stylesheet>
            """);
        File.WriteAllText(Path.Combine(directory, "part.xsl"), """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
              xmlns:a="http://www.altova.com/xslt-extensions" xmlns:fn="http://www.w3.org/2005/xpath-functions">
              <xsl:template match="/">
                <xsl:value-of select="a:evaluate('//item')"/>
                <xsl:value-of select="fn:doc('other.xml')"/>
                <xsl:result-document href="other.xml"><item/></xsl:result-document>
              </xsl:template>
            </xsl:stylesheet>
            """);

        XsltDiagnosticReport report = new XsltDiagnostics().AnalyzeFile(main);
        Assert.Equal(2, report.StylesheetsInspected);
        Assert.True(report.HasErrors);
        Assert.Contains(report.Functions, f => f.Name == "evaluate" && f.Support == XsltFunctionSupport.Unsupported);
        Assert.Contains(report.Functions, f => f.Name == "doc" && f.Support == XsltFunctionSupport.Unsupported);
        Assert.Contains(report.Diagnostics, d => d.Code == "XSLT_RESULT_DOCUMENT");
    }


    /// <summary>
    /// <para>Runs the UnsafeIncludeIsReportedWithoutReadingIt validation scenario.</para>
    /// <para>Выполняет проверку сценария UnsafeIncludeIsReportedWithoutReadingIt.</para>
    /// </summary>
    [Fact]
    public void UnsafeIncludeIsReportedWithoutReadingIt()
    {
        Directory.CreateDirectory(directory);
        string main = Path.Combine(directory, "main.xsl");
        File.WriteAllText(main, """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:include href="https://example.com/private.xsl"/>
            </xsl:stylesheet>
            """);
        XsltDiagnosticReport report = new XsltDiagnostics().AnalyzeFile(main);
        Assert.True(report.HasErrors);
        Assert.Contains(report.Diagnostics, d => d.Code == "XSLT_INCLUDE");
        Assert.Equal(1, report.StylesheetsInspected);
    }


    /// <summary>
    /// <para>Runs the ExistingFixtureHasNoAltovaFunctions validation scenario.</para>
    /// <para>Выполняет проверку сценария ExistingFixtureHasNoAltovaFunctions.</para>
    /// </summary>
    [Fact]
    public void ExistingFixtureHasNoAltovaFunctions()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "test-data/basic/document.xsl");
        XsltDiagnosticReport report = new XsltDiagnostics().AnalyzeFile(path);
        Assert.False(report.HasErrors);
        Assert.Equal(2, report.StylesheetsInspected);
        Assert.DoesNotContain(report.Functions, f => f.NamespaceUri == "http://www.altova.com/xslt-extensions");
    }


    /// <summary>
    /// <para>Runs the FunctionDeclaredInIncludedModuleIsSupported validation scenario.</para>
    /// <para>Выполняет проверку сценария FunctionDeclaredInIncludedModuleIsSupported.</para>
    /// </summary>
    [Fact]
    public void FunctionDeclaredInIncludedModuleIsSupported()
    {
        Directory.CreateDirectory(directory);
        string main = Path.Combine(directory, "main.xsl");
        File.WriteAllText(main, """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:local="urn:local">
              <xsl:include href="functions.xsl"/>
              <xsl:template match="/"><xsl:value-of select="local:id('ok')"/></xsl:template>
            </xsl:stylesheet>
            """);
        File.WriteAllText(Path.Combine(directory, "functions.xsl"), """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:local="urn:local">
              <xsl:function name="local:id"><xsl:param name="v"/><xsl:sequence select="$v"/></xsl:function>
            </xsl:stylesheet>
            """);

        XsltDiagnosticReport report = new XsltDiagnostics().AnalyzeFile(main);
        Assert.False(report.HasErrors);
        Assert.Equal(XsltFunctionSupport.Supported, Assert.Single(report.Functions).Support);
    }


    /// <summary>
    /// <para>Runs the UnprefixedRuntimeResourceAndDynamicEvaluationAreReported validation scenario.</para>
    /// <para>Выполняет проверку сценария UnprefixedRuntimeResourceAndDynamicEvaluationAreReported.</para>
    /// </summary>
    [Fact]
    public void UnprefixedRuntimeResourceAndDynamicEvaluationAreReported()
    {
        const string stylesheet = """
            <xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/">
                <xsl:value-of select="doc('other.xml')"/>
                <xsl:evaluate xpath="'1 + 1'"/>
              </xsl:template>
            </xsl:stylesheet>
            """;
        XsltDiagnosticReport report = new XsltDiagnostics().Analyze(stylesheet);
        Assert.True(report.HasErrors);
        Assert.Contains(report.Diagnostics, d => d.Code == "XSLT_RUNTIME_RESOURCE");
        Assert.Contains(report.Diagnostics, d => d.Code == "XSLT_DYNAMIC_EVALUATION");
    }


    /// <summary>
    /// <para>Runs the UnregisteredCustomExtensionIsUnsupported validation scenario.</para>
    /// <para>Выполняет проверку сценария UnregisteredCustomExtensionIsUnsupported.</para>
    /// </summary>
    [Fact]
    public void UnregisteredCustomExtensionIsUnsupported()
    {
        const string stylesheet = """
            <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:company="urn:company:custom">
              <xsl:template match="/"><xsl:value-of select="company:lookup('x')"/></xsl:template>
            </xsl:stylesheet>
            """;
        XsltDiagnosticReport report = new XsltDiagnostics().Analyze(stylesheet);
        Assert.True(report.HasErrors);
        Assert.Equal(XsltFunctionSupport.Unsupported, Assert.Single(report.Functions).Support);
    }


    /// <summary>
    /// <para>Runs the NonStylesheetXmlIsRejected validation scenario.</para>
    /// <para>Выполняет проверку сценария NonStylesheetXmlIsRejected.</para>
    /// </summary>
    [Fact]
    public void NonStylesheetXmlIsRejected()
    {
        XsltDiagnosticReport report = new XsltDiagnostics().Analyze("<document/>");
        Assert.True(report.HasErrors);
        Assert.Contains(report.Diagnostics, d => d.Code == "XSLT_ROOT");
    }


    /// <summary>
    /// <para>Runs the Dispose validation scenario.</para>
    /// <para>Выполняет проверку сценария Dispose.</para>
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
