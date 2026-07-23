using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using UI.Wysiwyg;

namespace UI.Controls;

// Section folding and navigation: hiding a Section Heading's Section Body the way Visual Studio
// collapses a region, revealing it again, and Navigating to a heading. Folding is view-only — Folded
// bodies are retained and spliced back into the logical block sequence when Capturing, so a Fold never
// changes the Markdown source (INV-011/INV-012).
public sealed partial class MarkdownRichEditor
{
    // Each Folded Section Heading mapped to the Section Body blocks removed from the visible Document.
    // The blocks are retained (not discarded) so Capture can reproduce the full source (INV-011).
    private readonly Dictionary<Block, IReadOnlyList<Block>> _foldedBodies = new();

    /// <summary>
    /// Navigates to <paramref name="heading"/>: reveals it (Unfolding its enclosing Section if it is
    /// hidden inside a Folded Section Body), selects it, and scrolls it into view. Navigation is
    /// view-only — it never changes <see cref="Markdown"/> (INV-012).
    /// </summary>
    /// <param name="heading">The Outline Entry to Navigate to.</param>
    public void Navigate(SectionHeading heading)
    {
        ArgumentNullException.ThrowIfNull(heading);

        Reveal(heading.Block);
        if (heading.Block is not Paragraph paragraph || !Document.Blocks.Contains(paragraph))
        {
            return;
        }

        Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
        Focus();
        BringHeadingIntoView(paragraph);
    }

    /// <summary>Whether the Section led by <paramref name="heading"/> is currently Folded.</summary>
    /// <param name="heading">The Section Heading block to query.</param>
    /// <returns><see langword="true"/> if the Section is Folded; otherwise <see langword="false"/>.</returns>
    public bool IsFolded(Block heading) => _foldedBodies.ContainsKey(heading);

    /// <summary>
    /// Whether <paramref name="block"/> is a Section Heading — a heading block that leads a Section
    /// and can therefore be Folded. Used by the Editor Gutter to place a Fold Toggle.
    /// </summary>
    /// <param name="block">The block to classify.</param>
    /// <returns><see langword="true"/> if the block is a Section Heading; otherwise <see langword="false"/>.</returns>
    public bool IsSectionHeading(Block block) => LevelOf(block) is not null;

    /// <summary>
    /// Folds the Section led by <paramref name="heading"/>, hiding its Section Body while leaving the
    /// Section Heading visible. A Fold is view-only and never changes <see cref="Markdown"/>.
    /// </summary>
    /// <param name="heading">The Section Heading block to Fold.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="heading"/> is not a Section Heading.</exception>
    public void Fold(Block heading)
    {
        ArgumentNullException.ThrowIfNull(heading);
        if (LevelOf(heading) is null)
        {
            throw new ArgumentException("Only a Section Heading can be Folded.", nameof(heading));
        }

        if (IsFolded(heading))
        {
            return;
        }

        var blocks = Document.Blocks.ToList();
        var index = blocks.IndexOf(heading);
        if (index < 0)
        {
            return;
        }

        var levels = blocks.ConvertAll(LevelOf);
        var body = SectionMap.FindBody(levels, index);
        if (body.Count == 0)
        {
            return;
        }

        var bodyBlocks = blocks.GetRange(body.Start, body.Count);
        MutateVisualDocument(() =>
        {
            foreach (var block in bodyBlocks)
            {
                Document.Blocks.Remove(block);
            }
        });

        _foldedBodies[heading] = bodyBlocks;
    }

    /// <summary>Unfolds the Section led by <paramref name="heading"/>, restoring its Section Body.</summary>
    /// <param name="heading">The Section Heading block to Unfold.</param>
    public void Unfold(Block heading)
    {
        ArgumentNullException.ThrowIfNull(heading);
        if (!_foldedBodies.TryGetValue(heading, out var bodyBlocks))
        {
            return;
        }

        _foldedBodies.Remove(heading);
        MutateVisualDocument(() =>
        {
            Block cursor = heading;
            foreach (var block in bodyBlocks)
            {
                Document.Blocks.InsertAfter(cursor, block);
                cursor = block;
            }
        });
    }

    /// <summary>Folds the Section if it is Unfolded, or Unfolds it if it is Folded.</summary>
    /// <param name="heading">The Section Heading block to toggle.</param>
    public void ToggleFold(Block heading)
    {
        if (IsFolded(heading))
        {
            Unfold(heading);
        }
        else
        {
            Fold(heading);
        }
    }

    /// <summary>Toggles the Fold of the Section that contains the caret.</summary>
    public void ToggleFoldAtCaret()
    {
        var caretParagraph = CaretPosition?.Paragraph;
        if (caretParagraph is null)
        {
            return;
        }

        var blocks = Document.Blocks.ToList();
        for (var index = blocks.IndexOf(caretParagraph); index >= 0; index--)
        {
            if (LevelOf(blocks[index]) is not null)
            {
                ToggleFold(blocks[index]);
                return;
            }
        }
    }

    /// <summary>
    /// Folds every Section, collapsing the Visual Document down to its top-level Section Headings.
    /// Folding an outer Section hides the Section Headings nested within it, so only the outermost
    /// Sections remain Folded — Expanding them restores the whole document. A view-only operation
    /// (INV-011).
    /// </summary>
    public void CollapseAllFolds()
    {
        // Snapshot the currently visible headings: Folding a Section removes its nested headings from
        // the visible document, so we only ever Fold the outermost Sections still present.
        var headings = Document.Blocks.Where(IsSectionHeading).ToList();
        foreach (var heading in headings)
        {
            if (Document.Blocks.Contains(heading) && !IsFolded(heading))
            {
                Fold(heading);
            }
        }
    }

    /// <summary>Unfolds every Folded Section, restoring the full Visual Document.</summary>
    public void ExpandAllFolds()
    {
        while (_foldedBodies.Count > 0)
        {
            var visibleFold = _foldedBodies.Keys.FirstOrDefault(heading => Document.Blocks.Contains(heading));
            if (visibleFold is null)
            {
                break;
            }

            Unfold(visibleFold);
        }
    }

    /// <summary>
    /// Captures the current Visual Document — including any Folded Section Bodies — back into
    /// canonical Markdown source. Folding never changes this result (INV-011).
    /// </summary>
    /// <returns>The canonical Markdown source text.</returns>
    public string Capture() => _capturer.Capture(BuildLogicalBlocks());

    // The full logical block sequence: every visible block, with each Folded Section Body spliced
    // back in at its Section Heading (recursively, so nested Folds are preserved).
    private List<Block> BuildLogicalBlocks()
    {
        var logical = new List<Block>();
        foreach (var block in Document.Blocks)
        {
            AppendLogical(block, logical);
        }

        return logical;
    }

    private void AppendLogical(Block block, List<Block> logical)
    {
        logical.Add(block);
        if (_foldedBodies.TryGetValue(block, out var bodyBlocks))
        {
            foreach (var child in bodyBlocks)
            {
                AppendLogical(child, logical);
            }
        }
    }

    // Unfolds outward until the target block is a visible block of the document. Each Unfold restores
    // one Folded Section Body; a body may itself contain further Folded Sections, so this repeats
    // until the target surfaces. View-only — Unfold never changes the Markdown source (INV-011/012).
    private void Reveal(Block target)
    {
        while (!Document.Blocks.Contains(target))
        {
            var owner = _foldedBodies.Keys.FirstOrDefault(heading =>
                Document.Blocks.Contains(heading) && BodyContains(_foldedBodies[heading], target));
            if (owner is null)
            {
                break;
            }

            Unfold(owner);
        }
    }

    private bool BodyContains(IReadOnlyList<Block> body, Block target)
    {
        foreach (var block in body)
        {
            if (ReferenceEquals(block, target))
            {
                return true;
            }

            if (_foldedBodies.TryGetValue(block, out var nested) && BodyContains(nested, target))
            {
                return true;
            }
        }

        return false;
    }

    private void BringHeadingIntoView(Paragraph paragraph)
    {
        if (!Document.Blocks.Contains(paragraph))
        {
            return;
        }

        var rect = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (rect == Rect.Empty)
        {
            // Layout after an Unfold can be pending; retry once it settles.
            Dispatcher.BeginInvoke(() => BringHeadingIntoView(paragraph), DispatcherPriority.Loaded);
            return;
        }

        if (RevealRectOverride is { } reveal)
        {
            reveal(rect);
            return;
        }

        ScrollToVerticalOffset(VerticalOffset + rect.Top);
    }
}
