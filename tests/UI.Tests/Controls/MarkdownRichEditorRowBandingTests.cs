using System.Windows.Documents;
using System.Windows.Media;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;
using WpfTable = System.Windows.Documents.Table;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Row Banding (INV-068): every other Body Row of a Table carries a shade so a wide Table
/// can be read across. The shade follows a row's <em>position</em>, so Add Row and Remove Row re-band
/// the rows below them; the header row is never banded; and none of it reaches the Markdown Document —
/// a Table Captures identically banded or not.
/// </summary>
/// <remarks>
/// The shade is assigned as a <c>DynamicResource</c> reference to <c>RowBandingBrush</c>, which the
/// active palette supplies at runtime so a theme change recolors the rows without re-formatting the
/// document (INV-017). No palette is merged in a test, so each test stands one in through the editor's
/// own <c>Resources</c> — the same lookup the real palette is found by.
/// </remarks>
public sealed class MarkdownRichEditorRowBandingTests
{
    // Two Body Rows: the first plain, the second banded.
    private const string SmallTable =
        "| A | B |\n| --- | --- |\n| a1 | b1 |\n| a2 | b2 |";

    // Four Body Rows, enough to show the alternation continuing and to re-band a middle insertion.
    private const string FourRowTable =
        "| A | B |\n| --- | --- |\n| a1 | b1 |\n| a2 | b2 |\n| a3 | b3 |\n| a4 | b4 |";

    private static readonly SolidColorBrush Shade = Brushes.Red;

    [Fact]
    public void Table_BandsEveryOtherBodyRow_INV068()
    {
        StaThread.Run(() =>
        {
            var editor = EditorWith(FourRowTable);

            // The first Body Row is left plain, so the header, the first row and the shade read as a
            // rhythm rather than as two stripes running together.
            BandingOf(editor).ShouldBe([false, true, false, true]);
        });
    }

    [Fact]
    public void Table_NeverBandsItsHeaderRow_INV068()
    {
        StaThread.Run(() =>
        {
            var editor = EditorWith(SmallTable);

            // A Table has exactly one header row and it already carries its own emphasis.
            HeaderRowOf(editor).Background.ShouldBeNull();
        });
    }

    [Fact]
    public void InsertTable_BandsTheNewTablesBodyRows_INV068()
    {
        StaThread.Run(() =>
        {
            var editor = EditorWith("prose");
            VisualDocumentText.PlaceCaretIn(editor, "prose");

            MarkdownEditingCommands.InsertTable.Execute(parameter: null, target: editor);

            // A user-built Table is banded exactly as a loaded one is (INV-018).
            BandingOf(editor).ShouldBe([false, true]);
        });
    }

    [Fact]
    public void AddRow_RebandsTheRowsBelowIt_INV068()
    {
        StaThread.Run(() =>
        {
            var editor = EditorWith(FourRowTable);
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.AddTableRow.Execute(parameter: null, target: editor);

            // The new row takes second place, so every row it pushed down changes stripe: a row does
            // not keep a shade it has been pushed out of.
            BandingOf(editor).ShouldBe([false, true, false, true, false]);
        });
    }

    [Fact]
    public void RemoveRow_RebandsTheRowsBelowIt_INV068()
    {
        StaThread.Run(() =>
        {
            var editor = EditorWith(FourRowTable);
            VisualDocumentText.PlaceCaretIn(editor, "a2");

            MarkdownEditingCommands.RemoveTableRow.Execute(parameter: null, target: editor);

            BandingOf(editor).ShouldBe([false, true, false]);
        });
    }

    [Fact]
    public void AddColumn_LeavesTheBandingAsItWas_INV068()
    {
        StaThread.Run(() =>
        {
            var editor = EditorWith(FourRowTable);
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.AddTableColumn.Execute(parameter: null, target: editor);

            // A column is not a row: widening the Table cannot restripe it.
            BandingOf(editor).ShouldBe([false, true, false, true]);
        });
    }

    [Fact]
    public void RowBanding_ChangesNothingInTheCapturedMarkdown_INV068()
    {
        StaThread.Run(() =>
        {
            var editor = EditorWith(FourRowTable);

            // Markdown has no way to say "shaded", so a banded Table Captures as the Table it is.
            editor.Markdown.ShouldBe(FourRowTable);
        });
    }

    /// <summary>
    /// The shade is a palette lookup rather than a fixed color, so flipping the theme recolors the
    /// banded rows in place — no part of the Visual Document is rebuilt, and nothing is Captured
    /// (the Code Shading rule of INV-017, reached for a Table row).
    /// </summary>
    [Fact]
    public void RowBanding_FollowsThePalette_WithoutTouchingTheDocument_INV068()
    {
        StaThread.Run(() =>
        {
            var editor = EditorWith(SmallTable);
            var banded = BandedRowsOf(editor).ShouldHaveSingleItem();
            banded.Background.ShouldBe(Shade);

            editor.Resources["RowBandingBrush"] = Brushes.Lime;

            banded.Background.ShouldBe(Brushes.Lime);
            editor.Markdown.ShouldBe(SmallTable);
        });
    }

    // An editor holding `markdown`, with a stand-in for the palette's Row Banding shade.
    private static MarkdownRichEditor EditorWith(string markdown)
    {
        var editor = new MarkdownRichEditor();
        editor.Resources["RowBandingBrush"] = Shade;
        editor.Markdown = markdown;
        return editor;
    }

    // Whether each Body Row of the document's first Table is banded, top to bottom.
    private static IReadOnlyList<bool> BandingOf(MarkdownRichEditor editor) =>
        [.. BodyRowsOf(editor).Select(row => row.Background is not null)];

    private static IReadOnlyList<TableRow> BandedRowsOf(MarkdownRichEditor editor) =>
        [.. BodyRowsOf(editor).Where(row => row.Background is not null)];

    private static TableRow HeaderRowOf(MarkdownRichEditor editor) => RowsOf(editor)[0];

    private static IEnumerable<TableRow> BodyRowsOf(MarkdownRichEditor editor) => RowsOf(editor).Skip(1);

    private static IReadOnlyList<TableRow> RowsOf(MarkdownRichEditor editor) =>
        [.. editor.Document.Blocks.OfType<WpfTable>().First().RowGroups.SelectMany(group => group.Rows)];
}
