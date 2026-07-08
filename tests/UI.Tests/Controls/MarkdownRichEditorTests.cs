using System.Windows;
using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// End-to-end tests for the <see cref="MarkdownRichEditor"/> custom control: assigning
/// <see cref="MarkdownRichEditor.Markdown"/> Projects it into the Visual Document, and editing the
/// Visual Document Captures it back into <see cref="MarkdownRichEditor.Markdown"/>.
/// </summary>
public sealed class MarkdownRichEditorTests
{
    [Fact]
    public void SettingMarkdown_ProjectsFormattedVisualDocument_WithoutRawSyntax()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title" };

            var visibleText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;

            visibleText.ShouldContain("Title");
            visibleText.ShouldNotContain("#");
        });
    }

    [Fact]
    public void EditingTheVisualDocument_CapturesBackToMarkdown()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor();

            // Simulate the user applying bold in the editor (property-based, as the toolbar does).
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("hello ") { });
            paragraph.Inlines.Add(new Run("world") { FontWeight = FontWeights.Bold });
            editor.Document = new FlowDocument(paragraph);

            editor.Markdown.ShouldBe("hello **world**");
        });
    }

    private const string TwoSectionDocument =
        "# Alpha\n\nAlpha body.\n\n## Alpha one\n\nSub body.\n\n# Beta\n\nBeta body.";

    [Fact]
    public void Fold_HidesTheSectionBody_ButKeepsTheSectionHeadingVisible()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var blockCountBefore = editor.Document.Blocks.Count;
            var alphaHeading = editor.Document.Blocks.FirstBlock!;

            editor.Fold(alphaHeading);

            editor.IsFolded(alphaHeading).ShouldBeTrue();
            // Alpha's body (Alpha body, ## Alpha one, Sub body) is hidden; Beta's section is intact.
            editor.Document.Blocks.Count.ShouldBeLessThan(blockCountBefore);
            var visibleText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
            visibleText.ShouldContain("Alpha");
            visibleText.ShouldNotContain("Alpha body");
        });
    }

    [Fact]
    public void Fold_DoesNotChangeCapturedMarkdown_INV011()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var unfolded = editor.Capture();

            editor.Fold(editor.Document.Blocks.FirstBlock!);

            // INV-011: a Fold is view-only — capturing yields identical Markdown whether or not any
            // Section is Folded, because Folded Section Bodies are retained and captured in place.
            editor.Capture().ShouldBe(unfolded);
        });
    }

    [Fact]
    public void ExpandAllFolds_RestoresTheHiddenSectionBodies()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var blockCountBefore = editor.Document.Blocks.Count;
            editor.Fold(editor.Document.Blocks.FirstBlock!);

            editor.ExpandAllFolds();

            editor.Document.Blocks.Count.ShouldBe(blockCountBefore);
            editor.IsFolded(editor.Document.Blocks.FirstBlock!).ShouldBeFalse();
        });
    }

    [Fact]
    public void ToggleFoldAtCaret_WithCaretInHeading_FoldsThatSection()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var heading = (Paragraph)editor.Document.Blocks.FirstBlock!;
            editor.CaretPosition = heading.ContentStart;

            editor.ToggleFoldAtCaret();

            editor.IsFolded(heading).ShouldBeTrue();
        });
    }

    [Fact]
    public void ToggleFoldAtCaret_WithCaretInSectionBody_FoldsEnclosingSection()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var heading = (Paragraph)editor.Document.Blocks.FirstBlock!;
            var bodyParagraph = (Paragraph)editor.Document.Blocks.ToList()[1];
            editor.CaretPosition = bodyParagraph.ContentStart;

            editor.ToggleFoldAtCaret();

            editor.IsFolded(heading).ShouldBeTrue();
        });
    }

    [Fact]
    public void ToggleFoldCommand_ExecutedAgainstEditor_FoldsSectionAtCaret()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var heading = (Paragraph)editor.Document.Blocks.FirstBlock!;
            editor.CaretPosition = heading.ContentStart;

            MarkdownEditingCommands.ToggleFold.Execute(parameter: null, target: editor);

            editor.IsFolded(heading).ShouldBeTrue();
        });
    }

    [Fact]
    public void CollapseAllFolds_FoldsEveryTopLevelSection()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var alphaHeading = editor.Document.Blocks.FirstBlock!;

            editor.CollapseAllFolds();

            // Folding the outer Sections hides everything but the top-level Section Headings.
            editor.IsFolded(alphaHeading).ShouldBeTrue();
            var betaHeading = editor.Document.Blocks.LastBlock!;
            editor.IsFolded(betaHeading).ShouldBeTrue();
            editor.Document.Blocks.Count.ShouldBe(2);
            var visibleText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
            visibleText.ShouldContain("Alpha");
            visibleText.ShouldContain("Beta");
            visibleText.ShouldNotContain("Alpha body");
            visibleText.ShouldNotContain("Sub body");
            visibleText.ShouldNotContain("Beta body");
        });
    }

    [Fact]
    public void CollapseAllFolds_DoesNotChangeCapturedMarkdown_INV011()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var unfolded = editor.Capture();

            editor.CollapseAllFolds();

            // INV-011: Collapse All is a Fold across every Section — still view-only.
            editor.Capture().ShouldBe(unfolded);
        });
    }

    [Fact]
    public void ExpandAllFolds_AfterCollapseAll_RestoresEveryBlock()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var blockCountBefore = editor.Document.Blocks.Count;
            editor.CollapseAllFolds();

            editor.ExpandAllFolds();

            editor.Document.Blocks.Count.ShouldBe(blockCountBefore);
        });
    }

    [Fact]
    public void CollapseAllFoldsCommand_ExecutedAgainstEditor_FoldsAllSections()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };

            MarkdownEditingCommands.CollapseAllFolds.Execute(parameter: null, target: editor);

            editor.IsFolded(editor.Document.Blocks.FirstBlock!).ShouldBeTrue();
            editor.IsFolded(editor.Document.Blocks.LastBlock!).ShouldBeTrue();
        });
    }

    [Fact]
    public void IsSectionHeading_ForHeadingBlock_ReturnsTrue()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title" };

            editor.IsSectionHeading(editor.Document.Blocks.FirstBlock!).ShouldBeTrue();
        });
    }

    [Fact]
    public void IsSectionHeading_ForNonHeadingBlock_ReturnsFalse()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Just a paragraph." };

            editor.IsSectionHeading(editor.Document.Blocks.FirstBlock!).ShouldBeFalse();
        });
    }

    [Fact]
    public void Fold_NonHeadingBlock_Throws()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Just a paragraph." };

            // Only a Section Heading can be Folded (INV-011).
            Should.Throw<ArgumentException>(() => editor.Fold(editor.Document.Blocks.FirstBlock!));
        });
    }
}
