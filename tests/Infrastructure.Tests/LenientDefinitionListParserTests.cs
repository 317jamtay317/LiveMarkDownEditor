using Domain;
using Infrastructure.Markdown;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="LenientDefinitionListParser"/>: a Definition Description is read from a
/// marker followed by at least one space, while a marker with no space at all stays ordinary
/// paragraph text — which is what keeps a table's alignment row, a <c>~~~</c> code fence, and a
/// <c>:shortcode:</c> from being mistaken for a Definition (INV-066).
/// </summary>
public sealed class LenientDefinitionListParserTests
{
    private readonly IMarkdownRenderer _renderer = new MarkdigMarkdownRenderer();

    private string Render(string markdown) => _renderer.Render(new MarkdownDocument(markdown)).Html;

    [Theory]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData("   ")]
    [InlineData("    ")]
    public void Render_GivenAColonAndAtLeastOneSpace_ProducesADefinitionList_INV066(string spacing)
    {
        var html = Render($"Markdown\n:{spacing}A markup language.\n");

        html.ShouldContain("<dl>");
        html.ShouldContain("<dt>Markdown</dt>");
        html.ShouldContain("<dd>A markup language.</dd>");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Render_GivenATildeAndAtLeastOneSpace_ProducesADefinitionList_INV066(string spacing)
    {
        var html = Render($"Markdown\n~{spacing}A markup language.\n");

        html.ShouldContain("<dl>");
        html.ShouldContain("<dd>A markup language.</dd>");
    }

    [Fact]
    public void Render_GivenAColonWithNoSpace_ProducesNoDefinitionList_INV066()
    {
        var html = Render("Markdown\n:A markup language.\n");

        html.ShouldNotContain("<dl>");
    }

    [Fact]
    public void Render_GivenAnEmojiShortcode_ProducesNoDefinitionList_INV066()
    {
        var html = Render("Markdown\n:smile: is a shortcode.\n");

        html.ShouldNotContain("<dl>");
    }

    [Fact]
    public void Render_GivenATableAlignmentRow_StillProducesATable_INV066()
    {
        var html = Render("| Feature | Status |\n|:---|---:|\n| Lists | shipped |\n");

        html.ShouldContain("<table>");
        html.ShouldNotContain("<dl>");
    }

    [Fact]
    public void Render_GivenATildeFencedCodeBlock_StillProducesACodeBlock_INV066()
    {
        var html = Render("A paragraph.\n\n~~~\nnot a definition\n~~~\n");

        html.ShouldContain("<code>");
        html.ShouldNotContain("<dl>");
    }

    [Fact]
    public void Render_GivenAOneSpaceDescriptionInsideABlockQuote_ProducesADefinitionList_INV066()
    {
        // The leniency is a parser rule, so it holds inside a container, not only at the margin.
        var html = Render("> Markdown\n> : A markup language.\n");

        html.ShouldContain("<blockquote>");
        html.ShouldContain("<dd>A markup language.</dd>");
    }

    [Fact]
    public void Render_GivenTwoOneSpaceItems_ProducesBothDefinitions_INV066()
    {
        var html = Render("Fast-forward\n: Already an ancestor.\n\nSquash\n: One new commit.\n");

        html.ShouldContain("<dd>Already an ancestor.</dd>");
        html.ShouldContain("<dd>One new commit.</dd>");
    }

    [Fact]
    public void Render_GivenAOneSpaceDescriptionWithNoParagraphAbove_ProducesNoDefinitionList_INV066()
    {
        // A Term is required: a marker opening a block with nothing above it is still a paragraph.
        var html = Render("# Heading\n\n: orphaned description\n");

        html.ShouldNotContain("<dl>");
    }
}
