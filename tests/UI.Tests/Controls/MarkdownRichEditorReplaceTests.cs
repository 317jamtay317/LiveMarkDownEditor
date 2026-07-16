using Shouldly;
using UI.Controls;
using UI.Find;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Replace and Replace All on the <see cref="MarkdownRichEditor"/>. Unlike Find, which is
/// view-only (INV-016), a Replace is a real edit: it swaps a Match for the Replacement and Captures
/// the result back into the Markdown Document as canonical Markdown (INV-022).
/// </summary>
public sealed class MarkdownRichEditorReplaceTests
{
    // Opens the Find Bar on a query, as pressing Ctrl+H and typing would, so Matches exist to Replace.
    private static MarkdownRichEditor EditorFinding(string markdown, string query, string replacement)
    {
        var editor = new MarkdownRichEditor { Markdown = markdown };
        editor.IsFindActive = true;
        editor.IsReplaceActive = true;
        editor.FindQuery = query;
        editor.Replacement = replacement;
        return editor;
    }

    [Fact]
    public void Replace_ReplacesCurrentMatch_AndCapturesToMarkdown_INV022()
    {
        StaThread.Run(() =>
        {
            var editor = EditorFinding("alpha beta alpha", "alpha", "gamma");

            MarkdownEditingCommands.Replace.Execute(parameter: null, target: editor);

            // Only the Current Match (the first) is replaced, and the edit Captures.
            editor.Markdown.ShouldBe("gamma beta alpha");
        });
    }

    [Fact]
    public void Replace_MovesToNextMatch_INV022()
    {
        StaThread.Run(() =>
        {
            var editor = EditorFinding("alpha beta alpha", "alpha", "gamma");

            MarkdownEditingCommands.Replace.Execute(parameter: null, target: editor);

            // One Match remains, and it is the Current Match — so Replacing again takes the second.
            editor.MatchCount.ShouldBe(1);
            editor.MatchSummary.ShouldBe("1 of 1");

            MarkdownEditingCommands.Replace.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("gamma beta gamma");
            editor.MatchCount.ShouldBe(0);
        });
    }

    [Fact]
    public void Replace_WithNoMatches_LeavesMarkdownUnchanged_INV022()
    {
        StaThread.Run(() =>
        {
            var editor = EditorFinding("alpha beta", "nothing here", "gamma");

            MarkdownEditingCommands.Replace.Execute(parameter: null, target: editor);
            MarkdownEditingCommands.ReplaceAll.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("alpha beta");
        });
    }

    [Fact]
    public void Replace_WithEmptyReplacement_DeletesTheMatch_INV022()
    {
        StaThread.Run(() =>
        {
            // An empty Replacement is legal — it is how a user deletes every occurrence.
            var editor = EditorFinding("alpha beta", "alpha ", string.Empty);

            MarkdownEditingCommands.Replace.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("beta");
        });
    }

    [Fact]
    public void Replace_MatchFoundCaseInsensitively_InsertsReplacementVerbatim_INV022()
    {
        StaThread.Run(() =>
        {
            // "Alpha" matches the query "alpha", but the Replacement is not re-cased to suit it.
            var editor = EditorFinding("Alpha beta", "alpha", "gamma");

            MarkdownEditingCommands.Replace.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("gamma beta");
        });
    }

    [Theory]
    [InlineData("**bold** text", "**plain** text")]         // bold
    [InlineData("*bold* text", "*plain* text")]             // italic
    [InlineData("~~bold~~ text", "~~plain~~ text")]         // strikethrough
    [InlineData("`bold` text", "`plain` text")]             // code span
    public void Replace_MatchWithinUniformFormatting_InheritsThatFormatting_INV022(
        string markdown,
        string expected)
    {
        StaThread.Run(() =>
        {
            // The Match has a single formatting, so the Replacement inherits it.
            var editor = EditorFinding(markdown, "bold", "plain");

            MarkdownEditingCommands.Replace.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe(expected);
        });
    }

    [Theory]
    [InlineData("**bo**ld text")]                           // bold -> plain
    [InlineData("bo**ld** text")]                           // plain -> bold
    [InlineData("*bo*ld text")]                             // italic
    [InlineData("`bo`ld text")]                             // code span
    [InlineData("~~bo~~ld text")]                           // strikethrough
    public void Replace_MatchSpanningFormattingBoundary_IsPlain_INV022(string markdown)
    {
        StaThread.Run(() =>
        {
            // Only "bo" is formatted, so the Match straddles the boundary and has no single
            // formatting to inherit — the Replacement is plain.
            var editor = EditorFinding(markdown, "bold", "plain");

            MarkdownEditingCommands.Replace.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("plain text");
        });
    }

    [Fact]
    public void ReplaceAll_ReplacesEveryMatch_INV022()
    {
        StaThread.Run(() =>
        {
            var editor = EditorFinding("alpha beta alpha gamma alpha", "alpha", "delta");

            MarkdownEditingCommands.ReplaceAll.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("delta beta delta gamma delta");
            editor.MatchCount.ShouldBe(0);
        });
    }

    [Fact]
    public void ReplaceAll_UnfoldsFoldedSections_SoNoMatchIsMissed_INV022()
    {
        StaThread.Run(() =>
        {
            // Find searches only the visible document, so an occurrence inside a Folded Section Body
            // is invisible to it — yet it is still in the Markdown Document. Replace All must Unfold
            // first and replace it rather than silently leave it behind.
            var editor = EditorFinding("# Title\n\nalpha one\n\nalpha two", "alpha", "beta");
            editor.Fold(editor.Document.Blocks.FirstBlock!);

            // The Section Body holding both occurrences is now hidden: a Find over the visible
            // Visual Document sees nothing to replace.
            MatchScanner.Scan(editor.Document, "alpha").ShouldBeEmpty();

            // Replace All must still be *reachable* — its whole point here is to Unfold and find
            // them. Gating it on the visible Match count would leave the command dead in the UI,
            // which Execute() alone would not catch (it bypasses CanExecute).
            MarkdownEditingCommands.ReplaceAll.CanExecute(parameter: null, target: editor).ShouldBeTrue();

            MarkdownEditingCommands.ReplaceAll.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("# Title\n\nbeta one\n\nbeta two");
            editor.Markdown.ShouldNotContain("alpha");
        });
    }

    [Fact]
    public void ReplaceAll_WhenReplacementContainsQuery_ReplacesOnlyTheOriginalMatches_INV022()
    {
        StaThread.Run(() =>
        {
            // The Replacement re-matches the query. Replace All must replace exactly the Matches
            // present when it was invoked and stop — never cascade into its own output.
            var editor = EditorFinding("a b a", "a", "aa");

            MarkdownEditingCommands.ReplaceAll.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("aa b aa");
        });
    }

    [Fact]
    public void ReplaceAll_CapturesCanonicalMarkdown_ThatRoundTrips_INV022()
    {
        StaThread.Run(() =>
        {
            // Anti-corruption: Replacing across headings, lists, bold, and a Table must leave source
            // that Round-Trips — re-Projecting and Capturing it again converges (INV-005).
            const string markdown = """
                # alpha title

                Some **alpha** text with `alpha` code.

                - alpha item
                - other item

                | alpha | head |
                | --- | --- |
                | alpha | cell |
                """;

            var editor = EditorFinding(markdown, "alpha", "omega");
            MarkdownEditingCommands.ReplaceAll.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            captured.ShouldNotContain("alpha");

            // Round-Trip the Captured source: it must converge.
            var reprojected = new MarkdownRichEditor { Markdown = captured };
            reprojected.Capture().ShouldBe(captured);
        });
    }

    // Replace All groups its edits into a single undo unit (BeginChange/EndChange), but that cannot
    // be covered here: WPF attaches no undo stack until the control is loaded in a visual tree, so
    // CanUndo is false on a headless editor even after an ordinary edit. It is verified by driving
    // Ctrl+Z in the running app instead — see docs/controls/MarkdownRichEditor.md.

    [Fact]
    public void Replace_WithoutACurrentMatch_IsUnavailable_INV022()
    {
        StaThread.Run(() =>
        {
            // Replace acts on the Current Match, so it needs one.
            var editor = EditorFinding("alpha beta", "nothing here", "gamma");

            MarkdownEditingCommands.Replace.CanExecute(parameter: null, target: editor).ShouldBeFalse();

            editor.FindQuery = "alpha";

            MarkdownEditingCommands.Replace.CanExecute(parameter: null, target: editor).ShouldBeTrue();
        });
    }

    [Fact]
    public void ReplaceAll_IsAvailableWheneverThereIsAQuery_EvenWithNoVisibleMatch_INV022()
    {
        StaThread.Run(() =>
        {
            // Replace All must not be gated on the visible Match count: the Matches it exists to
            // catch may all be hidden inside Folded Sections, which it Unfolds for itself.
            var editor = EditorFinding("alpha beta", string.Empty, "gamma");

            MarkdownEditingCommands.ReplaceAll.CanExecute(parameter: null, target: editor).ShouldBeFalse();

            editor.FindQuery = "nothing here";

            MarkdownEditingCommands.ReplaceAll.CanExecute(parameter: null, target: editor).ShouldBeTrue();
        });
    }

    [Fact]
    public void ShowReplace_OpensFindBar_WithReplaceRow_AndHideFindClosesBoth()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha" };

            MarkdownEditingCommands.ShowReplace.Execute(parameter: null, target: editor);

            editor.IsFindActive.ShouldBeTrue();
            editor.IsReplaceActive.ShouldBeTrue();

            MarkdownEditingCommands.HideFind.Execute(parameter: null, target: editor);

            editor.IsFindActive.ShouldBeFalse();
            editor.IsReplaceActive.ShouldBeFalse();
        });
    }

    [Fact]
    public void ShowFind_OpensFindBar_WithoutReplaceRow()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha" };

            MarkdownEditingCommands.ShowFind.Execute(parameter: null, target: editor);

            editor.IsFindActive.ShouldBeTrue();
            editor.IsReplaceActive.ShouldBeFalse();
        });
    }
}
