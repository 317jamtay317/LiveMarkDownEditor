using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="MarkdownToFlowDocumentProjector"/> that guard the Code Shading contract: code
/// is tagged but given no <c>Background</c>, so the shade lives entirely in the Code Shading overlay
/// and recolouring it cannot re-format the document (INV-017).
/// </summary>
public sealed class MarkdownToFlowDocumentProjectorTests
{
    private static readonly MarkdownToFlowDocumentProjector Projector = new();

    [Fact]
    public void CodeElements_CarryNoBackground_SoShadingCannotReflow_INV017()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Call `Compute()` now.\n\n```\nvar x = 1;\n```");

            // The Code Block paragraph.
            var codeBlock = document.Blocks.OfType<Paragraph>()
                .Single(paragraph => paragraph.Inlines.OfType<Run>().Any(run => run.Text.Contains("var x = 1;")));
            codeBlock.ReadLocalValue(TextElement.BackgroundProperty).ShouldBe(DependencyProperty.UnsetValue);

            // The inline Code Span run.
            var codeSpan = document.Blocks.OfType<Paragraph>()
                .SelectMany(paragraph => paragraph.Inlines.OfType<Run>())
                .Single(run => run.Text == "Compute()");
            codeSpan.ReadLocalValue(TextElement.BackgroundProperty).ShouldBe(DependencyProperty.UnsetValue);
        });
    }

    [Fact]
    public void Project_RecordsEachBlocksSourceLineRange_INV060()
    {
        StaThread.Run(() =>
        {
            //     0: # Title
            //     1:
            //     2: Body paragraph.
            //     3:
            //     4: ```
            //     5: code
            //     6: ```
            var document = Projector.Project("# Title\n\nBody paragraph.\n\n```\ncode\n```");

            var ranges = document.Blocks.Select(SourceLines.GetRange).ToList();

            ranges.ShouldAllBe(range => range != null);
            ranges.Select(range => range!.StartLine).ShouldBe([0, 2, 4]);

            // A single-line block ends where it starts; the fenced Code Block spans its fences.
            ranges[0]!.EndLine.ShouldBe(0);
            ranges[1]!.EndLine.ShouldBe(2);
            ranges[2]!.EndLine.ShouldBeGreaterThanOrEqualTo(5);
        });
    }

    [Fact]
    public void Project_GivenTheSameSource_RecordsTheSameSourceLineRanges_INV060()
    {
        StaThread.Run(() =>
        {
            const string Markdown = "alpha\n\n- one\n- two\n\n> quoted";

            var first = Projector.Project(Markdown).Blocks.Select(SourceLines.GetRange).ToList();
            var second = Projector.Project(Markdown).Blocks.Select(SourceLines.GetRange).ToList();

            second.ShouldBe(first);
        });
    }
}
