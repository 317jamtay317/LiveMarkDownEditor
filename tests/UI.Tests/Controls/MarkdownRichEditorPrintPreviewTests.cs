using Shouldly;
using UI.Controls;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Print Preview on the <see cref="MarkdownRichEditor"/> (INV-061): it hands the whole
/// document — re-projected from the Markdown source the way Print is (INV-034) — and the one
/// editor-wide Page Setup to the preview, and previewing is not an edit.
/// </summary>
public sealed class MarkdownRichEditorPrintPreviewTests
{
    [Fact]
    public void PrintPreview_HandsTheDocumentAndSetupToThePreview_INV061()
    {
        StaThread.Run(() =>
        {
            var preview = new FakePrintPreview();
            var setup = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Narrow));
            var editor = new MarkdownRichEditor
            {
                Markdown = "# Title\n\nBody text",
                PrintPreview = preview,
                PageSetup = setup,
            };

            MarkdownEditingCommands.PrintPreview.Execute(parameter: null, target: editor);

            preview.ShowCount.ShouldBe(1);
            preview.PreviewedText!.ShouldContain("Title");
            preview.PreviewedSetup.ShouldBe(setup);
        });
    }

    [Fact]
    public void PrintPreview_PreviewsTheWholeDocument_IncludingFoldedSections_INV061()
    {
        StaThread.Run(() =>
        {
            var preview = new FakePrintPreview();
            var editor = new MarkdownRichEditor
            {
                Markdown = "# One\n\nAlpha body\n\n# Two\n\nBravo body",
                PrintPreview = preview,
            };

            MarkdownEditingCommands.CollapseAllFolds.Execute(parameter: null, target: editor);

            MarkdownEditingCommands.PrintPreview.Execute(parameter: null, target: editor);

            // The preview shows the very pages Print would produce (INV-034): the whole document,
            // never merely the visible part.
            preview.PreviewedText!.ShouldContain("Alpha body");
            preview.PreviewedText!.ShouldContain("Bravo body");
        });
    }

    [Fact]
    public void PrintPreview_DoesNotChangeTheMarkdownDocument_INV061()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "# Title\n\nBody text",
                PrintPreview = new FakePrintPreview(),
            };

            MarkdownEditingCommands.PrintPreview.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("# Title\n\nBody text");
        });
    }

    [Fact]
    public void PrintPreview_WithNoPreview_MakesNoEditAndDoesNotThrow_INV061()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title\n\nBody text" };

            MarkdownEditingCommands.PrintPreview.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("# Title\n\nBody text");
        });
    }
}
