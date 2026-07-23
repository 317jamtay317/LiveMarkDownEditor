using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using UI.Find;

namespace UI.Controls;

// Find and Replace over the Visual Document: the query state the Find Bar binds to, the Match ranges
// the highlight adorner paints, the Current Match, and the Replace / Replace All edits. Find itself is
// presentation-only and never feeds back into Capture (INV-016); only a Replacement is a real edit
// that Captures back into the Markdown source (INV-022).
public sealed partial class MarkdownRichEditor
{
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

    // Find state: the current Matches as document ranges, and which one is the Current Match. All of
    // it is presentation-only — none of it feeds back into Capture (INV-016).
    private FindHighlightAdorner? _findAdorner;
    private readonly List<TextRange> _matchRanges = [];
    private int _currentMatch = -1;

    // Set while a Replace All batch is running, so the per-edit Recompute is suppressed: it would
    // clear the Match ranges the batch is iterating. The batch Recomputes once when it finishes.
    private bool _isReplacing;

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

        if (RevealRectOverride is { } reveal)
        {
            reveal(rect);
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
}
