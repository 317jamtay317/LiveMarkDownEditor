using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using UI.Wysiwyg;

namespace UI.Controls;

/// <summary>
/// The single-pane WYSIWYG editing surface: a <see cref="RichTextBox"/> whose content is a Visual
/// Document projected from Markdown. Formatting is shown as formatting — the user never sees raw
/// Markdown syntax — while the canonical Markdown source is exposed through <see cref="Markdown"/>.
/// </summary>
/// <remarks>
/// This is a custom Control (the only place interaction logic lives outside a ViewModel). It keeps
/// <see cref="Markdown"/> and its <see cref="RichTextBox.Document"/> in sync: assigning
/// <see cref="Markdown"/> Projects it into the Visual Document; editing the Visual Document Captures
/// it back into <see cref="Markdown"/>. A re-entrancy guard prevents the two directions from
/// echoing each other.
/// <para>
/// The control also supports Folding a Section — hiding a Section Heading's Section Body the way
/// Visual Studio collapses a region. Folding is view-only: Folded bodies are retained and spliced
/// back in when Capturing, so a Fold never changes the Markdown source (INV-011).
/// </para>
/// </remarks>
public sealed class MarkdownRichEditor : RichTextBox
{
    /// <summary>
    /// Identifies the <see cref="Markdown"/> dependency property. Binds two-way by default so the
    /// canonical Markdown source flows to and from the bound Editor Session.
    /// </summary>
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownRichEditor),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnMarkdownChanged));

    private readonly MarkdownToFlowDocumentProjector _projector = new();
    private readonly FlowDocumentToMarkdownCapturer _capturer = new();

    // Each Folded Section Heading mapped to the Section Body blocks removed from the visible Document.
    // The blocks are retained (not discarded) so Capture can reproduce the full source (INV-011).
    private readonly Dictionary<Block, IReadOnlyList<Block>> _foldedBodies = new();

    private bool _isSynchronising;
    private string _lastCaptured = string.Empty;
    private List<SectionHeading>? _outline;

    /// <summary>Initialises the editor and wires the Section-folding routed commands.</summary>
    public MarkdownRichEditor()
    {
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleFold, (_, _) => ToggleFoldAtCaret()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.CollapseAllFolds, (_, _) => CollapseAllFolds()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ExpandAllFolds, (_, _) => ExpandAllFolds()));

        // Moving the caret can change which Section the user is editing within; the Navigation Panel
        // listens to keep the Current Section's Outline Entry highlighted.
        SelectionChanged += (_, _) => CurrentSectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raised when the <see cref="Outline"/> may have changed — after a re-projection or an edit that
    /// alters the Section Headings. The Navigation Panel refreshes its Outline Entries in response.
    /// </summary>
    public event EventHandler? OutlineChanged;

    /// <summary>
    /// Raised when the <see cref="CurrentSection"/> may have changed — when the caret moves or the
    /// document is re-projected. The Navigation Panel re-highlights the Current Section in response.
    /// </summary>
    public event EventHandler? CurrentSectionChanged;

    /// <summary>The canonical Markdown source text shown, and edited, as a Visual Document.</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

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

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownRichEditor)d;
        if (editor._isSynchronising)
        {
            return;
        }

        var markdown = (string?)e.NewValue ?? string.Empty;
        if (markdown == editor._lastCaptured)
        {
            // This change is the echo of our own Capture; the Visual Document already reflects it.
            return;
        }

        editor.ProjectFromMarkdown(markdown);
    }

    private void ProjectFromMarkdown(string markdown)
    {
        _isSynchronising = true;
        try
        {
            // Fold state references the outgoing document's blocks; a fresh projection clears it.
            _foldedBodies.Clear();
            Document = _projector.Project(markdown);
        }
        finally
        {
            _isSynchronising = false;
        }

        // Track the source now shown so the echo check in OnMarkdownChanged reflects the current
        // Visual Document. Without this, re-binding to a different session whose Markdown happens to
        // equal the stale value (e.g. switching back to an empty tab) would be mistaken for an echo
        // and skip re-projection, leaving the previous document visible.
        _lastCaptured = markdown;

        InvalidateOutline();
    }

    /// <inheritdoc />
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        if (_isSynchronising)
        {
            return;
        }

        _isSynchronising = true;
        try
        {
            _lastCaptured = _capturer.Capture(BuildLogicalBlocks());
            SetCurrentValue(MarkdownProperty, _lastCaptured);
        }
        finally
        {
            _isSynchronising = false;
        }

        // An edit may have added, removed, or retitled a Section Heading.
        InvalidateOutline();
    }

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

    private void MutateVisualDocument(Action mutate)
    {
        _isSynchronising = true;
        try
        {
            mutate();
        }
        finally
        {
            _isSynchronising = false;
        }
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

        ScrollToVerticalOffset(VerticalOffset + rect.Top);
    }

    private static string HeadingText(Block block) =>
        new TextRange(block.ContentStart, block.ContentEnd).Text.Trim();

    private static int? LevelOf(Block block) =>
        block is Paragraph { Tag: HeadingRole role } ? role.Level : null;
}
