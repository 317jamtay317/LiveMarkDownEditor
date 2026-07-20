using System.Windows.Documents;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="MermaidDiagram"/>: it locates the Mermaid Diagram the caret is within — a
/// Code Block whose language is <c>mermaid</c> — and returns its source for the Diagram Preview. It
/// is pure and view-only: it reads the Visual Document and never changes it (INV-047).
/// </summary>
public sealed class MermaidDiagramTests
{
    private static readonly MarkdownToFlowDocumentProjector Projector = new();

    private static TextPointer CaretInFirstBlock(string markdown) =>
        Projector.Project(markdown).Blocks.FirstBlock!.ContentStart;

    [Fact]
    public void SourceAt_WhenCaretIsInAMermaidBlock_ReturnsItsSource_INV047()
    {
        StaThread.Run(() =>
        {
            var caret = CaretInFirstBlock("```mermaid\ngraph TD\n  A-->B\n```");

            MermaidDiagram.SourceAt(caret).ShouldBe("graph TD\n  A-->B");
        });
    }

    [Fact]
    public void SourceAt_MatchesTheLanguageCaseInsensitively_INV047()
    {
        StaThread.Run(() =>
        {
            var caret = CaretInFirstBlock("```Mermaid\npie title Pets\n```");

            MermaidDiagram.SourceAt(caret).ShouldBe("pie title Pets");
        });
    }

    [Fact]
    public void SourceAt_WhenCaretIsInAnotherCodeBlock_ReturnsNull_INV047()
    {
        StaThread.Run(() =>
        {
            var caret = CaretInFirstBlock("```csharp\nvar x = 1;\n```");

            MermaidDiagram.SourceAt(caret).ShouldBeNull();
        });
    }

    [Fact]
    public void SourceAt_WhenCaretIsInProse_ReturnsNull_INV047()
    {
        StaThread.Run(() =>
        {
            var caret = CaretInFirstBlock("Just ordinary prose, not a diagram.");

            MermaidDiagram.SourceAt(caret).ShouldBeNull();
        });
    }

    [Fact]
    public void SourceAt_WhenCaretIsNull_ReturnsNull_INV047()
    {
        MermaidDiagram.SourceAt(null).ShouldBeNull();
    }

    [Theory]
    [InlineData("mermaid", true)]
    [InlineData("Mermaid", true)]
    [InlineData("  mermaid  ", true)]
    [InlineData("csharp", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMermaidLanguage_JudgesTheInfoString_INV047(string? language, bool expected)
    {
        MermaidDiagram.IsMermaidLanguage(language).ShouldBe(expected);
    }

    [Fact]
    public void MermaidBlock_ProjectsToADiagramBlock_WhoseSourceIsReadBack_INV047()
    {
        StaThread.Run(() =>
        {
            var block = Projector.Project("```mermaid\nflowchart TD\n    a[\"A\"]\n```").Blocks.FirstBlock;

            MermaidDiagram.SourceOfBlock(block).ShouldBe("flowchart TD\n    a[\"A\"]");
        });
    }

    [Fact]
    public void RoundTrip_OfAMermaidBlock_EmitsTheFencedBlock_INV047()
    {
        StaThread.Run(() =>
        {
            const string source = "```mermaid\nflowchart TD\n    a[\"Start\"]\n    a --> b\n```";

            var captured = new FlowDocumentToMarkdownCapturer().Capture(Projector.Project(source));

            captured.ShouldBe(source);
        });
    }
}
