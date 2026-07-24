using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
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
/// <para>
/// The control's members are large enough that they are organised into cohesive
/// <see langword="partial"/> files by responsibility — folding, the outline, formatting actions,
/// diagrams, find/replace, the clipboard, links and printing, and the editing annotations — each of
/// which sits alongside this core file, which owns construction and the Markdown⇄Document
/// synchronisation.
/// </para>
/// </remarks>
public sealed partial class MarkdownRichEditor : RichTextBox
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

    private readonly MarkdownToFlowDocumentProjector _projector = new();
    private readonly FlowDocumentToMarkdownCapturer _capturer = new();

    private bool _isSynchronising;
    private string _lastCaptured = string.Empty;

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

        // Smart Paste: a URL over a selection becomes a Link, a clipboard image is written beside the
        // Watched File and inserted as an Image, and HTML converts to Markdown (INV-041).
        DataObject.AddPastingHandler(this, OnPasting);

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
            MarkdownEditingCommands.RemoveTableRow,
            (_, _) => RemoveTableRowAtCaret(),
            (_, e) => e.CanExecute = TableEditing.CanRemoveRow(this)));
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.RemoveTableColumn,
            (_, _) => RemoveTableColumnAtCaret(),
            (_, e) => e.CanExecute = TableEditing.CanRemoveColumn(this)));
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
        CommandBindings.Add(new CommandBinding(
            MarkdownEditingCommands.OpenFlowchartBuilder, (_, _) => OpenFlowchartBuilderAtCaret()));

        RegisterHeadingLevelGestures();

        // Moving the caret can change which Section the user is editing within; the Navigation Panel
        // listens to keep the Current Section's Outline Entry highlighted.
        SelectionChanged += (_, _) =>
        {
            UpdateCaretStatus();
            UpdateDiagramSource();
            CurrentSectionChanged?.Invoke(this, EventArgs.Empty);
        };

        // Right-clicking a Misspelling offers its Spelling Suggestions; the menu is refilled on demand
        // so it reflects the current Misspellings and the word actually under the pointer. The menu
        // instance itself is created here and never replaced — owning one from construction is what
        // keeps WPF's own text-editor context menu from pre-empting this handler (INV-057).
        ContextMenu = new ContextMenu();
        ContextMenuOpening += OnContextMenuOpening;

        // The Change Highlight, the Code Shading, the custom camelCase-aware spell checker, and the
        // Find highlights all draw through adorners, which need the editor's AdornerLayer — available
        // only once it is in the visual tree. They are attached bottom-up: the Change Highlight's
        // shade beneath the Code Shading's panels, and both beneath the squiggles and Find highlights.
        Loaded += (_, _) =>
        {
            AttachChangeHighlight();
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

    /// <summary>
    /// When set, an alternate way to bring a Visual Document rectangle into view — scrolling the outer
    /// canvas rather than this control's own (Page-View-disabled) scroll. Left <see langword="null"/>,
    /// the control scrolls itself. Set by <see cref="PageView"/> so the page-view concern stays out of
    /// this control (INV-058).
    /// </summary>
    internal Action<Rect>? RevealRectOverride { get; set; }

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

        // Render each Mermaid Diagram's picture into its inline view (async — INV-047). The picture
        // arrives after this projection and changes no structure (INV-003); it is cached by source and
        // theme, so an unchanged diagram is not re-rendered as the user types elsewhere.
        _diagramRenderer.RenderAll(Document, DiagramImageRenderer, IsDarkTheme);

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

        // An edit inside a Mermaid Diagram changes its source; refresh the Diagram Preview (INV-047).
        UpdateDiagramSource();

        // The edited text may have gained or lost Matches; keep the highlights current. A Replace All
        // batch is the exception — it is iterating the Match ranges, and Recomputes once at the end.
        if (IsFindActive && !_isReplacing)
        {
            RecomputeMatches();
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

    private static string HeadingText(Block block) =>
        new TextRange(block.ContentStart, block.ContentEnd).Text.Trim();

    private static int? LevelOf(Block block) =>
        block is Paragraph { Tag: HeadingRole role } ? role.Level : null;
}
