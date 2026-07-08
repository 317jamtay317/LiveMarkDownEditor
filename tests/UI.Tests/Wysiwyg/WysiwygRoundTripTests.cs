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
}
