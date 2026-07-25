using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Media;
using Domain;
using Infrastructure.Markdown;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for <see cref="SyntaxHighlighting"/>: it rebuilds a Code Block's Runs from its Code Tokens
/// so the code is colored by what it means. Covers INV-064 — the code the paragraph holds is
/// byte-identical afterwards, so Capture is unaffected.
/// </summary>
public sealed class SyntaxHighlightingTests
{
    private static readonly MarkdownToFlowDocumentProjector Projector = new();
    private static readonly ColorCodeSyntaxHighlighter Highlighter = new();
    private static readonly FlowDocumentToMarkdownCapturer Capturer = new();

    // The document's one Code Block, found the way the Code Shading overlay finds it — a block Code
    // Region is exactly a Code Block (INV-017), so this needs no reach into the projector's internals.
    private static Paragraph CodeBlockOf(FlowDocument document) =>
        CodeShadingScanner.Scan(document).Single(region => region.IsBlock).Start.Paragraph!;

    // The code a Code Block paragraph holds, read back exactly the way Capture reads it.
    private static string CodeOf(Paragraph paragraph)
    {
        var text = new System.Text.StringBuilder();
        foreach (var inline in paragraph.Inlines)
        {
            switch (inline)
            {
                case Run run:
                    text.Append(run.Text);
                    break;
                case LineBreak:
                    text.Append('\n');
                    break;
            }
        }

        return text.ToString();
    }

    [Fact]
    public void Apply_PreservesTheCodeExactly_INV064()
    {
        StaThread.Run(() =>
        {
            const string markdown = "```csharp\n// note\npublic int N = 42;\nvar s = \"hi\";\n```";
            var document = Projector.Project(markdown);
            var paragraph = CodeBlockOf(document);
            var before = CodeOf(paragraph);

            SyntaxHighlighting.Apply(paragraph, Highlighter);

            CodeOf(paragraph).ShouldBe(before);
        });
    }

    [Fact]
    public void Apply_DoesNotChangeTheCapturedMarkdown_INV064()
    {
        StaThread.Run(() =>
        {
            const string markdown = "```csharp\n// note\npublic int N = 42;\n```";
            var document = Projector.Project(markdown);
            var before = Capturer.Capture(document.Blocks);

            SyntaxHighlighting.ApplyAll(document, Highlighter);

            Capturer.Capture(document.Blocks).ShouldBe(before);
        });
    }

    [Fact]
    public void Apply_ColorsKeywordsStringsAndComments_INV064()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("```csharp\n// note\nconst int N = 42;\n```");
            var paragraph = CodeBlockOf(document);

            SyntaxHighlighting.Apply(paragraph, Highlighter);

            var runs = paragraph.Inlines.OfType<Run>().ToList();
            runs.ShouldContain(run => run.Text == "// note");
            runs.ShouldContain(run => run.Text == "const");
            runs.ShouldContain(run => run.Text == "42");

            // A colored Run resolves its brush from the palette rather than carrying a color, so a
            // theme flip recolors it without re-tokenizing.
            var keyword = runs.Single(run => run.Text == "const");
            keyword.ReadLocalValue(TextElement.ForegroundProperty)
                .ShouldNotBe(System.Windows.DependencyProperty.UnsetValue);
            keyword.ReadLocalValue(TextElement.ForegroundProperty)
                .ShouldNotBeAssignableTo<Brush>("a Code Token references its palette brush, never a fixed color");
        });
    }

    [Fact]
    public void Apply_LeavesPlainCodeInheritingTheOrdinaryForeground_INV064()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("```csharp\nvar someIdentifier = 1;\n```");
            var paragraph = CodeBlockOf(document);

            SyntaxHighlighting.Apply(paragraph, Highlighter);

            var plain = paragraph.Inlines.OfType<Run>().Single(run => run.Text.Contains("someIdentifier"));
            plain.ReadLocalValue(TextElement.ForegroundProperty)
                .ShouldBe(System.Windows.DependencyProperty.UnsetValue);
        });
    }

    [Fact]
    public void Apply_KeepsLineStructure_SoEachLineIsStillItsOwnLine_INV064()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("```csharp\nint a = 1;\nint b = 2;\nint c = 3;\n```");
            var paragraph = CodeBlockOf(document);

            SyntaxHighlighting.Apply(paragraph, Highlighter);

            paragraph.Inlines.OfType<LineBreak>().Count().ShouldBe(2);
            CodeOf(paragraph).ShouldBe("int a = 1;\nint b = 2;\nint c = 3;");
        });
    }

    [Fact]
    public void Apply_GivenNoHighlightingLanguage_LeavesTheCodeUncolored_INV064()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("```\npublic class Foo { }\n```");
            var paragraph = CodeBlockOf(document);

            SyntaxHighlighting.Apply(paragraph, Highlighter);

            CodeOf(paragraph).ShouldBe("public class Foo { }");
            paragraph.Inlines.OfType<Run>().ShouldAllBe(
                run => run.ReadLocalValue(TextElement.ForegroundProperty) == System.Windows.DependencyProperty.UnsetValue);
        });
    }

    [Fact]
    public void Apply_GivenAnUnknownLanguage_LeavesTheCodeUncolored_INV064()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("```bash\nls -la | grep x\n```");
            var paragraph = CodeBlockOf(document);

            SyntaxHighlighting.Apply(paragraph, Highlighter);

            CodeOf(paragraph).ShouldBe("ls -la | grep x");
            paragraph.Inlines.OfType<Run>().ShouldAllBe(
                run => run.ReadLocalValue(TextElement.ForegroundProperty) == System.Windows.DependencyProperty.UnsetValue);
        });
    }

    [Fact]
    public void Apply_IsIdempotent_ReapplyingChangesNothing_INV064()
    {
        StaThread.Run(() =>
        {
            // The editor re-highlights an edited Code Block; doing so repeatedly must converge, not
            // accumulate Runs or drift the code.
            var document = Projector.Project("```csharp\n// note\nconst int N = 42;\n```");
            var paragraph = CodeBlockOf(document);

            SyntaxHighlighting.Apply(paragraph, Highlighter);
            var runsAfterFirst = paragraph.Inlines.OfType<Run>().ToList();
            var codeAfterFirst = CodeOf(paragraph);

            SyntaxHighlighting.Apply(paragraph, Highlighter);

            // Reference equality, not merely equal text: when the coloring is already right the
            // paragraph is left alone entirely, so the caret does not move and the undo stack gains
            // no entry that undoes no edit.
            paragraph.Inlines.OfType<Run>().ShouldBe(runsAfterFirst);
            CodeOf(paragraph).ShouldBe(codeAfterFirst);
        });
    }

    [Fact]
    public void ApplyAllForPrint_ColorsWithFixedInk_NotThePalette_INV064()
    {
        StaThread.Run(() =>
        {
            // A printed document is composed detached from the window, and paper is white whatever
            // the app's theme is. So a printout's Code Tokens carry their color outright rather
            // than referencing a palette brush that would resolve to the dark theme's ink.
            var document = Projector.Project("```csharp\nconst int N = 42;\n```");

            SyntaxHighlighting.ApplyAllForPrint(document, Highlighter);

            var keyword = CodeBlockOf(document).Inlines.OfType<Run>().Single(run => run.Text == "const");
            keyword.Foreground.ShouldBeOfType<SolidColorBrush>().Color.ShouldBe(Color.FromRgb(0xCF, 0x22, 0x2E));
        });
    }

    [Fact]
    public void ApplyAllForPrint_DoesNotChangeTheCapturedMarkdown_INV064()
    {
        StaThread.Run(() =>
        {
            const string markdown = "```csharp\n// note\nconst int N = 42;\n```";
            var document = Projector.Project(markdown);
            var before = Capturer.Capture(document.Blocks);

            SyntaxHighlighting.ApplyAllForPrint(document, Highlighter);

            Capturer.Capture(document.Blocks).ShouldBe(before);
        });
    }

    [Fact]
    public void ApplyAll_SkipsAMermaidDiagram_INV064()
    {
        StaThread.Run(() =>
        {
            // A Mermaid Diagram is shown as a picture (INV-047); there is no code text to color,
            // and its source lives on the diagram's role rather than in Runs.
            const string markdown = "```mermaid\ngraph TD;\n  A-->B;\n```";
            var document = Projector.Project(markdown);
            var before = Capturer.Capture(document.Blocks);

            SyntaxHighlighting.ApplyAll(document, Highlighter);

            Capturer.Capture(document.Blocks).ShouldBe(before);
        });
    }

    [Fact]
    public void ApplyAll_ColorsACodeBlockNestedInAListOrQuote_INV064()
    {
        StaThread.Run(() =>
        {
            const string markdown = "> ```csharp\n> const int N = 1;\n> ```";
            var document = Projector.Project(markdown);
            var before = Capturer.Capture(document.Blocks);

            SyntaxHighlighting.ApplyAll(document, Highlighter);

            Capturer.Capture(document.Blocks).ShouldBe(before);
            AllRuns(document.Blocks).ShouldContain(run => run.Text == "const");
        });
    }

    [Fact]
    public void ApplyAll_LeavesProseAndCodeSpansAlone_INV064()
    {
        StaThread.Run(() =>
        {
            // Syntax Highlighting is the Code Block's alone: a Code Span carries no language, so
            // nothing says what its code would mean.
            const string markdown = "Call `const` now.\n\nOrdinary **prose**.";
            var document = Projector.Project(markdown);
            var before = Capturer.Capture(document.Blocks);

            SyntaxHighlighting.ApplyAll(document, Highlighter);

            Capturer.Capture(document.Blocks).ShouldBe(before);
        });
    }

    [Fact]
    public void Apply_GivenAnEmptyCodeBlock_DoesNothing_INV064()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("```csharp\n```");
            var paragraph = CodeBlockOf(document);
            var before = CodeOf(paragraph);

            Should.NotThrow(() => SyntaxHighlighting.Apply(paragraph, Highlighter));

            CodeOf(paragraph).ShouldBe(before);
        });
    }

    [Fact]
    public void Apply_GivenNullHighlighter_LeavesTheCodeUntouched_INV064()
    {
        StaThread.Run(() =>
        {
            // The port binds after the first projection; a document projected before it arrives must
            // still show its code, uncolored, rather than fail.
            var document = Projector.Project("```csharp\nconst int N = 1;\n```");
            var paragraph = CodeBlockOf(document);

            Should.NotThrow(() => SyntaxHighlighting.Apply(paragraph, highlighter: null));

            CodeOf(paragraph).ShouldBe("const int N = 1;");
        });
    }

    private static IEnumerable<Run> AllRuns(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    foreach (var run in paragraph.Inlines.OfType<Run>())
                    {
                        yield return run;
                    }

                    break;
                case Section section:
                    foreach (var run in AllRuns(section.Blocks))
                    {
                        yield return run;
                    }

                    break;
                case List list:
                    foreach (var run in list.ListItems.SelectMany(item => AllRuns(item.Blocks)))
                    {
                        yield return run;
                    }

                    break;
            }
        }
    }
}
