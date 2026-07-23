using System.Windows.Documents;
using Domain;

namespace UI.Controls;

// The document's structural and positional overview surfaced to the panels and the Status Bar: the
// Outline of Section Headings, the Current Section enclosing the caret, and the live document Status
// (word and character counts, reading time, caret line and column). All of it is view-only — reading
// it never changes the Markdown source (INV-012/INV-039).
public sealed partial class MarkdownRichEditor
{
    private List<SectionHeading>? _outline;

    /// <summary>
    /// The Outline: every Section Heading of the Visual Document in document order, each an Outline
    /// Entry carrying its level and text. Headings inside a Folded Section Body are still listed, so
    /// the Outline always mirrors the whole document. Reading the Outline is view-only (INV-012).
    /// </summary>
    public IReadOnlyList<SectionHeading> Outline => _outline ??= BuildOutline();

    /// <summary>
    /// The Current Section: the <see cref="SectionHeading"/> whose Section most immediately encloses
    /// the caret, or <see langword="null"/> when the caret precedes every heading. Used by the
    /// Navigation Panel to highlight where the user is editing.
    /// </summary>
    public SectionHeading? CurrentSection
    {
        get
        {
            var caretParagraph = CaretPosition?.Paragraph;
            if (caretParagraph is null)
            {
                return null;
            }

            var blocks = Document.Blocks.ToList();
            for (var index = blocks.IndexOf(caretParagraph); index >= 0; index--)
            {
                if (LevelOf(blocks[index]) is not null)
                {
                    return Outline.FirstOrDefault(entry => ReferenceEquals(entry.Block, blocks[index]));
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The live document status shown in the Status Bar — word and character counts, reading time,
    /// the caret's line and column, and the Current Section. Presentation-only (INV-039).
    /// </summary>
    public DocumentStatus Status { get; } = new();

    // Recomputes the word / character counts and reading time from the visible document text.
    private void UpdateStatistics()
    {
        var statistics = TextStatistics.Compute(new TextRange(Document.ContentStart, Document.ContentEnd).Text);
        Status.WordCount = statistics.WordCount;
        Status.CharacterCount = statistics.CharacterCount;
        Status.ReadingTime = statistics.ReadingTime;
    }

    // Updates the caret's line and column and the Current Section shown in the Status Bar.
    private void UpdateCaretStatus()
    {
        var caret = CaretPosition;
        caret.GetLineStartPosition(-int.MaxValue, out var linesMovedBack);
        Status.CaretLine = 1 - linesMovedBack;

        var lineStart = caret.GetLineStartPosition(0);
        Status.CaretColumn = lineStart is null ? 1 : new TextRange(lineStart, caret).Text.Length + 1;

        Status.CurrentSection = CurrentSection?.Text ?? string.Empty;
    }

    private List<SectionHeading> BuildOutline()
    {
        var outline = new List<SectionHeading>();
        foreach (var block in BuildLogicalBlocks())
        {
            if (LevelOf(block) is int level)
            {
                outline.Add(new SectionHeading(level, HeadingText(block), block));
            }
        }

        return outline;
    }

    private void InvalidateOutline()
    {
        _outline = null;
        OutlineChanged?.Invoke(this, EventArgs.Empty);
        CurrentSectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
