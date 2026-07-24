using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Domain;
using UI.Wysiwyg;

namespace UI.Controls;

/// <summary>
/// Draws the Change Highlight over a <see cref="RichTextBox"/>'s Visual Document after a live
/// reload: a shade behind every Block the External Change added or altered, and a thin tick at the
/// seam of every run it deleted. It holds briefly and then fades away of its own accord.
/// </summary>
/// <remarks>
/// It is a read-only overlay — it paints from the Changed Regions the editor hands it and nothing
/// else — so seeing what changed never changes the Markdown Document, and it neither moves the caret
/// nor scrolls: the reload is another writer's action, and taking the reader's place away mid-read
/// is not something they asked for (INV-060). Scrolling and resizing only repaint from the targets
/// already resolved; they never re-resolve them.
/// </remarks>
public sealed class ChangeHighlightAdorner : Adorner
{
    // Long enough to notice without becoming furniture, then a fade slow enough to read as one.
    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(900));
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(2200);

    private const double BlockPadding = 4d;
    private const double RightInset = 14d;
    private const double CornerRadius = 3d;
    private const double EdgeBarWidth = 3d;
    private const double SeamTickWidth = 72d;
    private const double SeamTickHeight = 2d;

    private readonly RichTextBox _editor;
    private IReadOnlyList<ChangeHighlightTarget> _targets = [];

    /// <summary>Creates the adorner over <paramref name="editor"/>, repainting as it scrolls or resizes.</summary>
    /// <param name="editor">The editor whose reloaded Blocks are highlighted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="editor"/> is <see langword="null"/>.</exception>
    public ChangeHighlightAdorner(RichTextBox editor)
        : base(editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        IsHitTestVisible = false;
        Opacity = 0;

        _editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnRepaintNeeded));
        _editor.SizeChanged += OnRepaintNeeded;
    }

    /// <summary>
    /// Shows the Change Highlight for <paramref name="regions"/>, restarting the hold-then-fade. An
    /// empty set clears the highlight outright, which is how an edit or a fresh document takes a
    /// highlight down before it would have faded.
    /// </summary>
    /// <param name="regions">The Changed Regions of the reload, numbered within the document now shown.</param>
    public void Show(IReadOnlyList<ChangedRegion>? regions)
    {
        _targets = regions is { Count: > 0 }
            ? ChangeHighlightScanner.Scan(_editor.Document, regions)
            : [];

        BeginAnimation(OpacityProperty, null);
        if (_targets.Count == 0)
        {
            Opacity = 0;
            InvalidateVisual();
            return;
        }

        Opacity = 1;

        // HoldEnd, deliberately: FillBehavior.Stop would hand the property back to its base value —
        // the 1 set just above — the instant the fade finished, snapping the highlight back to full
        // strength and leaving it there. Once faded it also drops its targets, so a highlight that
        // has gone holds nothing and paints nothing.
        var fade = new DoubleAnimation(1, 0, FadeDuration) { BeginTime = HoldDuration };
        fade.Completed += (_, _) =>
        {
            _targets = [];
            InvalidateVisual();
        };

        BeginAnimation(OpacityProperty, fade);

        // The reloaded document has been projected but not yet laid out, so every character rectangle
        // would still be empty. Repaint once layout has run — Loaded priority is below Render, so it
        // is dispatched after it.
        InvalidateVisual();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, InvalidateVisual);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_targets.Count == 0)
        {
            return;
        }

        var fill = _editor.TryFindResource("ChangeHighlightBrush") as Brush;
        var marker = _editor.TryFindResource("ChangeHighlightMarkerBrush") as Brush;
        if (fill is null || marker is null)
        {
            return;
        }

        var viewportWidth = _editor.ActualWidth;
        var viewportHeight = _editor.ActualHeight;
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, viewportWidth, viewportHeight)));

        foreach (var target in _targets)
        {
            Draw(drawingContext, target, fill, marker, viewportWidth, viewportHeight);
        }

        drawingContext.Pop();
    }

    private void Draw(
        DrawingContext drawingContext,
        ChangeHighlightTarget target,
        Brush fill,
        Brush marker,
        double viewportWidth,
        double viewportHeight)
    {
        try
        {
            var startRect = target.Block.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            var endRect = target.Block.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
            if (startRect == Rect.Empty || endRect == Rect.Empty)
            {
                return;
            }

            if (endRect.Bottom < 0 || startRect.Top > viewportHeight)
            {
                return;
            }

            var left = startRect.Left - BlockPadding;
            var right = viewportWidth - RightInset;
            if (right <= left)
            {
                return;
            }

            switch (target.Kind)
            {
                case ChangeHighlightTargetKind.Changed:
                    DrawChanged(drawingContext, fill, marker, left, right, startRect.Top, endRect.Bottom);
                    break;

                case ChangeHighlightTargetKind.RemovedAbove:
                    DrawSeam(drawingContext, marker, left, startRect.Top - BlockPadding);
                    break;

                case ChangeHighlightTargetKind.RemovedBelow:
                    DrawSeam(drawingContext, marker, left, endRect.Bottom + BlockPadding);
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            // A pointer into a document that has just been replaced — ignore. A highlight only ever
            // describes the document it was resolved against, and the next reload resolves afresh.
        }
    }

    // A changed Block: a soft panel over its whole extent, with a stronger bar down its leading edge
    // so the change still reads at a glance on a busy page.
    private static void DrawChanged(
        DrawingContext drawingContext,
        Brush fill,
        Brush marker,
        double left,
        double right,
        double top,
        double bottom)
    {
        var box = new Rect(left, top - BlockPadding, right - left, bottom - top + (2 * BlockPadding));
        drawingContext.DrawRoundedRectangle(fill, null, box, CornerRadius, CornerRadius);
        drawingContext.DrawRoundedRectangle(
            marker,
            null,
            new Rect(left, box.Top, EdgeBarWidth, box.Height),
            EdgeBarWidth / 2,
            EdgeBarWidth / 2);
    }

    // A deletion seam: a short tick between the Blocks that closed over the deleted content. There is
    // nothing left to shade, so the mark says "something was here" without inventing content.
    private static void DrawSeam(DrawingContext drawingContext, Brush marker, double left, double y)
    {
        drawingContext.DrawRoundedRectangle(
            marker,
            null,
            new Rect(left, y - (SeamTickHeight / 2), SeamTickWidth, SeamTickHeight),
            SeamTickHeight / 2,
            SeamTickHeight / 2);
    }

    private void OnRepaintNeeded(object? sender, EventArgs e) => InvalidateVisual();
}
