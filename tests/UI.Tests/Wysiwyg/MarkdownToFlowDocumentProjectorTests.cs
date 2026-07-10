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
}
