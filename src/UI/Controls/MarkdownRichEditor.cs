using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UI.Find;
using UI.Spelling;
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

    /// <summary>
    /// Identifies the <see cref="FindQuery"/> dependency property. The Find Bar binds its query box
    /// here; changing it re-runs the Find over the Visual Document.
    /// </summary>
    public static readonly DependencyProperty FindQueryProperty = DependencyProperty.Register(
        nameof(FindQuery),
        typeof(string),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(string.Empty, OnFindQueryChanged));

    /// <summary>
    /// Identifies the <see cref="IsFindActive"/> dependency property — whether the Find Bar is open.
    /// The Find Bar binds its visibility here; the Ctrl+F / Escape commands toggle it.
    /// </summary>
    public static readonly DependencyProperty IsFindActiveProperty = DependencyProperty.Register(
        nameof(IsFindActive),
        typeof(bool),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(false, OnIsFindActiveChanged));

    private static readonly DependencyPropertyKey MatchCountPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(MatchCount),
        typeof(int),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(0));

    /// <summary>Identifies the read-only <see cref="MatchCount"/> dependency property.</summary>
    public static readonly DependencyProperty MatchCountProperty = MatchCountPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey MatchSummaryPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(MatchSummary),
        typeof(string),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(string.Empty));

    /// <summary>Identifies the read-only <see cref="MatchSummary"/> dependency property.</summary>
    public static readonly DependencyProperty MatchSummaryProperty = MatchSummaryPropertyKey.DependencyProperty;

    private readonly MarkdownToFlowDocumentProjector _projector = new();
    private readonly FlowDocumentToMarkdownCapturer _capturer = new();

    // Each Folded Section Heading mapped to the Section Body blocks removed from the visible Document.
    // The blocks are retained (not discarded) so Capture can reproduce the full source (INV-011).
    private readonly Dictionary<Block, IReadOnlyList<Block>> _foldedBodies = new();

    // Created lazily the first time the built-in dictionary is needed, and shared across sessions.
    private static readonly Lazy<ISpellDictionary> SharedDictionary = new(() => new WindowsSpellDictionary());

    private bool _isSynchronising;
    private string _lastCaptured = string.Empty;
    private List<SectionHeading>? _outline;
    private CodeShadingAdorner? _codeShadingAdorner;
    private SpellCheckAdorner? _spellCheckAdorner;

    // Find state: the current Matches as document ranges, and which one is the Current Match. All of
    // it is presentation-only — none of it feeds back into Capture (INV-016).
    private FindHighlightAdorner? _findAdorner;
    private readonly List<TextRange> _matchRanges = [];
    private int _currentMatch = -1;

    /// <summary>Initialises the editor and wires the Section-folding routed commands.</summary>
    public MarkdownRichEditor()
    {
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleFold, (_, _) => ToggleFoldAtCaret()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.CollapseAllFolds, (_, _) => CollapseAllFolds()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ExpandAllFolds, (_, _) => ExpandAllFolds()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ShowFind, (_, _) => IsFindActive = true));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.HideFind, (_, _) => IsFindActive = false));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.FindNext, (_, _) => MoveCurrentMatch(1), CanFindMove));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.FindPrevious, (_, _) => MoveCurrentMatch(-1), CanFindMove));

        // Moving the caret can change which Section the user is editing within; the Navigation Panel
        // listens to keep the Current Section's Outline Entry highlighted.
        SelectionChanged += (_, _) => CurrentSectionChanged?.Invoke(this, EventArgs.Empty);

        // Right-clicking a Misspelling offers its Spelling Suggestions; the menu is built on demand so
        // it reflects the current Misspellings and the word actually under the pointer.
        ContextMenuOpening += OnContextMenuOpening;

        // The Code Shading, the custom camelCase-aware spell checker, and the Find highlights all draw
        // through adorners, which need the editor's AdornerLayer — available only once it is in the
        // visual tree. Code Shading is attached first so its panels sit beneath the squiggles and
        // Find highlights.
        Loaded += (_, _) =>
        {
            AttachCodeShading();
            AttachSpellCheck();
            AttachFind();
        };
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

    /// <summary>The Find query. Every occurrence in the Visual Document is highlighted as a Match.</summary>
    public string FindQuery
    {
        get => (string)GetValue(FindQueryProperty);
        set => SetValue(FindQueryProperty, value);
    }

    /// <summary>Whether the Find Bar is open. Closing it clears the Find highlights.</summary>
    public bool IsFindActive
    {
        get => (bool)GetValue(IsFindActiveProperty);
        set => SetValue(IsFindActiveProperty, value);
    }

    /// <summary>The number of Matches for the current <see cref="FindQuery"/>.</summary>
    public int MatchCount => (int)GetValue(MatchCountProperty);

    /// <summary>
    /// A short summary of the Find result shown in the Find Bar: empty when there is no query,
    /// "No results" when the query matches nothing, or "{ordinal} of {count}" otherwise.
    /// </summary>
    public string MatchSummary => (string)GetValue(MatchSummaryProperty);

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

    // Builds the right-click menu on demand: when the pointer is over a Misspelling, its Spelling
    // Suggestions head the menu (choosing one replaces the word), followed by the usual clipboard
    // commands. Over correctly-spelled text it is just the clipboard commands.
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var clickPosition = e.CursorLeft < 0
            ? CaretPosition                                                  // opened from the keyboard
            : GetPositionFromPoint(Mouse.GetPosition(this), snapToText: true);
        var misspelling = _spellCheckAdorner?.MisspellingAt(clickPosition);

        var menu = new ContextMenu();
        if (misspelling is not null)
        {
            AddSuggestionItems(menu, misspelling);
            menu.Items.Add(new Separator());
        }

        AddClipboardItems(menu);
        ContextMenu = menu;
    }

    private void AddSuggestionItems(ContextMenu menu, TextRange misspelling)
    {
        var suggestions = SpellingSuggestions.For(misspelling.Text, SharedDictionary.Value);
        if (suggestions.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "No suggestions", IsEnabled = false });
            return;
        }

        foreach (var suggestion in suggestions)
        {
            var replacement = suggestion;
            var item = new MenuItem { Header = suggestion, FontWeight = FontWeights.SemiBold };
            item.Click += (_, _) => ReplaceMisspelling(misspelling, replacement);
            menu.Items.Add(item);
        }
    }

    private void AddClipboardItems(ContextMenu menu)
    {
        menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut, CommandTarget = this });
        menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy, CommandTarget = this });
        menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste, CommandTarget = this });
    }

    // Swaps a Misspelling's span for the chosen Spelling Suggestion. Editing the Visual Document
    // Captures back into the Markdown source, so the correction flows through like any other edit.
    private void ReplaceMisspelling(TextRange misspelling, string replacement)
    {
        if (misspelling.Start.HasValidLayout && misspelling.End.HasValidLayout)
        {
            misspelling.Text = replacement;
        }
    }

    // Attaches the Code Shading adorner once, when the editor first has an AdornerLayer. The adorner
    // then watches the editor for edits and repaints the shade behind code itself.
    private void AttachCodeShading()
    {
        if (_codeShadingAdorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null)
        {
            return;
        }

        _codeShadingAdorner = new CodeShadingAdorner(this);
        layer.Add(_codeShadingAdorner);
    }

    // Attaches the spell-check adorner once, when the editor first has an AdornerLayer. The adorner
    // then watches the editor for edits and repaints its squiggles itself.
    private void AttachSpellCheck()
    {
        if (_spellCheckAdorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null)
        {
            return;
        }

        _spellCheckAdorner = new SpellCheckAdorner(this, SharedDictionary.Value);
        layer.Add(_spellCheckAdorner);
    }

    // Attaches the Find highlight adorner once the editor has an AdornerLayer. The editor drives it by
    // calling Update whenever the Matches change; the adorner repaints itself on scroll and resize.
    private void AttachFind()
    {
        if (_findAdorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is null)
        {
            return;
        }

        _findAdorner = new FindHighlightAdorner(this);
        layer.Add(_findAdorner);
    }

    private static void OnFindQueryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MarkdownRichEditor)d).RecomputeMatches();

    private static void OnIsFindActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownRichEditor)d;
        if ((bool)e.NewValue)
        {
            editor.RecomputeMatches();
        }
        else
        {
            editor.ClearFind();
        }
    }

    private void CanFindMove(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = MatchCount > 0;

    // Re-runs the Find over the current Visual Document: builds a text snapshot, finds every Match,
    // maps each to a document range for the adorner, and refreshes the count and Current Match. Never
    // touches the Markdown source (INV-016).
    private void RecomputeMatches()
    {
        _matchRanges.Clear();
        var query = FindQuery;

        if (IsFindActive && !string.IsNullOrEmpty(query) && Document is { } document)
        {
            var (text, anchors) = BuildTextSnapshot(document);
            foreach (var match in MatchFinder.FindMatches(text, query))
            {
                var range = RangeFor(anchors, match);
                if (range is not null)
                {
                    _matchRanges.Add(range);
                }
            }
        }

        _currentMatch = _matchRanges.Count == 0
            ? -1
            : Math.Clamp(_currentMatch < 0 ? 0 : _currentMatch, 0, _matchRanges.Count - 1);

        PublishFindState();
        ScrollCurrentMatchIntoView();
    }

    // Moves the Current Match forward (+1) or backward (-1), wrapping around, and reveals it.
    private void MoveCurrentMatch(int delta)
    {
        if (_matchRanges.Count == 0)
        {
            return;
        }

        _currentMatch = MatchFinder.Advance(_currentMatch, delta, _matchRanges.Count);
        PublishFindState();
        ScrollCurrentMatchIntoView();
    }

    private void ClearFind()
    {
        _matchRanges.Clear();
        _currentMatch = -1;
        PublishFindState();
    }

    // Pushes the current Match ranges and counts to the adorner and the read-only properties the Find
    // Bar binds to.
    private void PublishFindState()
    {
        SetValue(MatchCountPropertyKey, _matchRanges.Count);
        SetValue(MatchSummaryPropertyKey, BuildMatchSummary());

        if (_findAdorner is not null)
        {
            _findAdorner.SetColors(HighlightBrush(0.25), HighlightBrush(0.5), CurrentMatchOutline());
            _findAdorner.Update(_matchRanges, _currentMatch);
        }
    }

    private string BuildMatchSummary()
    {
        if (string.IsNullOrEmpty(FindQuery))
        {
            return string.Empty;
        }

        return _matchRanges.Count == 0
            ? "No results"
            : $"{_currentMatch + 1} of {_matchRanges.Count}";
    }

    private void ScrollCurrentMatchIntoView()
    {
        if (_currentMatch < 0 || _currentMatch >= _matchRanges.Count)
        {
            return;
        }

        var rect = _matchRanges[_currentMatch].Start.GetCharacterRect(LogicalDirection.Forward);
        if (rect == Rect.Empty)
        {
            return;
        }

        // Only scroll when the Current Match is outside the viewport, leaving a small margin so it is
        // not flush against the top or bottom edge.
        const double margin = 40d;
        if (rect.Top < 0)
        {
            ScrollToVerticalOffset(VerticalOffset + rect.Top - margin);
        }
        else if (rect.Bottom > ActualHeight)
        {
            ScrollToVerticalOffset(VerticalOffset + (rect.Bottom - ActualHeight) + margin);
        }
    }

    private SolidColorBrush HighlightBrush(double opacity)
    {
        var color = (TryFindResource("AccentBrush") as SolidColorBrush)?.Color ?? Colors.MediumPurple;
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    private Pen CurrentMatchOutline()
    {
        var color = (TryFindResource("AccentBrush") as SolidColorBrush)?.Color ?? Colors.MediumPurple;
        var pen = new Pen(new SolidColorBrush(color), 1.2d);
        pen.Freeze();
        return pen;
    }

    // Maps a Match (offsets into the text snapshot) to a document range, resolving its start and end
    // through their anchoring text runs — so a Match that spans an inline formatting boundary still
    // yields one contiguous range.
    private static TextRange? RangeFor(IReadOnlyList<(int Offset, int Length, TextPointer Pointer)> anchors, Match match)
    {
        var start = PointerAt(anchors, match.Start, atEnd: false);
        var end = PointerAt(anchors, match.Start + match.Length, atEnd: true);
        return start is null || end is null ? null : new TextRange(start, end);
    }

    private static TextPointer? PointerAt(
        IReadOnlyList<(int Offset, int Length, TextPointer Pointer)> anchors,
        int index,
        bool atEnd)
    {
        foreach (var anchor in anchors)
        {
            // For a span's end, the character before the index must lie in this run (offset < index);
            // for a start, the character at the index must (offset <= index).
            var withinStart = atEnd ? index > anchor.Offset : index >= anchor.Offset;
            var withinEnd = index <= anchor.Offset + anchor.Length;
            if (withinStart && withinEnd)
            {
                return anchor.Pointer.GetPositionAtOffset(index - anchor.Offset, LogicalDirection.Forward);
            }
        }

        return null;
    }

    // A plain-text snapshot of the Visual Document for searching, with an anchor per text run recording
    // where that run's text begins in the snapshot. A separator is inserted at block boundaries so a
    // Match never bridges two blocks, while adjacent inline runs are concatenated so a Match may span a
    // formatting boundary within a line.
    private static (string Text, List<(int Offset, int Length, TextPointer Pointer)> Anchors) BuildTextSnapshot(
        FlowDocument document)
    {
        var builder = new StringBuilder();
        var anchors = new List<(int, int, TextPointer)>();
        var pointer = document.ContentStart;

        while (pointer is not null)
        {
            switch (pointer.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    var runText = pointer.GetTextInRun(LogicalDirection.Forward);
                    anchors.Add((builder.Length, runText.Length, pointer));
                    builder.Append(runText);
                    pointer = pointer.GetPositionAtOffset(runText.Length, LogicalDirection.Forward);
                    break;

                case TextPointerContext.ElementStart:
                case TextPointerContext.ElementEnd:
                    if (pointer.GetAdjacentElement(LogicalDirection.Forward) is Block or LineBreak
                        && builder.Length > 0 && builder[^1] != '\n')
                    {
                        builder.Append('\n');
                    }

                    pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                    break;

                default:
                    pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                    break;
            }
        }

        return (builder.ToString(), anchors);
    }

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

        // A fresh document invalidates the Match ranges (they point into the old one); re-run the Find
        // once the new document has laid out so the highlights follow the content.
        if (IsFindActive)
        {
            Dispatcher.BeginInvoke(RecomputeMatches, DispatcherPriority.Loaded);
        }
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

        // The edited text may have gained or lost Matches; keep the highlights current.
        if (IsFindActive)
        {
            RecomputeMatches();
        }
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
