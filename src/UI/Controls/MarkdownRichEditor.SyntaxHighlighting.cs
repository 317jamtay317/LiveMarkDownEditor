using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using Domain;
using UI.Wysiwyg;

namespace UI.Controls;

/// <summary>
/// The editor's Syntax Highlighting: each Code Block's code is colored by what it means in its
/// Highlighting Language, and the Code Block the user is typing in is re-colored once the typing
/// settles (INV-064).
/// </summary>
/// <remarks>
/// None of this is an edit. A whole-document pass runs inside the projection's synchronisation
/// guard, and a re-highlight runs inside <c>MutateVisualDocument</c>, so neither Captures — the
/// Markdown Document is untouched. The re-highlight is also skipped outright when the coloring
/// would not change, which is what keeps it off the undo stack and stops the caret churning while
/// the user reads.
/// </remarks>
public sealed partial class MarkdownRichEditor
{
    /// <summary>
    /// Identifies the <see cref="SyntaxHighlighter"/> dependency property. Bound to the tokenizer
    /// the Workspace resolves, it is what colors each Code Block's code (INV-064). Left unbound,
    /// Code Blocks show their code plain.
    /// </summary>
    public static readonly DependencyProperty SyntaxHighlighterProperty = DependencyProperty.Register(
        nameof(SyntaxHighlighter),
        typeof(ISyntaxHighlighter),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null, OnSyntaxHighlighterChanged));

    // How long the typing must settle before the Code Block under the caret is re-colored.
    // Long enough that a burst of typing re-tokenizes once rather than per keystroke; short enough
    // that the color arrives while the user is still looking at the word they typed.
    private static readonly TimeSpan ReHighlightDelay = TimeSpan.FromMilliseconds(200);

    private DispatcherTimer? _reHighlightTimer;
    private Paragraph? _pendingReHighlight;

    /// <summary>
    /// The tokenizer each Code Block's Syntax Highlighting is colored by.
    /// <see langword="null"/> — the default, and the state before the port is bound — shows every
    /// Code Block's code plain rather than colored. View-only: it never changes the Markdown
    /// Document (INV-064).
    /// </summary>
    public ISyntaxHighlighter? SyntaxHighlighter
    {
        get => (ISyntaxHighlighter?)GetValue(SyntaxHighlighterProperty);
        set => SetValue(SyntaxHighlighterProperty, value);
    }

    /// <summary>
    /// Runs any pending re-highlight immediately rather than waiting for the typing to settle.
    /// Exists so a test can observe the settled coloring without sleeping on a timer.
    /// </summary>
    internal void FlushPendingSyntaxHighlight()
    {
        _reHighlightTimer?.Stop();

        var block = _pendingReHighlight;
        _pendingReHighlight = null;
        if (block is not null && SyntaxHighlighter is not null)
        {
            ReHighlight(block);
        }
    }

    // Colors every Code Block of the document now showing. Called from the projection (inside its
    // synchronisation guard) and when the port binds after a projection has already happened.
    private void HighlightAllCodeBlocks() => SyntaxHighlighting.ApplyAll(Document, SyntaxHighlighter);

    // The port usually binds after the first projection; color what is already on screen.
    private static void OnSyntaxHighlighterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownRichEditor)d;
        editor.MutateVisualDocument(editor.HighlightAllCodeBlocks);
    }

    // Queues the Code Block under the caret for re-coloring once the typing settles. An edit
    // outside a Code Block cancels any pending pass: the block that was queued may no longer be the
    // one being edited, and coloring prose is not this pass's business.
    private void ScheduleReHighlightAtCaret(UndoAction undoAction)
    {
        // Never re-highlight in response to an undo or a redo. Undoing a re-highlight is itself a
        // text change, so re-highlighting on it would push a fresh undo entry and undo the one just
        // taken back — an unbreakable loop that leaves the user unable to reach their own typing.
        // Stepping aside also means an undo restores the coloring the document had at that point,
        // which is what the user is asking for when they step backwards.
        if (SyntaxHighlighter is null || undoAction is UndoAction.Undo or UndoAction.Redo)
        {
            return;
        }

        if (CaretPosition?.Paragraph is not { Tag: CodeBlockRole } codeBlock)
        {
            _pendingReHighlight = null;
            _reHighlightTimer?.Stop();
            return;
        }

        _pendingReHighlight = codeBlock;

        _reHighlightTimer ??= CreateReHighlightTimer();
        _reHighlightTimer.Stop();
        _reHighlightTimer.Start();
    }

    private DispatcherTimer CreateReHighlightTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = ReHighlightDelay,
        };

        timer.Tick += (_, _) => FlushPendingSyntaxHighlight();
        return timer;
    }

    // Re-colors one Code Block, keeping the caret where the user left it. The rebuild replaces the
    // block's Runs, so the caret — which points into one of the outgoing Runs — is remembered as a
    // character offset within the block and re-found afterwards.
    private void ReHighlight(Paragraph codeBlock)
    {
        var caretOffset = CaretPosition?.Paragraph == codeBlock
            ? CharacterOffsetWithin(codeBlock, CaretPosition)
            : (int?)null;

        // The rebuild empties the paragraph and refills it. Those are two separate edits, and left
        // ungrouped the editor records them as two undo units — so a single Ctrl+Z replays only the
        // refill's inverse and leaves the Code Block empty, destroying the user's code. BeginChange
        // makes the pair atomic: one undo unit, whose inverse restores exactly what was there.
        //
        // Switching recording off instead (IsUndoEnabled) is not an option: WPF empties the undo
        // stack when it is set false, which throws away the history the user still needs.
        BeginChange();
        try
        {
            MutateVisualDocument(() => SyntaxHighlighting.Apply(codeBlock, SyntaxHighlighter));
        }
        finally
        {
            EndChange();
        }

        if (caretOffset is { } offset && PositionAt(codeBlock, offset) is { } restored)
        {
            CaretPosition = restored;
        }
    }

    // Characters from the block's start to `position`, counting a line end as one character — the
    // same measure the rebuild's pieces are laid out in, so an offset taken before a rebuild names
    // the same place afterwards.
    private static int CharacterOffsetWithin(Paragraph codeBlock, TextPointer position)
    {
        var offset = 0;
        foreach (var inline in codeBlock.Inlines)
        {
            switch (inline)
            {
                case Run run:
                    if (position.CompareTo(run.ContentStart) <= 0)
                    {
                        return offset;
                    }

                    // A Run holds nothing but text, so within one the symbol offset is the
                    // character offset.
                    if (position.CompareTo(run.ContentEnd) < 0)
                    {
                        return offset + run.ContentStart.GetOffsetToPosition(position);
                    }

                    offset += run.Text.Length;
                    break;

                case LineBreak lineBreak:
                    if (position.CompareTo(lineBreak.ContentStart) <= 0)
                    {
                        return offset;
                    }

                    offset++;
                    break;
            }
        }

        return offset;
    }

    // The reverse of CharacterOffsetWithin, over the block's new inlines.
    private static TextPointer? PositionAt(Paragraph codeBlock, int characterOffset)
    {
        var remaining = characterOffset;
        foreach (var inline in codeBlock.Inlines)
        {
            switch (inline)
            {
                case Run run:
                    if (remaining <= run.Text.Length)
                    {
                        return run.ContentStart.GetPositionAtOffset(remaining);
                    }

                    remaining -= run.Text.Length;
                    break;

                case LineBreak:
                    if (remaining == 0)
                    {
                        return inline.ContentStart;
                    }

                    remaining--;
                    break;
            }
        }

        return codeBlock.ContentEnd;
    }
}
