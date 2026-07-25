using System.Collections.Frozen;
using Domain;
using Markdig.Syntax;
using MigraDoc.DocumentObjectModel;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a Code Block: its code monospaced and shaded, colored by the same Code Tokens the Visual
/// Document shows (INV-064).
/// </summary>
/// <remarks>
/// A Code Block with no Highlighting Language yields no tokens and is written as plain code rather
/// than guessed at. Coloring changes no code — the runs concatenate back to exactly the block's
/// code — so a colored export still carries the document (INV-033).
/// </remarks>
/// <param name="syntaxHighlighter">
/// Tokenizes the code. <see langword="null"/> writes every Code Block uncolored.
/// </param>
internal sealed class CodeBlockWriter(ISyntaxHighlighter? syntaxHighlighter) : IBlockWriter
{
    // The color each Code Token Kind is printed in — the editor's light palette, which is the one
    // that reads on paper (INV-064). A Plain Code Token is absent on purpose: it prints in the
    // ordinary code color, exactly as it shows in the editor.
    private static readonly FrozenDictionary<CodeTokenKind, Color> CodeTokenColors =
        new Dictionary<CodeTokenKind, Color>
        {
            [CodeTokenKind.Comment] = new Color(0x6E, 0x77, 0x81),
            [CodeTokenKind.String] = new Color(0x0F, 0x76, 0x6E),
            [CodeTokenKind.Number] = new Color(0xB4, 0x53, 0x09),
            [CodeTokenKind.Keyword] = new Color(0xCF, 0x22, 0x2E),
            [CodeTokenKind.Type] = new Color(0x7C, 0x3A, 0xED),
            [CodeTokenKind.Function] = new Color(0x1F, 0x6F, 0xEB),
            [CodeTokenKind.Operator] = new Color(0x57, 0x60, 0x6A),
        }.ToFrozenDictionary();

    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is not CodeBlock code)
        {
            return;
        }

        var paragraph = context.Unquoted().Indented(0.2).NewParagraph();
        paragraph.Format.Font.Name = PdfStyle.CodeFont;
        paragraph.Format.Font.Size = 9.5;
        paragraph.Format.Shading.Color = Colors.WhiteSmoke;
        paragraph.Format.SpaceBefore = "4pt";
        paragraph.Format.SpaceAfter = "4pt";

        var tokens = code is FencedCodeBlock fenced
            ? syntaxHighlighter?.Highlight(CodeOf(code), fenced.Info) ?? []
            : [];

        if (tokens.Count > 0)
        {
            WriteTokens(paragraph, tokens);
            return;
        }

        WriteLines(paragraph, code);
    }

    // Writes the Code Tokens into the paragraph, one run each, splitting a token that spans a line
    // end so each code line still lands on its own line. A Plain Code Token is written as ordinary
    // text with no color of its own, mirroring the palette.
    private static void WriteTokens(Paragraph paragraph, IReadOnlyList<CodeToken> tokens)
    {
        foreach (var token in tokens)
        {
            var lines = token.Text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    paragraph.AddLineBreak();
                }

                if (lines[i].Length == 0)
                {
                    continue;
                }

                if (CodeTokenColors.TryGetValue(token.Kind, out var color))
                {
                    paragraph.AddFormattedText(lines[i]).Font.Color = color;
                }
                else
                {
                    paragraph.AddText(lines[i]);
                }
            }
        }
    }

    private static void WriteLines(Paragraph paragraph, CodeBlock code)
    {
        for (var i = 0; i < code.Lines.Count; i++)
        {
            if (i > 0)
            {
                paragraph.AddLineBreak();
            }

            paragraph.AddText(code.Lines.Lines[i].Slice.ToString());
        }
    }

    // The block's code, lines joined by '\n' — the same text the Visual Document holds, so the PDF
    // tokenizes exactly what the editor tokenized.
    private static string CodeOf(LeafBlock block)
    {
        var lines = block.Lines;
        var slices = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            slices.Add(lines.Lines[i].Slice.ToString());
        }

        return string.Join("\n", slices);
    }
}
