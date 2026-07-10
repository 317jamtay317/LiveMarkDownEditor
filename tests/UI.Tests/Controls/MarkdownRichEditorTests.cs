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
    public void SettingMarkdown_BackToEmptyAfterContent_ClearsTheVisualDocument()
    {
        StaThread.Run(() =>
        {
            // Reproduces closing a document tab back onto the empty "Untitled" tab: the shared editor
            // is re-bound from loaded content to an empty session and must re-Project to empty, not
            // keep showing the closed document.
            var editor = new MarkdownRichEditor { Markdown = "# Loaded" };

            editor.Markdown = string.Empty;

            var visibleText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
            visibleText.ShouldNotContain("Loaded");
            editor.Outline.ShouldBeEmpty();
        });
    }

    [Fact]
    public void SwitchingMarkdown_BetweenTwoDocuments_ShowsTheNewContent()
    {
        StaThread.Run(() =>
        {
            // Switching tabs re-binds the shared editor; the visible document must follow the source.
            var editor = new MarkdownRichEditor { Markdown = "# First" };

            editor.Markdown = "# Second";

            var visibleText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
            visibleText.ShouldContain("Second");
            visibleText.ShouldNotContain("First");
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

    [Fact]
    public void AssigningSource_ProjectsVisualDocument_AndVisualEdit_UpdatesSource_INV013()
    {
        StaThread.Run(() =>
        {
            // The Source Panel and the Visual Document are two views of one Markdown Document, kept in
            // sync through the shared Markdown source (INV-013). This locks that contract at the point
            // both views bind to: MarkdownRichEditor.Markdown.
            var editor = new MarkdownRichEditor();

            // Source Panel → Visual Document: assigning raw source Projects a formatted, syntax-free view.
            editor.Markdown = "# Heading";
            var visibleText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
            visibleText.ShouldContain("Heading");
            visibleText.ShouldNotContain("#");

            // Visual Document → Source Panel: editing the visual document Captures back into the source
            // the Source Panel shows, without echoing back to re-edit the visual document.
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("plain "));
            paragraph.Inlines.Add(new Run("strong") { FontWeight = FontWeights.Bold });
            editor.Document = new FlowDocument(paragraph);

            editor.Markdown.ShouldBe("plain **strong**");
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

    [Fact]
    public void Outline_ListsSectionHeadingsInDocumentOrder_WithLevelsAndText()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };

            var outline = editor.Outline;

            // Alpha (1) / Alpha one (2) / Beta (1), in document order, non-heading blocks excluded.
            outline.Count.ShouldBe(3);
            outline[0].Level.ShouldBe(1);
            outline[0].Text.ShouldBe("Alpha");
            outline[1].Level.ShouldBe(2);
            outline[1].Text.ShouldBe("Alpha one");
            outline[2].Level.ShouldBe(1);
            outline[2].Text.ShouldBe("Beta");
        });
    }

    [Fact]
    public void Outline_ListsHeadingsInsideFoldedSections_INV012()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            editor.Fold(editor.Document.Blocks.FirstBlock!); // Fold Alpha, hiding "## Alpha one".

            // The Outline lists every Section Heading, including ones inside a Folded Section Body.
            editor.Outline.Select(entry => entry.Text).ShouldBe(["Alpha", "Alpha one", "Beta"]);
        });
    }

    [Fact]
    public void Outline_IsViewOnly_DoesNotChangeCapturedMarkdown_INV012()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var before = editor.Capture();

            _ = editor.Outline;

            editor.Capture().ShouldBe(before);
        });
    }

    [Fact]
    public void Navigate_SelectsTheSectionHeading()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var beta = editor.Outline.First(entry => entry.Text == "Beta");

            editor.Navigate(beta);

            var selected = editor.Selection.Text;
            selected.ShouldContain("Beta");
            selected.ShouldNotContain("Beta body");
        });
    }

    [Fact]
    public void Navigate_ToHeadingInFoldedSection_UnfoldsAndRevealsIt()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var alphaHeading = (Paragraph)editor.Document.Blocks.FirstBlock!;
            editor.Fold(alphaHeading); // "## Alpha one" is now hidden inside Alpha's folded body.

            editor.Navigate(editor.Outline.First(entry => entry.Text == "Alpha one"));

            // Navigating a hidden heading Unfolds its enclosing Section and selects it.
            editor.IsFolded(alphaHeading).ShouldBeFalse();
            editor.Selection.Text.ShouldContain("Alpha one");
        });
    }

    [Fact]
    public void Navigate_DoesNotChangeCapturedMarkdown_INV012()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var before = editor.Capture();
            editor.Fold(editor.Document.Blocks.FirstBlock!);

            editor.Navigate(editor.Outline.First(entry => entry.Text == "Alpha one"));

            // INV-012: Navigating (even when it Unfolds) never changes the Markdown Document.
            editor.Capture().ShouldBe(before);
        });
    }

    [Fact]
    public void Find_HighlightsEveryOccurrence_OfTheQuery()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };

            editor.IsFindActive = true;
            editor.FindQuery = "Alpha";

            // "Alpha", "Alpha body", and "## Alpha one" — three occurrences of the query.
            editor.MatchCount.ShouldBe(3);
            editor.MatchSummary.ShouldBe("1 of 3");
        });
    }

    [Fact]
    public void FindNext_MovesTheCurrentMatch_AndWrapsAround()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            editor.IsFindActive = true;
            editor.FindQuery = "Alpha"; // 3 matches, Current Match starts at "1 of 3".

            MarkdownEditingCommands.FindNext.Execute(parameter: null, target: editor);
            editor.MatchSummary.ShouldBe("2 of 3");

            MarkdownEditingCommands.FindNext.Execute(parameter: null, target: editor);
            MarkdownEditingCommands.FindNext.Execute(parameter: null, target: editor);

            // Past the last Match, Find Next wraps back to the first.
            editor.MatchSummary.ShouldBe("1 of 3");
        });
    }

    [Fact]
    public void Find_WithNoOccurrence_ReportsNoResults()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            editor.IsFindActive = true;

            editor.FindQuery = "Gamma";

            editor.MatchCount.ShouldBe(0);
            editor.MatchSummary.ShouldBe("No results");
        });
    }

    [Fact]
    public void Find_DoesNotChangeCapturedMarkdown_INV016()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var before = editor.Capture();

            editor.IsFindActive = true;
            editor.FindQuery = "Alpha";
            MarkdownEditingCommands.FindNext.Execute(parameter: null, target: editor);

            // INV-016: Find highlights, counts, and navigates Matches — but never edits the source.
            editor.MatchCount.ShouldBeGreaterThan(0);
            editor.Capture().ShouldBe(before);
        });
    }

    [Fact]
    public void CodeShading_DoesNotChangeCapturedMarkdown_INV017()
    {
        StaThread.Run(() =>
        {
            const string withCode = "Call `Compute()` now.\n\n```\nvar x = 1;\n```";
            var editor = new MarkdownRichEditor { Markdown = withCode };
            var before = editor.Capture();

            // Scanning the Visual Document for its Code Regions (what the Code Shading overlay does) is
            // view-only — it never edits the source (INV-017).
            var regions = UI.Wysiwyg.CodeShadingScanner.Scan(editor.Document);

            regions.ShouldNotBeEmpty();
            editor.Capture().ShouldBe(before);
        });
    }

    [Fact]
    public void CurrentSection_ReflectsTheHeadingEnclosingTheCaret()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoSectionDocument };
            var subBody = (Paragraph)editor.Document.Blocks.ToList()[3]; // the "Sub body" paragraph.
            editor.CaretPosition = subBody.ContentStart;

            // The caret sits under "## Alpha one", the nearest preceding Section Heading.
            editor.CurrentSection.ShouldNotBeNull();
            editor.CurrentSection!.Text.ShouldBe("Alpha one");
        });
    }
}
