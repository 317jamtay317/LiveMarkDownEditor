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
}
