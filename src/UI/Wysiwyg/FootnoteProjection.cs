using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigDocument = Markdig.Syntax.MarkdownDocument;
using WpfBlock = System.Windows.Documents.Block;

namespace UI.Wysiwyg;

/// <summary>
/// The one shared composition of a Footnote in the Visual Document: a Footnote Reference as a
/// superscript Footnote Number standing in the prose, and every Footnote Definition gathered into the
/// Footnote Section at the document's end (INV-065). The Projector and the Insert Footnote Formatting
/// Action both build Footnotes through here, so Capture treats a loaded Footnote and a user-cited one
/// identically (INV-018).
/// </summary>
internal static class FootnoteProjection
{
    /// <summary>
    /// One Footnote Definition as the parser gives it: its Footnote Label, the Footnote Number its
    /// References show (<see langword="null"/> when no Reference cites it), the source line it was
    /// authored on, and the blocks holding the note itself.
    /// </summary>
    /// <param name="Label">The Footnote Label, without its leading <c>^</c>.</param>
    /// <param name="Number">The Footnote Number, or <see langword="null"/> when unreferenced.</param>
    /// <param name="Line">The 0-based source line the Definition was authored on.</param>
    /// <param name="Blocks">The parsed blocks of the note's content.</param>
    internal sealed record ParsedDefinition(
        string Label, int? Number, int Line, IReadOnlyList<MarkdigBlock> Blocks);

    /// <summary>
    /// Every Footnote Definition of a parsed document, in the order a reader meets them: by Footnote
    /// Number, with the unreferenced ones last.
    /// </summary>
    /// <param name="ast">The parsed Markdown Document.</param>
    /// <returns>The Definitions to show in the Footnote Section; empty when the document has none.</returns>
    internal static List<ParsedDefinition> Collect(MarkdigDocument ast)
    {
        var found = new List<ParsedDefinition>();
        foreach (var block in ast)
        {
            switch (block)
            {
                case FootnoteGroup group:
                    foreach (var child in group)
                    {
                        if (child is Footnote footnote)
                        {
                            found.Add(Describe(footnote, footnote.Order));
                        }
                    }

                    break;

                // A Footnote Definition no Reference cites is dropped from the Footnote Group by the
                // parser, because Markdown omits it from the Rendered Output. It is kept here anyway:
                // dropping it would silently delete the author's prose the moment they removed a
                // Reference (INV-065). Its content survives on the link reference definition.
                case LinkReferenceDefinitionGroup definitions:
                    foreach (var child in definitions)
                    {
                        if (child is FootnoteLinkReferenceDefinition { Footnote: { Order: < 1 } unreferenced })
                        {
                            found.Add(Describe(unreferenced, number: null));
                        }
                    }

                    break;
            }
        }

        return [.. found.OrderBy(definition => definition.Number ?? int.MaxValue).ThenBy(definition => definition.Line)];
    }

    /// <summary>
    /// Projects a Footnote Reference: the superscript Footnote Number that stands in the prose where a
    /// Footnote is cited. Capture reads <paramref name="label"/> back off it, never the Number shown.
    /// </summary>
    /// <param name="label">The Footnote Label, without its leading <c>^</c>.</param>
    /// <param name="number">The Footnote Number to show.</param>
    /// <returns>The Run to place in the prose.</returns>
    internal static Run CreateReference(string label, int number)
    {
        var run = new Run(number.ToString(CultureInfo.InvariantCulture))
        {
            Tag = new FootnoteReferenceRole(label),
            BaselineAlignment = BaselineAlignment.Superscript,
            FontSize = ReferenceFontSize,
        };
        run.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
        return run;
    }

    /// <summary>
    /// Composes the Footnote Section: the block gathering every Footnote Definition at the end of the
    /// Visual Document, behind a thin rule, each Definition beside its Footnote Number.
    /// </summary>
    /// <param name="definitions">The Definitions to show, in the order <see cref="Collect"/> gives them.</param>
    /// <param name="anchors">Each already-projected top-level block paired with the source line it came
    /// from, so a Definition can remember the block it was authored after (INV-065).</param>
    /// <param name="projectBlock">How to project one parsed block of a note's content.</param>
    /// <returns>The Footnote Section to append to the Visual Document.</returns>
    internal static Section CreateSection(
        IReadOnlyList<ParsedDefinition> definitions,
        IReadOnlyList<(int Line, WpfBlock Block)> anchors,
        Func<MarkdigBlock, WpfBlock?> projectBlock)
    {
        var section = CreateEmptySection();
        foreach (var definition in definitions)
        {
            section.Blocks.Add(CreateDefinition(
                definition.Label,
                definition.Number,
                AnchorFor(definition.Line, anchors),
                definition.Blocks.Select(projectBlock).OfType<WpfBlock>()));
        }

        return section;
    }

    /// <summary>
    /// An empty Footnote Section — the rule and the spacing that set the notes off from the prose above
    /// them. Insert Footnote composes one when a document cites its first Footnote.
    /// </summary>
    /// <returns>A Footnote Section holding no Definitions yet.</returns>
    internal static Section CreateEmptySection()
    {
        var section = new Section
        {
            Tag = BlockSemantic.FootnoteSection,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0),
            Margin = new Thickness(0, 24, 0, 0),
        };
        section.SetResourceReference(WpfBlock.BorderBrushProperty, "BorderBrush");
        return section;
    }

    /// <summary>
    /// Composes one Footnote Definition for the Footnote Section: its Footnote Number, then the note's
    /// own editable blocks.
    /// </summary>
    /// <param name="label">The Footnote Label, without its leading <c>^</c>.</param>
    /// <param name="number">The Footnote Number to show, or <see langword="null"/> when unreferenced.</param>
    /// <param name="anchor">The top-level block the Definition was authored after, or
    /// <see langword="null"/> when it was authored before any of them.</param>
    /// <param name="blocks">The note's content; an empty note gets an empty paragraph to type into.</param>
    /// <returns>The Definition to place in the Footnote Section.</returns>
    internal static Section CreateDefinition(
        string label, int? number, WpfBlock? anchor, IEnumerable<WpfBlock> blocks)
    {
        var definition = new Section
        {
            Tag = new FootnoteDefinitionRole(label, anchor),
            Margin = DefinitionSpacing,
        };

        foreach (var block in blocks)
        {
            definition.Blocks.Add(block);
        }

        // A Section must hold at least one block, and an empty note needs somewhere to type. A note
        // opening with something other than a paragraph — a List, a Code Block — gets one above it to
        // carry the Footnote Number.
        if (definition.Blocks.FirstBlock is not Paragraph first)
        {
            first = new Paragraph { Margin = BodySpacing };
            if (definition.Blocks.FirstBlock is { } existingFirst)
            {
                definition.Blocks.InsertBefore(existingFirst, first);
            }
            else
            {
                definition.Blocks.Add(first);
            }
        }

        if (number is { } shown)
        {
            var marker = CreateNumber(label, shown);
            if (first.Inlines.FirstInline is { } existing)
            {
                first.Inlines.InsertBefore(existing, marker);
            }
            else
            {
                first.Inlines.Add(marker);
            }
        }

        return definition;
    }

    /// <summary>The Footnote Label of <paramref name="parsedLabel"/> without the parser's leading <c>^</c>.</summary>
    /// <param name="parsedLabel">The label as the parser reports it (<c>^note</c>), possibly null.</param>
    /// <returns>The Footnote Label as it is carried on a role and captured (<c>note</c>).</returns>
    internal static string LabelOf(string? parsedLabel)
    {
        var label = parsedLabel ?? string.Empty;
        return label.StartsWith('^') ? label[1..] : label;
    }

    // The Footnote Number shown beside a Definition. It is presentation, not content: Capture emits
    // nothing for this Run, so the number can never reach the Markdown Document (INV-065).
    private static Run CreateNumber(string label, int number)
    {
        var run = new Run(number.ToString(CultureInfo.InvariantCulture) + ". ")
        {
            Tag = new FootnoteNumberRole(label),
        };
        run.SetResourceReference(TextElement.ForegroundProperty, "MutedTextBrush");
        return run;
    }

    // The top-level block a Definition authored on `line` follows: the last one that begins above it.
    // Null means the Definition was authored before any block, and is captured before all of them.
    private static WpfBlock? AnchorFor(int line, IReadOnlyList<(int Line, WpfBlock Block)> anchors)
    {
        WpfBlock? anchor = null;
        foreach (var candidate in anchors)
        {
            if (candidate.Line < line)
            {
                anchor = candidate.Block;
            }
        }

        return anchor;
    }

    private static ParsedDefinition Describe(Footnote footnote, int? number) =>
        new(LabelOf(footnote.Label), number, footnote.Line, [.. footnote]);

    // A Footnote Number is a small raised digit beside its note, not body text at full size.
    private const double ReferenceFontSize = 10;

    private static readonly Thickness DefinitionSpacing = new(0, 0, 0, 4);
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
}
