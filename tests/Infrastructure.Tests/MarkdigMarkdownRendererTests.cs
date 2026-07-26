using Domain;
using Infrastructure.Markdown;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="MarkdigMarkdownRenderer"/>, the Markdig-backed adapter for the
/// <see cref="IMarkdownRenderer"/> port. Verifies INV-002 (deterministic rendering) and basic
/// GFM rendering behaviour.
/// </summary>
public sealed class MarkdigMarkdownRendererTests
{
    private readonly IMarkdownRenderer _renderer = new MarkdigMarkdownRenderer();

    [Fact]
    public void Render_GivenEmptyDocument_ProducesEmptyRenderedOutput()
    {
        var output = _renderer.Render(new MarkdownDocument(""));

        output.Html.ShouldBe("");
    }

    [Fact]
    public void Render_GivenHeading_ProducesHeadingHtml()
    {
        var output = _renderer.Render(new MarkdownDocument("# Heading"));

        output.Html.ShouldContain("<h1");
        output.Html.ShouldContain("Heading");
    }

    [Fact]
    public void Render_GivenSameSourceTwice_ProducesIdenticalOutput_INV002()
    {
        var document = new MarkdownDocument("# Title\n\nSome **bold** and _italic_ text.\n");

        var first = _renderer.Render(document);
        var second = _renderer.Render(document);

        second.ShouldBe(first);
    }

    [Fact]
    public void Render_GivenGfmTable_ProducesTableHtml()
    {
        const string source = "| A | B |\n| - | - |\n| 1 | 2 |\n";

        var output = _renderer.Render(new MarkdownDocument(source));

        output.Html.ShouldContain("<table");
    }

    [Fact]
    public void Render_GivenGfmTaskList_ProducesCheckboxHtml()
    {
        var output = _renderer.Render(new MarkdownDocument("- [x] done\n- [ ] todo\n"));

        output.Html.ShouldContain("type=\"checkbox\"");
    }

    [Fact]
    public void Render_GivenFootnote_ProducesReferenceAndNotesSection_INV065()
    {
        var output = _renderer.Render(new MarkdownDocument("A claim.[^a]\n\n[^a]: the note\n"));

        output.Html.ShouldContain("<sup>1</sup>");
        output.Html.ShouldContain("class=\"footnotes\"");
        output.Html.ShouldContain("the note");
    }

    [Fact]
    public void Render_GivenFootnoteReferenceWithNoDefinition_ProducesLiteralText_INV065()
    {
        var output = _renderer.Render(new MarkdownDocument("A claim.[^missing]\n"));

        output.Html.ShouldContain("[^missing]");
        output.Html.ShouldNotContain("<sup>");
    }

    [Fact]
    public void Render_GivenDefinitionList_ProducesDefinitionListHtml_INV066()
    {
        var output = _renderer.Render(new MarkdownDocument("Markdown\n:   A markup language.\n"));

        output.Html.ShouldContain("<dl>");
        output.Html.ShouldContain("<dt>Markdown</dt>");
        output.Html.ShouldContain("<dd>A markup language.</dd>");
    }

    /// <summary>
    /// A Video is written with the Image's own syntax, so it would otherwise render as an
    /// <c>&lt;img&gt;</c> that could never play. The Rendered Output shows it as a video, so an exported
    /// page carries what the editor showed (INV-069/032).
    /// </summary>
    [Fact]
    public void Render_GivenAVideoSource_ProducesAVideoElement_INV069()
    {
        var output = _renderer.Render(new MarkdownDocument("![a clip](media/demo.mp4)\n"));

        output.Html.ShouldContain("<video");
        output.Html.ShouldContain("src=\"media/demo.mp4\"");
        output.Html.ShouldContain("controls");
        output.Html.ShouldNotContain("<img");
    }

    /// <summary>The alt text stands in for a Video a browser cannot play, exactly as it does in the editor.</summary>
    [Fact]
    public void Render_GivenAVideoSource_CarriesItsAltTextAsTheFallback_INV069()
    {
        var output = _renderer.Render(new MarkdownDocument("![a clip](demo.webm)\n"));

        output.Html.ShouldContain("a clip");
    }

    [Fact]
    public void Render_GivenAnImageSource_StillProducesAnImageElement_INV069()
    {
        var output = _renderer.Render(new MarkdownDocument("![a cat](cat.png)\n"));

        output.Html.ShouldContain("<img");
        output.Html.ShouldNotContain("<video");
    }

    /// <summary>
    /// A Link to a video is a Link — the reader is being sent to the file, not shown it. Only the Image
    /// syntax makes a Video (INV-069).
    /// </summary>
    [Fact]
    public void Render_GivenALinkToAVideo_LeavesItALink_INV069()
    {
        var output = _renderer.Render(new MarkdownDocument("[the clip](demo.mp4)\n"));

        output.Html.ShouldContain("<a href=\"demo.mp4\"");
        output.Html.ShouldNotContain("<video");
    }
}
