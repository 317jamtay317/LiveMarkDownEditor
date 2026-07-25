using System.Collections.Frozen;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;
using Domain;

namespace UI.Wysiwyg;

/// <summary>
/// Applies Syntax Highlighting to the Code Blocks of a Visual Document: each Code Block's code is
/// tokenized by its Highlighting Language and rebuilt as one Run per Code Token, colored from the
/// active palette (INV-064).
/// </summary>
/// <remarks>
/// This is the Code-Block counterpart of Code Shading (INV-017), and it is view-only in the same
/// sense: it changes how the code <em>looks</em>, never what it says. Capture reads a Code Block's
/// Runs back as its code (INV-004), and the rebuilt Runs concatenate to exactly the code that was
/// there — the tokenizer guarantees it — so the Captured Markdown is untouched.
/// <para>
/// Unlike Code Shading this cannot be an adorner: an overlay that owns no text cannot recolor
/// glyphs the editor has already drawn, only paint behind them. So the color lives on the Runs.
/// What keeps a theme flip cheap is that each Run references its palette <em>brush</em> rather than
/// carrying a color, so recoloring is the brush's business and no re-tokenizing or re-projection
/// is needed.
/// </para>
/// </remarks>
public static class SyntaxHighlighting
{
    // The palette key each Code Token Kind colors through. Plain is deliberately absent: a plain
    // Run is left with no Foreground of its own so it inherits the ordinary code foreground, which
    // is what makes un-highlighted code and the plain stretches of highlighted code identical.
    private static readonly FrozenDictionary<CodeTokenKind, string> BrushKeys = new Dictionary<CodeTokenKind, string>()
    {
        [CodeTokenKind.Comment] = "CodeTokenCommentBrush",
        [CodeTokenKind.String] = "CodeTokenStringBrush",
        [CodeTokenKind.Number] = "CodeTokenNumberBrush",
        [CodeTokenKind.Keyword] = "CodeTokenKeywordBrush",
        [CodeTokenKind.Type] = "CodeTokenTypeBrush",
        [CodeTokenKind.Function] = "CodeTokenFunctionBrush",
        [CodeTokenKind.Operator] = "CodeTokenOperatorBrush",
    }.ToFrozenDictionary();

    // The ink each Code Token Kind prints in: the light palette's colors, frozen. A printout is
    // composed detached from the window and lands on white paper whatever the app's theme is, so it
    // carries its color outright rather than referencing a palette brush that would resolve to the
    // dark theme's ink (INV-064).
    private static readonly FrozenDictionary<CodeTokenKind, Brush> PrintInks = new Dictionary<CodeTokenKind, Brush>()
    {
        [CodeTokenKind.Comment] = Frozen(0x6E, 0x77, 0x81),
        [CodeTokenKind.String] = Frozen(0x0F, 0x76, 0x6E),
        [CodeTokenKind.Number] = Frozen(0xB4, 0x53, 0x09),
        [CodeTokenKind.Keyword] = Frozen(0xCF, 0x22, 0x2E),
        [CodeTokenKind.Type] = Frozen(0x7C, 0x3A, 0xED),
        [CodeTokenKind.Function] = Frozen(0x1F, 0x6F, 0xEB),
        [CodeTokenKind.Operator] = Frozen(0x57, 0x60, 0x6A),
    }.ToFrozenDictionary();

    /// <summary>
    /// Highlights every Code Block in <paramref name="document"/>, descending through block quotes,
    /// lists, and tables so a nested Code Block is reached too.
    /// </summary>
    /// <param name="document">The Visual Document to highlight. <see langword="null"/> does nothing.</param>
    /// <param name="highlighter">
    /// The tokenizer to color by. <see langword="null"/> does nothing, so a document projected
    /// before the port is bound still shows its code — uncolored — rather than failing.
    /// </param>
    public static void ApplyAll(FlowDocument? document, ISyntaxHighlighter? highlighter)
    {
        if (document is not null && highlighter is not null)
        {
            ApplyToBlocks(document.Blocks, highlighter, forPrint: false);
        }
    }

    /// <summary>
    /// Highlights every Code Block in <paramref name="document"/> for print, coloring each Code
    /// Token with fixed ink rather than a palette brush.
    /// </summary>
    /// <remarks>
    /// Print and Print Preview compose a fresh Visual Document that is never shown in the window
    /// (INV-034). A palette reference has nothing to resolve against there, and paper is white
    /// whatever theme the app is in — so a printout gets the light palette's colors outright.
    /// </remarks>
    /// <param name="document">The Visual Document to highlight. <see langword="null"/> does nothing.</param>
    /// <param name="highlighter">The tokenizer to color by. <see langword="null"/> does nothing.</param>
    public static void ApplyAllForPrint(FlowDocument? document, ISyntaxHighlighter? highlighter)
    {
        if (document is not null && highlighter is not null)
        {
            ApplyToBlocks(document.Blocks, highlighter, forPrint: true);
        }
    }

    /// <summary>
    /// Highlights one Code Block, replacing its inline content with one Run per Code Token.
    /// </summary>
    /// <param name="paragraph">
    /// The Code Block paragraph to highlight. A paragraph that is not a Code Block, or one whose
    /// Code Block has no Highlighting Language, is left exactly as it is.
    /// </param>
    /// <param name="highlighter">The tokenizer to color by. <see langword="null"/> does nothing.</param>
    public static void Apply(Paragraph? paragraph, ISyntaxHighlighter? highlighter) =>
        Apply(paragraph, highlighter, forPrint: false);

    private static void Apply(Paragraph? paragraph, ISyntaxHighlighter? highlighter, bool forPrint)
    {
        if (highlighter is null || paragraph?.Tag is not CodeBlockRole role)
        {
            return;
        }

        var code = ReadCode(paragraph);
        if (code.Length == 0)
        {
            return;
        }

        var tokens = highlighter.Highlight(code, role.Language);
        if (tokens.Count == 0)
        {
            // No Highlighting Language. The code is already shown plain, so rebuilding its Runs
            // would churn the document — and move the caret — for no visible change.
            return;
        }

        var pieces = PiecesOf(tokens);
        if (AlreadyShows(paragraph, pieces))
        {
            // The coloring is already right. Rebuilding anyway would replace every Run for nothing
            // — moving the caret, and putting an entry on the undo stack that undoes no edit.
            return;
        }

        Rebuild(paragraph, pieces, forPrint);
    }

    // Reads a Code Block's code back exactly the way Capture does — Runs concatenated, a LineBreak
    // standing for a newline — so what is tokenized is what the Markdown says.
    private static string ReadCode(Paragraph paragraph)
    {
        var code = new StringBuilder();
        foreach (var inline in paragraph.Inlines)
        {
            switch (inline)
            {
                case Run run:
                    code.Append(run.Text);
                    break;
                case LineBreak:
                    code.Append('\n');
                    break;
            }
        }

        return code.ToString();
    }

    // One piece of a highlighted Code Block: a run of colored code, or — when Text is null — the
    // line end between two of them. Comparing pieces rather than Runs is what lets a re-highlight
    // decide whether anything would actually change before it touches the document.
    private readonly record struct Piece(string? Text, CodeTokenKind Kind);

    // Turns the Code Tokens into the pieces the paragraph should hold, splitting a token that spans
    // a line end so each line is still its own line. Capture reads a LineBreak as '\n' (and the
    // tokenizer was handed those same '\n's), so the code survives the rebuild character for
    // character.
    private static List<Piece> PiecesOf(IReadOnlyList<CodeToken> tokens)
    {
        var pieces = new List<Piece>();
        foreach (var token in tokens)
        {
            var lines = token.Text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    pieces.Add(new Piece(Text: null, token.Kind));
                }

                if (lines[i].Length > 0)
                {
                    pieces.Add(new Piece(lines[i], token.Kind));
                }
            }
        }

        return pieces;
    }

    // Whether the paragraph already shows exactly these pieces — same text, same kinds, same line
    // ends, in the same order. The kind is read back from the Run's Tag, which is where the rebuild
    // records it; Capture never looks at it (it reads Runs for their text alone), so it is free to
    // carry this.
    private static bool AlreadyShows(Paragraph paragraph, List<Piece> pieces)
    {
        var index = 0;
        foreach (var inline in paragraph.Inlines)
        {
            if (index >= pieces.Count)
            {
                return false;
            }

            var piece = pieces[index++];
            var matches = inline switch
            {
                Run run => piece.Text == run.Text && run.Tag is CodeTokenKind kind && kind == piece.Kind,
                LineBreak => piece.Text is null,
                _ => false,
            };

            if (!matches)
            {
                return false;
            }
        }

        return index == pieces.Count;
    }

    private static void Rebuild(Paragraph paragraph, List<Piece> pieces, bool forPrint)
    {
        var inlines = new List<Inline>(pieces.Count);
        foreach (var piece in pieces)
        {
            inlines.Add(piece.Text is null ? new LineBreak() : ApplyColor(new Run(piece.Text), piece.Kind, forPrint));
        }

        paragraph.Inlines.Clear();
        paragraph.Inlines.AddRange(inlines);
    }

    // Points the Run's Foreground at its kind's palette brush by resource reference, never at a
    // color: that is what lets a theme flip recolor every Code Block without re-tokenizing. The
    // kind is recorded on the Run so a later re-highlight can tell whether anything would change.
    private static Run ApplyColor(Run run, CodeTokenKind kind, bool forPrint)
    {
        run.Tag = kind;
        if (forPrint)
        {
            if (PrintInks.TryGetValue(kind, out var ink))
            {
                run.Foreground = ink;
            }
        }
        else if (BrushKeys.TryGetValue(kind, out var key))
        {
            run.SetResourceReference(TextElement.ForegroundProperty, key);
        }

        return run;
    }

    private static Brush Frozen(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    // Walks the block containers the projector produces, highlighting each Code Block. A Mermaid
    // Diagram is not reached: it projects to its own block kind (a picture, INV-047), not to a
    // paragraph tagged as a Code Block.
    //
    // The blocks are snapshotted before the walk because rebuilding a paragraph's Inlines bumps the
    // version of the whole text container, which invalidates any collection enumerator open over it
    // — including the one for the Section or List the paragraph sits in.
    private static void ApplyToBlocks(IEnumerable<Block> blocks, ISyntaxHighlighter highlighter, bool forPrint)
    {
        foreach (var block in blocks.ToList())
        {
            switch (block)
            {
                case Paragraph paragraph when paragraph.Tag is CodeBlockRole:
                    Apply(paragraph, highlighter, forPrint);
                    break;

                case Section section:
                    ApplyToBlocks(section.Blocks, highlighter, forPrint);
                    break;

                case List list:
                    foreach (var item in list.ListItems.ToList())
                    {
                        ApplyToBlocks(item.Blocks, highlighter, forPrint);
                    }

                    break;

                case Table table:
                    foreach (var cell in table.RowGroups
                        .SelectMany(group => group.Rows)
                        .SelectMany(row => row.Cells)
                        .ToList())
                    {
                        ApplyToBlocks(cell.Blocks, highlighter, forPrint);
                    }

                    break;
            }
        }
    }
}
