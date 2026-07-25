using System.Linq;
using System.Windows.Documents;
using Infrastructure.Markdown;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests that the <see cref="MarkdownRichEditor"/> colors its Code Blocks through the
/// <c>ISyntaxHighlighter</c> port, re-colors a Code Block the user is typing in, and that none of
/// it is an edit — the Captured Markdown is identical throughout, the caret stays put, and no undo
/// entry is added (INV-064).
/// </summary>
public sealed class MarkdownRichEditorSyntaxHighlightingTests
{
    private const string CodeDocument = "```csharp\n// note\nconst int N = 42;\n```";

    private static ColorCodeSyntaxHighlighter Highlighter => new();

    private static Paragraph CodeBlockOf(MarkdownRichEditor editor) =>
        CodeShadingScanner.Scan(editor.Document).Single(region => region.IsBlock).Start.Paragraph!;

    [Fact]
    public void SettingMarkdown_ColorsTheCodeBlock_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter };

            editor.Markdown = CodeDocument;

            var runs = CodeBlockOf(editor).Inlines.OfType<Run>().Select(run => run.Text).ToList();
            runs.ShouldContain("// note");
            runs.ShouldContain("const");
            runs.ShouldContain("42");
        });
    }

    [Fact]
    public void SettingMarkdown_ColoringDoesNotChangeTheMarkdown_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter };

            editor.Markdown = CodeDocument;

            editor.Markdown.ShouldBe(CodeDocument);
        });
    }

    [Fact]
    public void WithNoHighlighterBound_TheCodeBlockStillShowsItsCode_INV064()
    {
        StaThread.Run(() =>
        {
            // The port binds after the editor is constructed; a document projected before it arrives
            // must still show its code, uncolored, rather than fail or come up empty.
            var editor = new MarkdownRichEditor { Markdown = CodeDocument };

            editor.Markdown.ShouldBe(CodeDocument);
            new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text
                .ShouldContain("const int N = 42;");
        });
    }

    [Fact]
    public void BindingTheHighlighterAfterProjecting_ColorsWhatIsAlreadyShown_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = CodeDocument };

            editor.SyntaxHighlighter = Highlighter;

            CodeBlockOf(editor).Inlines.OfType<Run>().Select(run => run.Text).ShouldContain("const");
            editor.Markdown.ShouldBe(CodeDocument);
        });
    }

    [Fact]
    public void TypingInACodeBlock_ReHighlightsIt_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = CodeDocument };
            var codeBlock = CodeBlockOf(editor);

            // Type " var" at the very end of the code — the word must come out colored as a keyword
            // once the typing settles.
            editor.CaretPosition = codeBlock.ContentEnd;
            editor.CaretPosition.InsertTextInRun(" var");
            editor.FlushPendingSyntaxHighlight();

            CodeBlockOf(editor).Inlines.OfType<Run>().Select(run => run.Text).ShouldContain("var");
        });
    }

    [Fact]
    public void TypingInACodeBlock_ReHighlightingDoesNotChangeTheCapturedMarkdown_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = CodeDocument };
            var codeBlock = CodeBlockOf(editor);
            editor.CaretPosition = codeBlock.ContentEnd;
            editor.CaretPosition.InsertTextInRun(" var");
            var afterTyping = editor.Markdown;

            editor.FlushPendingSyntaxHighlight();

            // Re-coloring the block is not an edit: the source is exactly what the typing left.
            editor.Markdown.ShouldBe(afterTyping);
            afterTyping.ShouldBe("```csharp\n// note\nconst int N = 42; var\n```");
        });
    }

    [Fact]
    public void TypingInACodeBlock_ReHighlightingLeavesTheCaretWhereItWas_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = CodeDocument };
            var codeBlock = CodeBlockOf(editor);
            editor.CaretPosition = codeBlock.ContentEnd;
            editor.CaretPosition.InsertTextInRun(" var");

            // The caret must still sit after the same code. It is measured as the text preceding it
            // rather than as a raw pointer offset: the rebuild deliberately re-splits the Runs, so
            // the number of element boundaries before the caret changes even though its place in
            // the code does not.
            var before = new TextRange(CodeBlockOf(editor).ContentStart, editor.CaretPosition).Text;

            editor.FlushPendingSyntaxHighlight();

            new TextRange(CodeBlockOf(editor).ContentStart, editor.CaretPosition).Text.ShouldBe(before);
            before.ShouldEndWith("const int N = 42; var");
        });
    }

    [Fact]
    public void ReHighlighting_WhenTheColoringWouldNotChange_LeavesTheDocumentUntouched_INV064()
    {
        StaThread.Run(() =>
        {
            // The document is only rebuilt when the Code Tokens actually differ from what is already
            // rendered. That is what keeps a re-highlight off the undo stack and stops the caret and
            // the Runs churning while the user reads.
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = CodeDocument };
            var runsBefore = CodeBlockOf(editor).Inlines.OfType<Run>().ToList();

            editor.FlushPendingSyntaxHighlight();

            // Reference equality: not merely equal Runs, but the very same Run objects — proof that
            // nothing was replaced.
            CodeBlockOf(editor).Inlines.OfType<Run>().ShouldBe(runsBefore);
        });
    }

    [Fact]
    public void ReHighlighting_ReplacesOnlyWhatTheColoringChanges_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = CodeDocument };
            var codeBlock = CodeBlockOf(editor);
            editor.CaretPosition = codeBlock.ContentEnd;
            editor.CaretPosition.InsertTextInRun(" var");

            editor.FlushPendingSyntaxHighlight();

            // The edited block is re-tokenized once and settles: a second flush finds nothing to do.
            var settled = CodeBlockOf(editor).Inlines.OfType<Run>().ToList();
            editor.FlushPendingSyntaxHighlight();
            CodeBlockOf(editor).Inlines.OfType<Run>().ShouldBe(settled);
        });
    }

    [Fact]
    public void TypingOutsideACodeBlock_LeavesTheMarkdownAndTheDocumentAlone_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = "Some prose." };
            var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock!;
            editor.CaretPosition = paragraph.ContentEnd;
            editor.CaretPosition.InsertTextInRun(" More.");
            var afterTyping = editor.Markdown;

            editor.FlushPendingSyntaxHighlight();

            editor.Markdown.ShouldBe(afterTyping);
        });
    }

    [Fact]
    public void ACodeBlockWithNoHighlightingLanguage_IsLeftPlain_INV064()
    {
        StaThread.Run(() =>
        {
            const string plain = "```\npublic class Foo { }\n```";
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = plain };

            editor.Markdown.ShouldBe(plain);
            new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text
                .ShouldContain("public class Foo { }");
        });
    }

    [Fact]
    public void SwitchingDocuments_ColorsTheNewOne_INV064()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = "# Prose only" };

            editor.Markdown = CodeDocument;

            CodeBlockOf(editor).Inlines.OfType<Run>().Select(run => run.Text).ShouldContain("const");
        });
    }

    [Fact]
    public void AMermaidDiagram_IsNotHighlighted_AndStillRoundTrips_INV064()
    {
        StaThread.Run(() =>
        {
            // A Mermaid Diagram is shown as a picture (INV-047); there is no code text to color.
            const string diagram = "```mermaid\ngraph TD\n  A-->B\n```";
            var editor = new MarkdownRichEditor { SyntaxHighlighter = Highlighter, Markdown = diagram };

            editor.Markdown.ShouldBe(diagram);
        });
    }
}
