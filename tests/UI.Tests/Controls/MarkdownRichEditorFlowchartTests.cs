using System.Linq;
using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Flowchart Builder write-back seam on the <see cref="MarkdownRichEditor"/> (INV-053):
/// Insert replaces the Mermaid Diagram at the caret or inserts a new Code Block, Capturing canonical
/// Markdown; opening the builder asks it with the diagram at the caret; and Cancel makes no edit.
/// </summary>
public sealed class MarkdownRichEditorFlowchartTests
{
    private const string NewDiagram = "flowchart LR\n    n1[\"New\"]";

    [Fact]
    public void InsertOrReplace_WithNoDiagramAtCaret_InsertsANewBlock_INV053()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Some prose." };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);

            editor.InsertOrReplaceDiagramAtCaret(NewDiagram);

            editor.Markdown.ShouldContain("Some prose.");
            editor.Markdown.ShouldContain("```mermaid");
            editor.Markdown.ShouldContain("flowchart LR");
            editor.Markdown.ShouldContain("n1[\"New\"]");
        });
    }

    [Fact]
    public void InsertOrReplace_WhenOpenedOnADiagram_ReplacesThatDiagram_AndCapturesCanonicalMarkdown_INV053()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "```mermaid\nflowchart TD\n    oldNode\n```" };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);

            editor.InsertOrReplaceDiagramAtCaret(NewDiagram);

            editor.Markdown.ShouldContain("n1[\"New\"]");
            editor.Markdown.ShouldNotContain("oldNode"); // the old diagram was replaced, not appended
            editor.Markdown.Split("```mermaid").Length.ShouldBe(2); // exactly one mermaid block remains
        });
    }

    [Fact]
    public void InsertedDiagram_RoundTripsAsAMermaidCodeBlock_INV053()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Intro." };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);
            editor.InsertOrReplaceDiagramAtCaret(NewDiagram);

            // Move the caret into the freshly inserted diagram: its Diagram Preview source is that diagram.
            var mermaidBlock = editor.Document.Blocks.ToList()[1];
            editor.Selection.Select(mermaidBlock.ContentStart, mermaidBlock.ContentStart);

            editor.CurrentDiagramSource.ShouldBe(NewDiagram);
        });
    }

    [Fact]
    public void OpenFlowchartBuilder_AsksTheBuilderWithTheDiagramAtCaret_AndInsertsItsResult_INV053()
    {
        StaThread.Run(() =>
        {
            var builder = new StubFlowchartBuilder(NewDiagram);
            var editor = new MarkdownRichEditor
            {
                Markdown = "```mermaid\nflowchart TD\n    oldNode\n```",
                FlowchartBuilder = builder,
            };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);

            editor.OpenFlowchartBuilderAtCaret();

            builder.ReceivedExistingSource.ShouldBe("flowchart TD\n    oldNode");
            editor.Markdown.ShouldContain("n1[\"New\"]");
            editor.Markdown.ShouldNotContain("oldNode");
        });
    }

    [Fact]
    public void OpenFlowchartBuilder_WhenCancelled_MakesNoEdit_INV053()
    {
        StaThread.Run(() =>
        {
            var builder = new StubFlowchartBuilder(result: null);
            var editor = new MarkdownRichEditor { Markdown = "Just prose.", FlowchartBuilder = builder };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);
            var before = editor.Markdown;

            editor.OpenFlowchartBuilderAtCaret();

            builder.TimesAsked.ShouldBe(1);
            editor.Markdown.ShouldBe(before);
        });
    }

    [Fact]
    public void OpenFlowchartBuilder_WithNoBuilderSet_MakesNoEdit_INV053()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Just prose." };
            var before = editor.Markdown;

            editor.OpenFlowchartBuilderAtCaret();

            editor.Markdown.ShouldBe(before);
        });
    }

    [Fact]
    public void InsertOrReplace_AtEndOfDocument_LeavesALineBelow_INV055()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Intro." };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);

            editor.InsertOrReplaceDiagramAtCaret(NewDiagram);

            // A Mermaid Diagram is a Block Island: it must never be the last block, or there is no
            // line below it for the user to carry on typing in (INV-055).
            editor.Document.Blocks.LastBlock.ShouldBeOfType<Paragraph>();
        });
    }

    [Fact]
    public void InsertOrReplace_PlacesTheCaretInTextAfterTheDiagram_INV055()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Intro." };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);

            editor.InsertOrReplaceDiagramAtCaret(NewDiagram);

            // The caret must land in a text position, not inside the diagram's own container: a
            // BlockUIContainer holds a picture rather than text, so a caret in it swallows Enter.
            var caretParagraph = editor.CaretPosition.Paragraph;
            caretParagraph.ShouldNotBeNull();
            caretParagraph.ShouldBeSameAs(editor.Document.Blocks.LastBlock);
        });
    }

    [Fact]
    public void InsertOrReplace_WhenReplacingADiagram_StillLeavesALineBelow_INV055()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "```mermaid\nflowchart TD\n    oldNode\n```" };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);

            editor.InsertOrReplaceDiagramAtCaret(NewDiagram);

            editor.Document.Blocks.LastBlock.ShouldBeOfType<Paragraph>();
            editor.CaretPosition.Paragraph.ShouldNotBeNull();
        });
    }

    [Fact]
    public void ADocumentEndingInADiagram_ProjectsALineBelowIt_INV055()
    {
        StaThread.Run(() =>
        {
            // Reopening a saved document must leave the user just as able to type below the diagram
            // as inserting one did: the Block Island rule is about the document, not only the edit.
            var editor = new MarkdownRichEditor
            {
                Markdown = "Intro.\n\n```mermaid\nflowchart LR\n    n1[\"A\"]\n```",
            };

            editor.Document.Blocks.LastBlock.ShouldBeOfType<Paragraph>();
        });
    }

    [Fact]
    public void ADocumentEndingInADiagram_CapturesUnchanged_INV055()
    {
        StaThread.Run(() =>
        {
            const string source = "Intro.\n\n```mermaid\nflowchart LR\n    n1[\"A\"]\n```";
            var editor = new MarkdownRichEditor { Markdown = source };

            // The trailing line is a caret affordance, not content: it must not make a freshly
            // opened document Capture as something other than what was loaded (INV-005).
            editor.Capture().ShouldBe(source);
        });
    }

    [Fact]
    public void InsertOrReplace_AtEndOfDocument_StillRoundTrips_INV055()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Intro." };
            var block = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(block.ContentStart, block.ContentStart);
            editor.InsertOrReplaceDiagramAtCaret(NewDiagram);

            // The trailing paragraph must cost the Round-Trip nothing (INV-005): re-projecting what
            // was Captured and Capturing it again must yield the same source. Capture() is the real
            // check — the Markdown property would merely echo the string it was set to.
            var captured = editor.Markdown;
            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }
}
