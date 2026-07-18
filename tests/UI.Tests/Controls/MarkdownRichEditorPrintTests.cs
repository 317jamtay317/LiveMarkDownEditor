using Shouldly;
using UI.Controls;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Print on the <see cref="MarkdownRichEditor"/> (INV-034): it hands the whole document to
/// the printer — re-projected from the Markdown source, so a Folded Section's hidden body prints too —
/// and printing is not an edit.
/// </summary>
public sealed class MarkdownRichEditorPrintTests
{
    [Fact]
    public void Print_HandsTheDocumentToThePrinter_INV034()
    {
        StaThread.Run(() =>
        {
            var printer = new FakeDocumentPrinter();
            var editor = new MarkdownRichEditor
            {
                Markdown = "# Title\n\nBody text",
                DocumentPrinter = printer,
            };

            MarkdownEditingCommands.Print.Execute(parameter: null, target: editor);

            printer.PrintCount.ShouldBe(1);
            printer.PrintedText!.ShouldContain("Title");
            printer.PrintedText!.ShouldContain("Body text");
        });
    }

    [Fact]
    public void Print_PrintsTheWholeDocument_IncludingFoldedSections_INV034()
    {
        StaThread.Run(() =>
        {
            var printer = new FakeDocumentPrinter();
            var editor = new MarkdownRichEditor
            {
                Markdown = "# One\n\nAlpha body\n\n# Two\n\nBravo body",
                DocumentPrinter = printer,
            };

            // Fold every Section: a Folded Section's Section Body is removed from the visible Document.
            MarkdownEditingCommands.CollapseAllFolds.Execute(parameter: null, target: editor);

            MarkdownEditingCommands.Print.Execute(parameter: null, target: editor);

            // Print re-projects the source (INV-011: folding never changes it), so the hidden bodies
            // print — Print means the whole document, never merely the visible part (INV-034).
            printer.PrintCount.ShouldBe(1);
            printer.PrintedText!.ShouldContain("Alpha body");
            printer.PrintedText!.ShouldContain("Bravo body");
        });
    }

    [Fact]
    public void Print_DoesNotChangeTheMarkdownDocument_INV034()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "# Title\n\nBody text",
                DocumentPrinter = new FakeDocumentPrinter(),
            };

            MarkdownEditingCommands.Print.Execute(parameter: null, target: editor);

            // Printing reads the document and writes no file the editor owns — it is not an edit.
            editor.Markdown.ShouldBe("# Title\n\nBody text");
        });
    }

    [Fact]
    public void Print_WithNoPrinter_MakesNoEditAndDoesNotThrow_INV034()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title\n\nBody text" };

            MarkdownEditingCommands.Print.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("# Title\n\nBody text");
        });
    }
}
