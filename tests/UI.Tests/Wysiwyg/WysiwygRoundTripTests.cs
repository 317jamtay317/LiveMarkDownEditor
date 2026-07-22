using System.Windows;
using System.Windows.Documents;
using Domain;
using Infrastructure.Markdown;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Round-trip tests for the WYSIWYG projection: Project (Markdown -> Visual Document) followed by
/// Capture (Visual Document -> Markdown). Covers INV-004 (semantic preservation, verified against
/// the HTML renderer oracle) and INV-005 (Capture idempotency), plus the core WYSIWYG guarantee
/// that the Visual Document shows formatting, never raw Markdown syntax.
/// </summary>
public sealed class WysiwygRoundTripTests
{
    private static string RoundTrip(string markdown)
    {
        var projector = new MarkdownToFlowDocumentProjector();
        var capturer = new FlowDocumentToMarkdownCapturer();
        return capturer.Capture(projector.Project(markdown));
    }

    [Theory]
    [InlineData("# Heading")]
    [InlineData("## Sub heading")]
    [InlineData("###### Deep heading")]
    [InlineData("A plain paragraph.")]
    [InlineData("Text with **bold** word.")]
    [InlineData("Text with *italic* word.")]
    [InlineData("Text with ~~struck~~ word.")]
    [InlineData("Text with `code` word.")]
    [InlineData("Mixed **bold** and *italic* and `code` and ~~gone~~.")]
    [InlineData("- One\n- Two\n- Three")]
    [InlineData("- Item with **bold** and `code`")]
    [InlineData("1. First\n2. Second\n3. Third")]
    [InlineData("## Milestones\n\n- Milestone 1, installation flow\n- Milestone 2, background agent\n- Milestone 3, diagnostics dashboard")]
    [InlineData("See [Anthropic](https://anthropic.com) here.")]
    [InlineData("Visit https://example.com now.")]
    [InlineData("![alt text](https://x/y.png)")]
    [InlineData("> quoted text")]
    [InlineData("> line one\n>\n> line two")]
    [InlineData("```\ncode line\n```")]
    [InlineData("```csharp\nvar x = 1;\n```")]
    [InlineData("    indented code")]
    [InlineData("above\n\n---\n\nbelow")]
    [InlineData("| A | B |\n| --- | --- |\n| 1 | 2 |")]
    [InlineData("| Left | Right |\n| :--- | ---: |\n| a | b |")]
    [InlineData("- [ ] todo\n- [x] done")]
    [InlineData("line one  \nline two")]
    public void RoundTrip_PreservesSemantics_INV004(string markdown)
    {
        var renderer = new MarkdigMarkdownRenderer();

        StaThread.Run(() =>
        {
            var captured = RoundTrip(markdown);

            renderer.Render(new MarkdownDocument(captured))
                .ShouldBe(renderer.Render(new MarkdownDocument(markdown)));
        });
    }

    [Theory]
    [InlineData("# Heading")]
    [InlineData("Mixed **bold** and *italic* and `code` and ~~gone~~.")]
    [InlineData("- One\n- Two\n- Three")]
    [InlineData("1. First\n2. Second")]
    [InlineData("See [Anthropic](https://anthropic.com) here.")]
    [InlineData("> quoted text")]
    [InlineData("```csharp\nvar x = 1;\n```")]
    [InlineData("| A | B |\n| --- | --- |\n| 1 | 2 |")]
    [InlineData("- [ ] todo\n- [x] done")]
    public void RoundTrip_IsIdempotent_INV005(string markdown)
    {
        StaThread.Run(() =>
        {
            var once = RoundTrip(markdown);
            var twice = RoundTrip(once);

            twice.ShouldBe(once);
        });
    }

    [Fact]
    public void Project_Bold_ShowsFormattedTextNotRawSyntax()
    {
        StaThread.Run(() =>
        {
            var document = new MarkdownToFlowDocumentProjector().Project("a **bold** c");

            var visibleText = new TextRange(document.ContentStart, document.ContentEnd).Text;

            visibleText.ShouldContain("bold");
            visibleText.ShouldNotContain("**");
        });
    }

    [Fact]
    public void Capture_Heading_ProducesCanonicalHashSyntax()
    {
        StaThread.Run(() => RoundTrip("#    Title").ShouldBe("# Title"));
    }

    [Fact]
    public void Capture_PropertyBasedBold_AsAppliedByTheToolbar_ProducesBoldSyntax()
    {
        StaThread.Run(() =>
        {
            // EditingCommands.ToggleBold sets FontWeight on a Run rather than wrapping it in a
            // Bold element; Capture must still recognise it as bold.
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("plain "));
            paragraph.Inlines.Add(new Run("strong") { FontWeight = FontWeights.Bold });
            var document = new FlowDocument(paragraph);

            new FlowDocumentToMarkdownCapturer().Capture(document).ShouldBe("plain **strong**");
        });
    }

    [Fact]
    public void Capture_CodeSpanWithSurroundingWhitespace_KeepsTheBackticksAgainstItsText_INV018()
    {
        StaThread.Run(() =>
        {
            // A Code Span's backticks hug their text for the same reason an emphasis delimiter does:
            // `fast `now shades the space as code and leaves the next word with no separator, so the
            // space is hoisted out from between them.
            RoundTrip("make this `fast `now").ShouldBe("make this `fast` now");
        });
    }

    [Fact]
    public void Capture_CodeSpanOfWhitespaceAlone_KeepsItsBackticks_INV018()
    {
        StaThread.Run(() =>
        {
            // Unlike emphasis, a Code Span *can* be nothing but whitespace — hoisting it out would
            // leave empty backticks, so a blank span keeps the delimiters it came in with.
            RoundTrip("a ` ` b").ShouldBe("a ` ` b");
        });
    }

    [Fact]
    public void Project_BodyParagraph_UsesTightBlockSpacing()
    {
        StaThread.Run(() =>
        {
            var document = new MarkdownToFlowDocumentProjector().Project("A plain paragraph.");

            var paragraph = (Paragraph)document.Blocks.FirstBlock;

            paragraph.Margin.ShouldBe(new Thickness(0, 0, 0, 6));
        });
    }

    [Fact]
    public void Project_Heading_UsesHeadingBlockSpacing()
    {
        StaThread.Run(() =>
        {
            var document = new MarkdownToFlowDocumentProjector().Project("# Heading");

            var heading = (Paragraph)document.Blocks.FirstBlock;

            heading.Margin.ShouldBe(new Thickness(0, 12, 0, 4));
        });
    }

    [Fact]
    public void Project_UnorderedList_ShowsEachItemsText()
    {
        StaThread.Run(() =>
        {
            var document = new MarkdownToFlowDocumentProjector()
                .Project("## Milestones\n\n- Milestone 1\n- Milestone 2\n- Milestone 3");

            var visibleText = new TextRange(document.ContentStart, document.ContentEnd).Text;

            visibleText.ShouldContain("Milestone 1");
            visibleText.ShouldContain("Milestone 2");
            visibleText.ShouldContain("Milestone 3");
        });
    }

    [Fact]
    public void Project_UnorderedList_ProducesBulletedList()
    {
        StaThread.Run(() =>
        {
            var document = new MarkdownToFlowDocumentProjector().Project("- One\n- Two");

            var list = document.Blocks.OfType<List>().Single();

            list.MarkerStyle.ShouldBe(TextMarkerStyle.Disc);
            list.ListItems.Count.ShouldBe(2);
        });
    }

    [Fact]
    public void Project_OrderedList_ProducesNumberedList()
    {
        StaThread.Run(() =>
        {
            var document = new MarkdownToFlowDocumentProjector().Project("1. One\n2. Two");

            var list = document.Blocks.OfType<List>().Single();

            list.MarkerStyle.ShouldBe(TextMarkerStyle.Decimal);
            list.ListItems.Count.ShouldBe(2);
        });
    }

    [Fact]
    public void Project_List_ShowsTextNotRawDashSyntax()
    {
        StaThread.Run(() =>
        {
            var document = new MarkdownToFlowDocumentProjector().Project("- Item one");

            var visibleText = new TextRange(document.ContentStart, document.ContentEnd).Text;

            visibleText.ShouldContain("Item one");
            visibleText.ShouldNotContain("- Item");
        });
    }
}
