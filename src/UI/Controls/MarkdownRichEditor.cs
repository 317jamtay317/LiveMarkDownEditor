using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Domain;
using UI.Core;
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
    /// Identifies the <see cref="BaseDirectory"/> dependency property. It is the folder the bound
    /// Editor Session's file lives in, and the folder a relative Image Source resolves against
    /// (INV-031). Changing it re-projects, because the same source text against a different Base
    /// Directory names different pictures (INV-003).
    /// </summary>
    public static readonly DependencyProperty BaseDirectoryProperty = DependencyProperty.Register(
        nameof(BaseDirectory),
        typeof(string),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null, OnBaseDirectoryChanged));

    /// <summary>
    /// Identifies the <see cref="LinkPrompt"/> dependency property. Insert Link and Insert Image ask
    /// through it for their text and URL; the composition root supplies the real Link Prompt, and a
    /// test supplies a stub. Left unset, neither action edits (INV-030).
    /// </summary>
    public static readonly DependencyProperty LinkPromptProperty = DependencyProperty.Register(
        nameof(LinkPrompt),
        typeof(ILinkPrompt),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// Identifies the <see cref="DocumentPrinter"/> dependency property. Print sends the Visual
    /// Document through it; the composition root supplies the real printer, and a test supplies a
    /// fake. Left unset, Print does nothing (INV-034).
    /// </summary>
    public static readonly DependencyProperty DocumentPrinterProperty = DependencyProperty.Register(
        nameof(DocumentPrinter),
        typeof(IDocumentPrinter),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// Identifies the <see cref="Renderer"/> dependency property. A Copy renders the selection to
    /// HTML through it for the clipboard's HTML flavor; the composition root supplies the real
    /// renderer. Left unset, Copy adds no HTML flavor (the built-in rich text is unaffected) (INV-035).
    /// </summary>
    public static readonly DependencyProperty RendererProperty = DependencyProperty.Register(
        nameof(Renderer),
        typeof(IMarkdownRenderer),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// Identifies the <see cref="FollowLinkCommand"/> dependency property. Ctrl+Clicking a Link to a
    /// Markdown file invokes it with the file's absolute path so the shell opens it in a new Tab; a
    /// web Link is opened in the browser without it (INV-038).
    /// </summary>
    public static readonly DependencyProperty FollowLinkCommandProperty = DependencyProperty.Register(
        nameof(FollowLinkCommand),
        typeof(ICommand),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

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

    /// <summary>
    /// Identifies the <see cref="Replacement"/> dependency property. The Find Bar's Replace Row binds
    /// its Replacement box here. Unlike the other Find state, using it is a real edit (INV-022).
    /// </summary>
    public static readonly DependencyProperty ReplacementProperty = DependencyProperty.Register(
        nameof(Replacement),
        typeof(string),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="IsReplaceActive"/> dependency property — whether the Find Bar's
    /// Replace Row is shown. The Ctrl+H command opens the Find Bar with it; Ctrl+F opens without it.
    /// </summary>
    public static readonly DependencyProperty IsReplaceActiveProperty = DependencyProperty.Register(
        nameof(IsReplaceActive),
        typeof(bool),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(false));

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

    // The User Dictionary of accepted words, shared across sessions and persisted to per-user storage.
    private static readonly Lazy<IUserDictionary> SharedUserDictionary = new(CreateUserDictionary);

    // Created lazily the first time the dictionary is needed, and shared across sessions. The
    // operating system's speller, made aware of the User Dictionary so an accepted word is not a
    // Misspelling (INV-040).
    private static readonly Lazy<ISpellDictionary> SharedDictionary = new(() =>
        new UserAwareSpellDictionary(new WindowsSpellDictionary(), SharedUserDictionary.Value));

    private static IUserDictionary CreateUserDictionary() => new FileUserDictionary(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LiveMarkDownEditor",
        "user-dictionary.txt"));

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

    // Set while a Replace All batch is running, so the per-edit Recompute is suppressed: it would
    // clear the Match ranges the batch is iterating. The batch Recomputes once when it finishes.
    private bool _isReplacing;

    /// <summary>Initialises the editor and wires the Section-folding routed commands.</summary>
    public MarkdownRichEditor()
    {
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.Print, (_, _) => PrintVisualDocument()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.CopyAsMarkdown,
            (_, _) => CopySelectionAsMarkdown(),
            (_, e) => e.CanExecute = !Selection.IsEmpty));

        // A Copy also carries an HTML flavor, so a selection pastes formatted into web editors, not
        // only into the RTF-native Word and Outlook the RichTextBox already serves (INV-035).
        DataObject.AddCopyingHandler(this, OnCopying);

        // Ctrl+Clicking a Link follows it: a web URL to the browser, a relative .md into a new Tab.
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));

        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleFold, (_, _) => ToggleFoldAtCaret()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.CollapseAllFolds, (_, _) => CollapseAllFolds()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ExpandAllFolds, (_, _) => ExpandAllFolds()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleCode,
            (_, _) => ToggleCodeAtSelection(),
            (_, e) => e.CanExecute = CodeFormatting.CanToggle(this)));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.InsertLink, (_, _) => InsertLinkAtSelection()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.InsertImage, (_, _) => InsertImageAtSelection()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleStrikethrough,
            (_, _) => ToggleStrikethroughAtSelection(),
            (_, e) => e.CanExecute = StrikethroughFormatting.CanToggle(this)));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleBlockQuote,
            (_, _) => ToggleBlockQuoteAtSelection(),
            (_, e) => e.CanExecute = QuoteFormatting.CanToggle(this)));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.SetHeadingLevel,
            (_, e) => SetHeadingLevelAtCaret(e.Parameter),
            (_, e) => e.CanExecute = HeadingFormatting.CanSetLevel(this)));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.InsertTable,
            (_, _) => InsertTableAtCaret(),
            (_, e) => e.CanExecute = !IsCaretInTable));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.AddTableRow,
            (_, _) => AddTableRowAtCaret(),
            (_, e) => e.CanExecute = IsCaretInTable));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.AddTableColumn,
            (_, _) => AddTableColumnAtCaret(),
            (_, e) => e.CanExecute = IsCaretInTable));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleUnorderedList, (_, _) => ToggleUnorderedListAtSelection()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleOrderedList, (_, _) => ToggleOrderedListAtSelection()));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ToggleTaskList,
            (_, _) => ToggleTaskListAtSelection(),
            (_, e) => e.CanExecute = ListFormatting.CanToggleTaskList(this)));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ShowFind, (_, _) =>
            {
                IsReplaceActive = false;
                IsFindActive = true;
            }));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ShowReplace, (_, _) =>
            {
                IsReplaceActive = true;
                IsFindActive = true;
            }));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.HideFind, (_, _) =>
            {
                IsFindActive = false;
                IsReplaceActive = false;
            }));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.FindNext, (_, _) => MoveCurrentMatch(1), CanFindMove));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.FindPrevious, (_, _) => MoveCurrentMatch(-1), CanFindMove));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.Replace, (_, _) => ReplaceCurrentMatch(), CanFindMove));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.ReplaceAll, (_, _) => ReplaceAllMatches(), CanReplaceAll));

        // Moving the caret can change which Section the user is editing within; the Navigation Panel
        // listens to keep the Current Section's Outline Entry highlighted.
        SelectionChanged += (_, _) =>
        {
            UpdateCaretStatus();
            CurrentSectionChanged?.Invoke(this, EventArgs.Empty);
        };

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

    /// <summary>
    /// The Base Directory: the folder the Editor Session's file lives in, which a relative Image
    /// Source resolves against. <see langword="null"/> for an unsaved Editor Session, whose relative
    /// Images fall back to their alt text — there is no folder yet for "beside this document" to
    /// mean (INV-031).
    /// </summary>
    public string? BaseDirectory
    {
        get => (string?)GetValue(BaseDirectoryProperty);
        set => SetValue(BaseDirectoryProperty, value);
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

    /// <summary>The Replacement: the text a Match is swapped for, inserted verbatim (INV-022).</summary>
    public string Replacement
    {
        get => (string)GetValue(ReplacementProperty);
        set => SetValue(ReplacementProperty, value);
    }

    /// <summary>Whether the Find Bar's Replace Row is shown.</summary>
    public bool IsReplaceActive
    {
        get => (bool)GetValue(IsReplaceActiveProperty);
        set => SetValue(IsReplaceActiveProperty, value);
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
    /// Whether the caret currently sits inside a Table — the availability switch for the Table
    /// Formatting Actions: Insert Table runs only outside a Table; Add Row and Add Column run only
    /// inside one (INV-018).
    /// </summary>
    public bool IsCaretInTable => TableEditing.IsInTable(CaretPosition);

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

    /// <summary>
    /// Captures the Markdown source of the blocks the current selection spans. A partial selection
    /// captures the whole blocks it touches (whole-block granularity); an empty selection captures
    /// nothing. It reads the document and is not an edit (INV-035).
    /// </summary>
    /// <returns>The canonical Markdown source of the selected blocks; the empty string for no selection.</returns>
    public string CaptureSelection()
    {
        if (Selection.IsEmpty)
        {
            return string.Empty;
        }

        var start = Selection.Start;
        var end = Selection.End;
        var selectedBlocks = Document.Blocks.Where(block => Overlaps(block, start, end)).ToList();
        return _capturer.Capture(selectedBlocks);
    }

    /// <summary>
    /// Renders the current selection to the CF_HTML clipboard flavor, or <see langword="null"/> when
    /// there is no selection or no <see cref="Renderer"/>. A Copy adds this so a selection pastes
    /// formatted into web editors (INV-035).
    /// </summary>
    /// <returns>The CF_HTML string, or <see langword="null"/> when no HTML flavor should be added.</returns>
    public string? SelectionAsCfHtml()
    {
        if (Renderer is null)
        {
            return null;
        }

        var markdown = CaptureSelection();
        if (string.IsNullOrEmpty(markdown))
        {
            return null;
        }

        var html = Renderer.Render(new MarkdownDocument(markdown)).Html;
        return CfHtml.Wrap(html);
    }

    // Adds the HTML flavor to a Copy (or Cut / drag) so the selection pastes formatted into web
    // editors; the RichTextBox's own RTF and text flavors are left untouched.
    private void OnCopying(object sender, DataObjectCopyingEventArgs e)
    {
        var cfHtml = SelectionAsCfHtml();
        if (cfHtml is not null)
        {
            e.DataObject.SetData(DataFormats.Html, cfHtml);
        }
    }

    // Copies the selection's Markdown source to the clipboard for Copy as Markdown.
    private void CopySelectionAsMarkdown()
    {
        var markdown = CaptureSelection();
        if (!string.IsNullOrEmpty(markdown))
        {
            Clipboard.SetText(markdown);
        }
    }

    // Whether a top-level Block's content range overlaps the selection [start, end].
    private static bool Overlaps(Block block, TextPointer start, TextPointer end) =>
        block.ContentStart.CompareTo(end) < 0 && block.ContentEnd.CompareTo(start) > 0;

    /// <summary>
    /// Applies the Toggle Code Formatting Action at the current selection: a selection within a
    /// single line becomes a Code Span, a selection spanning multiple lines (or a whole line)
    /// becomes a Code Block, and inside existing code the code formatting is removed. The edit
    /// Captures back into <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    public void ToggleCodeAtSelection() => CodeFormatting.Toggle(this);

    /// <summary>
    /// The Link Prompt that Insert Link and Insert Image ask for a text and URL. Left
    /// <see langword="null"/>, neither action makes an edit (INV-030).
    /// </summary>
    public ILinkPrompt? LinkPrompt
    {
        get => (ILinkPrompt?)GetValue(LinkPromptProperty);
        set => SetValue(LinkPromptProperty, value);
    }

    /// <summary>
    /// The printer Print sends the Visual Document to. Supplied by the composition root; when
    /// <see langword="null"/>, Print does nothing (INV-034).
    /// </summary>
    public IDocumentPrinter? DocumentPrinter
    {
        get => (IDocumentPrinter?)GetValue(DocumentPrinterProperty);
        set => SetValue(DocumentPrinterProperty, value);
    }

    /// <summary>
    /// The renderer a Copy uses to render the selection to HTML for the clipboard's HTML flavor.
    /// Supplied by the composition root; when <see langword="null"/>, Copy adds no HTML flavor and the
    /// built-in rich text (RTF) is unaffected (INV-035).
    /// </summary>
    public IMarkdownRenderer? Renderer
    {
        get => (IMarkdownRenderer?)GetValue(RendererProperty);
        set => SetValue(RendererProperty, value);
    }

    /// <summary>
    /// The command invoked to open a followed Markdown Link in a new Tab, with the file's absolute
    /// path as its parameter. Supplied by the composition root; when <see langword="null"/>, a
    /// Markdown Link is not followed (a web Link still opens in the browser) (INV-038).
    /// </summary>
    public ICommand? FollowLinkCommand
    {
        get => (ICommand?)GetValue(FollowLinkCommandProperty);
        set => SetValue(FollowLinkCommandProperty, value);
    }

    /// <summary>
    /// The live document status shown in the Status Bar — word and character counts, reading time,
    /// the caret's line and column, and the Current Section. Presentation-only (INV-039).
    /// </summary>
    public DocumentStatus Status { get; } = new();

    /// <summary>
    /// Follows a Link's destination: a web address opens in the default browser, a Markdown file opens
    /// in a new Tab through <see cref="FollowLinkCommand"/>, and anything else is left alone. Following
    /// reads the document and is not an edit (INV-038).
    /// </summary>
    /// <param name="uri">The Link's destination, as its <c>NavigateUri</c>.</param>
    public void FollowLink(Uri uri)
    {
        var target = MarkdownLink.Classify(uri, BaseDirectory);
        switch (target.Kind)
        {
            case LinkKind.Web:
                LaunchBrowser(target.Value);
                break;
            case LinkKind.MarkdownFile when FollowLinkCommand?.CanExecute(target.Value) == true:
                FollowLinkCommand.Execute(target.Value);
                break;
        }
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        FollowLink(e.Uri);
        e.Handled = true;
    }

    // Opens a web address in the default browser. A platform boundary — the shell picks the browser.
    private static void LaunchBrowser(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    /// <summary>
    /// Prints the whole document. The Visual Document is re-projected from the current
    /// <see cref="Markdown"/> source rather than taken from the live editing surface, so a Folded
    /// Section's hidden Section Body prints too — Print means the whole document, never merely the
    /// visible part (INV-034, the fold rule of INV-032 reached from printing) — and the surface the
    /// user is editing is left undisturbed. Printing reads the document and writes no file the editor
    /// owns, so it is not an edit. Does nothing when no <see cref="DocumentPrinter"/> is set.
    /// </summary>
    public void PrintVisualDocument()
    {
        if (DocumentPrinter is null)
        {
            return;
        }

        var document = _projector.Project(Markdown, BaseDirectory);
        DocumentPrinter.Print(document, "LiveMarkDownEditor document");
    }

    /// <summary>
    /// Applies the Insert Link Formatting Action: asks the <see cref="LinkPrompt"/> for the Link's
    /// text (seeded with the selection) and destination URL, and turns the selection into that Link.
    /// No edit is made when the Link Prompt is dismissed or gives no URL (INV-030). The edit Captures
    /// back into <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    public void InsertLinkAtSelection() => LinkFormatting.InsertLink(this, LinkPrompt);

    /// <summary>
    /// Applies the Insert Image Formatting Action: asks the <see cref="LinkPrompt"/> for the Image's
    /// alt text (seeded with the selection) and source URL, and inserts that Image. No edit is made
    /// when the Link Prompt is dismissed or gives no URL (INV-030). The edit Captures back into
    /// <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    public void InsertImageAtSelection() => LinkFormatting.InsertImage(this, LinkPrompt, BaseDirectory);

    /// <summary>
    /// Applies the Toggle Strikethrough Formatting Action at the current selection: the selection is
    /// struck through, or struck-through prose is restored to plain text — whether that
    /// Strikethrough was loaded or applied by a previous toggle (INV-029). The edit Captures back
    /// into <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    public void ToggleStrikethroughAtSelection() => StrikethroughFormatting.Toggle(this);

    /// <summary>
    /// Applies the Toggle Block Quote Formatting Action at the current selection: the whole blocks
    /// the selection touches become a Block Quote, or the selected Block Quote's blocks become plain
    /// blocks again (INV-028). The edit Captures back into <see cref="Markdown"/> like any other
    /// edit (INV-018).
    /// </summary>
    public void ToggleBlockQuoteAtSelection() => QuoteFormatting.Toggle(this);

    /// <summary>
    /// Applies the Set Heading Level Formatting Action at the caret: the block at the caret becomes a
    /// Heading at <paramref name="level"/> (1–6), or a plain paragraph again given
    /// <see cref="MarkdownEditingCommands.ParagraphHeadingLevel"/>. It sets a level rather than
    /// toggling one, and its content survives the change (INV-027). The edit Captures back into
    /// <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    /// <param name="level">The Heading Level to set, or the Paragraph level to clear the Heading.</param>
    public void SetHeadingLevelAtCaret(int level) => HeadingFormatting.SetLevel(this, level);

    // The Heading Level Picker's XAML passes its CommandParameter as a string ("2"), while a test or
    // caller passes an int — so the parameter is resolved to a level before the action runs. An
    // unreadable parameter names no level, and so relevels nothing.
    private void SetHeadingLevelAtCaret(object? parameter)
    {
        if (parameter is int level)
        {
            SetHeadingLevelAtCaret(level);
        }
        else if (int.TryParse(parameter?.ToString(), out var parsed))
        {
            SetHeadingLevelAtCaret(parsed);
        }
    }

    /// <summary>
    /// Applies the Insert Table Formatting Action: inserts a new three-column Table (header row plus
    /// two empty body rows) at the caret and selects the first header cell. No-op while the caret is
    /// inside a Table (INV-018).
    /// </summary>
    public void InsertTableAtCaret() => TableEditing.InsertTable(this);

    /// <summary>
    /// Applies the Add Row Formatting Action: inserts a new empty row below the caret's row, at the
    /// Table's column count (INV-019). No-op while the caret is not inside a Table.
    /// </summary>
    public void AddTableRowAtCaret() => TableEditing.AddRow(this);

    /// <summary>
    /// Applies the Add Column Formatting Action: inserts a new empty column to the right of the
    /// caret's column, extending every row (INV-019). No-op while the caret is not inside a Table.
    /// </summary>
    public void AddTableColumnAtCaret() => TableEditing.AddColumn(this);

    /// <summary>
    /// Applies the Toggle Unordered List Formatting Action at the current selection: the selected
    /// paragraphs become an Unordered List, an Unordered List becomes plain paragraphs again, and an
    /// Ordered List is converted rather than removed. The items' content is preserved (INV-023) and
    /// the edit Captures back into <see cref="Markdown"/> like any other (INV-018).
    /// </summary>
    public void ToggleUnorderedListAtSelection() => ListFormatting.ToggleUnordered(this);

    /// <summary>
    /// Applies the Toggle Ordered List Formatting Action at the current selection — the counterpart
    /// of <see cref="ToggleUnorderedListAtSelection"/> (INV-018, INV-023).
    /// </summary>
    public void ToggleOrderedListAtSelection() => ListFormatting.ToggleOrdered(this);

    /// <summary>
    /// Applies the Toggle Task List Formatting Action at the current selection: gives every selected
    /// List Item lacking one an unchecked Task Marker, or clears them all when every selected List
    /// Item already carries one. No-op while the selection is not inside a List (INV-023).
    /// </summary>
    public void ToggleTaskListAtSelection() => ListFormatting.ToggleTaskList(this);

    /// <summary>
    /// Continues a Task List across a paragraph break: when the caret sits in a task item, breaks the
    /// line and gives the new List Item its own unchecked Task Marker, the way a bullet or a number
    /// carries to the next item (INV-023). Called by the control's Enter handling.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the break was handled; <see langword="false"/> when the caret is
    /// not in a task item, so Enter should behave as it normally does.
    /// </returns>
    public bool ContinueTaskListAtCaret() => ListFormatting.TryContinueTaskList(this);

    /// <summary>
    /// Gives the List Item at the caret an unchecked Task Marker when the item before it has one and
    /// it does not — the rule that makes Enter continue a Task List (INV-023). A no-op anywhere else.
    /// </summary>
    /// <returns><see langword="true"/> when a Task Marker was added.</returns>
    public bool MarkContinuedTaskItemAtCaret() => ListFormatting.MarkContinuedTaskItem(this);

    /// <summary>
    /// Applies the Toggle Task Marker edit: flips the Task Marker at <paramref name="position"/>
    /// between unchecked and checked, changing nothing else (INV-024).
    /// </summary>
    /// <param name="position">The position to toggle at — where the user clicked.</param>
    /// <returns>
    /// <see langword="true"/> when a Task Marker was toggled; <see langword="false"/> when the
    /// position is not on one, so the click should place the caret as usual.
    /// </returns>
    public bool ToggleTaskMarkerAt(TextPointer? position) => TaskMarkerEditing.Toggle(this, position);

    /// <summary>
    /// Replaces the Current Match with the <see cref="Replacement"/> and moves to the next Match.
    /// Unlike Find, this is a real edit: it Captures back into <see cref="Markdown"/> (INV-022).
    /// No-op while there is no Current Match.
    /// </summary>
    public void ReplaceCurrentMatch()
    {
        if (_currentMatch < 0 || _currentMatch >= _matchRanges.Count)
        {
            return;
        }

        MatchReplacer.Replace(_matchRanges[_currentMatch], Replacement);

        // The edit already drove a Recompute through OnTextChanged, which clamped the Current Match:
        // the replaced Match is gone from the list, so the same index now names the following Match.
    }

    /// <summary>
    /// Replaces every Match in the Markdown Document with the <see cref="Replacement"/>, in a single
    /// undoable edit that Captures back into <see cref="Markdown"/> (INV-022). Every Folded Section
    /// is Unfolded first so an occurrence hidden in a Section Body is not missed — Find searches only
    /// the visible Visual Document.
    /// </summary>
    public void ReplaceAllMatches()
    {
        // Unfolding is view-only (INV-011), but it does reveal Matches Find could not see.
        ExpandAllFolds();
        RecomputeMatches();

        if (_matchRanges.Count == 0)
        {
            return;
        }

        // Replace a snapshot of the ranges taken before the first edit: each Match is replaced exactly
        // once, so a Replacement containing the query cannot cascade. The guard suppresses the
        // per-edit Recompute (which would clear the very list being iterated); BeginChange/EndChange
        // coalesces the batch into one undo unit.
        var matches = _matchRanges.ToList();
        _isReplacing = true;
        BeginChange();
        try
        {
            MatchReplacer.ReplaceAll(matches, Replacement);
        }
        finally
        {
            EndChange();
            _isReplacing = false;
        }

        RecomputeMatches();
    }

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

            var word = misspelling.Text;
            var addToDictionary = new MenuItem { Header = "Add to Dictionary" };
            addToDictionary.Click += (_, _) => AddToDictionary(word);
            menu.Items.Add(addToDictionary);

            menu.Items.Add(new Separator());
        }

        AddClipboardItems(menu);
        ContextMenu = menu;
    }

    // Accepts a Misspelling into the User Dictionary and re-checks, so it stops being marked (INV-040).
    private void AddToDictionary(string word)
    {
        SharedUserDictionary.Value.Add(word);
        _spellCheckAdorner?.Refresh();
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
        menu.Items.Add(new MenuItem
        {
            Header = "Copy as Markdown",
            Command = MarkdownEditingCommands.CopyAsMarkdown,
            CommandTarget = this,
        });
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

    // Find Next / Find Previous / Replace all act on the Current Match, so they need one.
    private void CanFindMove(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = MatchCount > 0;

    // Replace All, by contrast, is available whenever there is a query — never gated on the Match
    // count. Find sees only the visible Visual Document, so the occurrences Replace All exists to
    // catch may all be hidden inside Folded Sections, leaving the count at zero; it Unfolds and
    // re-finds for itself (INV-022). Gating it on the count would disable the command in exactly the
    // case it is there to handle.
    private void CanReplaceAll(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = !string.IsNullOrEmpty(FindQuery);

    // Re-runs the Find over the current Visual Document and refreshes the ranges the adorner paints,
    // the count, and the Current Match. The scan itself lives in the pure MatchScanner and never
    // touches the Markdown source (INV-016).
    private void RecomputeMatches()
    {
        _matchRanges.Clear();

        if (IsFindActive)
        {
            _matchRanges.AddRange(MatchScanner.Scan(Document, FindQuery));
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

    // The same source text against a different Base Directory names different pictures, so a Session
    // saved to a new folder — or an unsaved one gaining its first file — must re-project (INV-031).
    private static void OnBaseDirectoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownRichEditor)d;
        if (!editor._isSynchronising)
        {
            editor.ProjectFromMarkdown(editor.Markdown);
        }
    }

    private void ProjectFromMarkdown(string markdown)
    {
        _isSynchronising = true;
        try
        {
            // Fold state references the outgoing document's blocks; a fresh projection clears it.
            _foldedBodies.Clear();
            Document = _projector.Project(markdown, BaseDirectory);
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
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Enter in a Task List carries the checkbox to the new item, the way WPF carries a bullet or
        // a number (INV-023). Shift+Enter is a soft break within the same item, so it is left alone.
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None && ContinueTaskListAtCaret())
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // A click on a Task Marker's checkbox toggles it (INV-024). snapToText is off so only a click
        // on the checkbox itself resolves to it; every other click falls through to the base class and
        // places the caret exactly as it always has.
        if (ToggleTaskMarkerAt(GetPositionFromPoint(e.GetPosition(this), snapToText: false)))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);

        // The Status Bar counts follow the visible document, whether the change was a user edit or a
        // fresh projection (which returns below before Capturing).
        UpdateStatistics();

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

        // The edited text may have gained or lost Matches; keep the highlights current. A Replace All
        // batch is the exception — it is iterating the Match ranges, and Recomputes once at the end.
        if (IsFindActive && !_isReplacing)
        {
            RecomputeMatches();
        }
    }

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
