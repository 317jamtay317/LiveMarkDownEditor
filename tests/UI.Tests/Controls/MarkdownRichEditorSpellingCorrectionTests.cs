using System.Windows.Documents;
using System.Windows.Threading;
using Shouldly;
using UI.Controls;
using UI.Find;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for correcting a Misspelling with a Spelling Suggestion on the
/// <see cref="MarkdownRichEditor"/>. Choosing a Suggestion swaps the Misspelling's span for it, which
/// — like Replace, whose mechanism it shares — inherits the Misspelling's formatting and Captures back
/// into the Markdown Document (INV-022).
/// </summary>
/// <remarks>
/// The Misspelling ranges are scanned here with <see cref="MatchScanner"/> rather than taken from the
/// <c>SpellCheckAdorner</c>: the adorner needs an <see cref="AdornerLayer"/>, which a headless editor
/// has no visual tree to provide. The ranges are the same shape either way — a span inside one Run.
/// </remarks>
public sealed class MarkdownRichEditorSpellingCorrectionTests
{
    private static TextRange Misspelling(MarkdownRichEditor editor, string word) =>
        MatchScanner.Scan(editor.Document, word)[0];

    [Theory]
    [InlineData("plain **bolld here** text", "plain **bold here** text")]     // bold
    [InlineData("plain *bolld here* text", "plain *bold here* text")]         // italic
    [InlineData("plain ~~bolld here~~ text", "plain ~~bold here~~ text")]     // strikethrough
    [InlineData("plain **here bolld** text", "plain **here bold** text")]     // at the span's end
    [InlineData("plain **a bolld b** text", "plain **a bold b** text")]       // mid-span
    [InlineData("**bolld here** text", "**bold here** text")]                 // starting the block
    public void CorrectMisspelling_WithinFormattedText_KeepsThatFormatting_INV022(
        string markdown,
        string expected)
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = markdown };

            editor.CorrectMisspelling(Misspelling(editor, "bolld"), "bold");

            editor.Markdown.ShouldBe(expected);
        });
    }

    [Theory]
    [InlineData("plain **bolld** text", "plain **bold** text")]               // bold
    [InlineData("plain *bolld* text", "plain *bold* text")]                   // italic
    [InlineData("plain ~~bolld~~ text", "plain ~~bold~~ text")]               // strikethrough
    [InlineData("plain **bolld**", "plain **bold**")]                         // ending the block
    [InlineData("# A **bolld** heading", "# A **bold** heading")]             // inside a Heading
    [InlineData("- item **bolld**", "- item **bold**")]                       // inside a List Item
    public void CorrectMisspelling_WhereTheWordIsTheWholeFormattedSpan_KeepsThatFormatting_INV022(
        string markdown,
        string expected)
    {
        StaThread.Run(() =>
        {
            // The Misspelling is the *entire* formatted span — the commonest shape of all, a single
            // bolded word — so correcting it empties the span's Run. The formatting must survive that.
            var editor = new MarkdownRichEditor { Markdown = markdown };

            editor.CorrectMisspelling(Misspelling(editor, "bolld"), "bold");

            editor.Markdown.ShouldBe(expected);
        });
    }

    [Fact]
    public void CorrectMisspelling_InPlainProse_ReplacesTheWord_INV022()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "some bolld prose" };

            editor.CorrectMisspelling(Misspelling(editor, "bolld"), "bold");

            editor.Markdown.ShouldBe("some bold prose");
        });
    }

    [Fact]
    public void CorrectMisspelling_CapturesCanonicalMarkdown_ThatRoundTrips_INV022()
    {
        StaThread.Run(() =>
        {
            // Anti-corruption: correcting inside bold must leave source that Round-Trips (INV-005).
            var editor = new MarkdownRichEditor { Markdown = "# Title\n\nA **bolld heading** here." };

            editor.CorrectMisspelling(Misspelling(editor, "bolld"), "bold");
            var captured = editor.Markdown;

            captured.ShouldBe("# Title\n\nA **bold heading** here.");
            new MarkdownRichEditor { Markdown = captured }.Capture().ShouldBe(captured);
        });
    }

    [Theory]
    [InlineData("plain **bolld here** text", "plain **bold here** text")]     // part of the bold span
    [InlineData("plain **bolld** text", "plain **bold** text")]               // the whole bold span
    public void CorrectMisspelling_WithTheRangeTheSpellCheckAdornerFound_KeepsTheFormatting_INV022(
        string markdown,
        string expected)
    {
        StaThread.Run(() =>
        {
            // End to end through the real Spell Check: the adorner scans the document, finds the
            // Misspelling, and hands its range to the correction — the path a right-click takes. It is
            // the adorner's own range, not one this test built, that must survive the replacement.
            var editor = new MarkdownRichEditor { Markdown = markdown };
            var adorner = new SpellCheckAdorner(editor, new StubSpellDictionary("bolld"));

            // Spell Check is debounced onto the dispatcher, so let the queued rescan run.
            PumpDispatcher(TimeSpan.FromSeconds(2));

            var caret = MatchScanner.Scan(editor.Document, "bolld")[0].Start;
            var misspelling = adorner.MisspellingAt(caret).ShouldNotBeNull();
            misspelling.Text.ShouldBe("bolld");

            editor.CorrectMisspelling(misspelling, "bold");

            editor.Markdown.ShouldBe(expected);
        });
    }

    // Runs the STA thread's dispatcher queue for a while, so a DispatcherTimer set up by the code
    // under test actually ticks. A headless test has no message loop of its own.
    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(
            duration,
            DispatcherPriority.Background,
            (_, _) => frame.Continue = false,
            Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    [Fact]
    public void CorrectMisspelling_WithARangeFromAnotherDocument_ChangesNothing_INV022()
    {
        StaThread.Run(() =>
        {
            // A Misspelling range held from a document that has since been replaced must be ignored
            // rather than throw or edit the wrong document.
            var editor = new MarkdownRichEditor { Markdown = "some bolld prose" };
            var stale = Misspelling(editor, "bolld");
            editor.Markdown = "entirely different prose";

            editor.CorrectMisspelling(stale, "bold");

            editor.Markdown.ShouldBe("entirely different prose");
        });
    }
}
