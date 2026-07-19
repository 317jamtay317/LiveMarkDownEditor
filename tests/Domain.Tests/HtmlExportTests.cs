using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="HtmlExport"/>, the pure composition of a Rendered Output into the Export
/// Shape the user chose. Covers INV-032: both Export Shapes carry the same Rendered Output, and a
/// Standalone Page is that Rendered Output plus a fixed wrapper.
/// </summary>
public sealed class HtmlExportTests
{
    private static readonly RenderedOutput Output = new("<h1>Title</h1>\n<p>Body.</p>\n");

    [Fact]
    public void Compose_GivenNullOutput_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(
            () => HtmlExport.Compose(null!, ExportShape.HtmlFragment, "Title"));
    }

    [Fact]
    public void Compose_GivenNullTitle_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(
            () => HtmlExport.Compose(Output, ExportShape.StandalonePage, null!));
    }

    [Fact]
    public void Compose_GivenUnknownShape_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => HtmlExport.Compose(Output, (ExportShape)42, "Title"));
    }

    [Fact]
    public void Compose_HtmlFragment_IsTheRenderedOutputAlone_INV032()
    {
        HtmlExport.Compose(Output, ExportShape.HtmlFragment, "Title").ShouldBe(Output.Html);
    }

    [Fact]
    public void Compose_StandalonePage_CarriesTheSameRenderedOutputAsTheFragment_INV032()
    {
        var page = HtmlExport.Compose(Output, ExportShape.StandalonePage, "Title");
        var fragment = HtmlExport.Compose(Output, ExportShape.HtmlFragment, "Title");

        page.ShouldContain(fragment);
    }

    [Fact]
    public void Compose_StandalonePage_IsACompleteHtmlDocument_INV032()
    {
        var page = HtmlExport.Compose(Output, ExportShape.StandalonePage, "Title");

        page.ShouldStartWith("<!DOCTYPE html>");
        page.ShouldContain("<html lang=\"en\">");
        page.ShouldContain("<meta charset=\"utf-8\">");
        page.ShouldContain("<style>");
        page.TrimEnd().ShouldEndWith("</html>");
    }

    [Fact]
    public void Compose_StandalonePage_CarriesTheGivenTitle_INV032()
    {
        HtmlExport.Compose(Output, ExportShape.StandalonePage, "My notes")
            .ShouldContain("<title>My notes</title>");
    }

    [Theory]
    [InlineData("A & B", "A &amp; B")]
    [InlineData("<script>", "&lt;script&gt;")]
    [InlineData("\"quoted\"", "&quot;quoted&quot;")]
    public void Compose_StandalonePage_EscapesTheTitle_INV032(string title, string expected)
    {
        // The title comes from a file name the user chose, so it is not trusted to be HTML-safe:
        // an unescaped '<' would close the <title> element and spill into the page.
        HtmlExport.Compose(Output, ExportShape.StandalonePage, title).ShouldContain($"<title>{expected}</title>");
    }

    [Fact]
    public void Compose_GivenAnEmptyRenderedOutput_StillProducesAPage_INV032()
    {
        var page = HtmlExport.Compose(RenderedOutput.Empty, ExportShape.StandalonePage, "Empty");

        page.ShouldStartWith("<!DOCTYPE html>");
        page.ShouldContain("<title>Empty</title>");
    }

    [Fact]
    public void Compose_GivenTheSameInputsTwice_YieldsIdenticalHtml_INV032()
    {
        // Composition is pure: it is a function of its three inputs and nothing else.
        HtmlExport.Compose(Output, ExportShape.StandalonePage, "Title")
            .ShouldBe(HtmlExport.Compose(Output, ExportShape.StandalonePage, "Title"));
    }

    private static readonly RenderedOutput WithDiagram =
        new("<pre><code class=\"language-mermaid\">graph TD\nA--&gt;B</code></pre>\n");

    [Fact]
    public void Compose_StandalonePage_WithAMermaidDiagram_EmbedsTheScript_INV049()
    {
        var page = HtmlExport.Compose(WithDiagram, ExportShape.StandalonePage, "Title", "MERMAID_LIB_CODE");

        // The bundled script is embedded so the mermaid block renders in a browser...
        page.ShouldContain("MERMAID_LIB_CODE");
        // ...and the Rendered Output itself is still carried verbatim (INV-032 unbroken).
        page.ShouldContain(WithDiagram.Html);
    }

    [Fact]
    public void Compose_StandalonePage_WithoutAMermaidDiagram_EmbedsNoScript_INV049()
    {
        // A document with no Mermaid Diagram never pays for the script.
        HtmlExport.Compose(Output, ExportShape.StandalonePage, "Title", "MERMAID_LIB_CODE")
            .ShouldNotContain("MERMAID_LIB_CODE");
    }

    [Fact]
    public void Compose_StandalonePage_WithADiagram_ButNoScriptSupplied_EmbedsNoScript_INV049()
    {
        // Render stays pure: with no script supplied, the page carries the mermaid code block unrendered.
        var page = HtmlExport.Compose(WithDiagram, ExportShape.StandalonePage, "Title", mermaidScript: null);

        page.ShouldContain(WithDiagram.Html);
        page.ShouldContain("language-mermaid");
    }

    [Fact]
    public void Compose_HtmlFragment_WithAMermaidDiagram_IsTheRenderedOutputAlone_INV049()
    {
        // The Fragment carries no wrapper and therefore no script — the same Rendered Output either
        // Export Shape carries (INV-032); the script lives only in the Standalone Page's wrapper.
        HtmlExport.Compose(WithDiagram, ExportShape.HtmlFragment, "Title", "MERMAID_LIB_CODE")
            .ShouldBe(WithDiagram.Html);
    }
}
