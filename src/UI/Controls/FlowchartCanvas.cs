using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UI.ViewModels;

namespace UI.Controls;

/// <summary>
/// The Flowchart Builder's drag-and-drop surface: Diagram Nodes as draggable boxes joined by drawn
/// Diagram Edges. Left-drag a node to move it, drag from a node's connector handle to another node to
/// connect them, click to select, and double-click to rename. It drives a
/// <see cref="FlowchartBuilderViewModel"/> — every gesture becomes a call on the builder — and holds no
/// diagram state of its own; node positions it sets are builder view state, never emitted to Mermaid
/// (INV-051).
/// </summary>
/// <remarks>
/// Authored as a custom Control (interaction logic in the class, look in
/// <c>Controls/FlowchartCanvas.xaml</c>), per the project's Control exception to the zero-code-behind
/// rule — the same pattern as <see cref="MermaidPreview"/>. Its look is an implicit style merged from
/// the app resources; the class translates mouse gestures into <see cref="FlowchartBuilderViewModel"/>
/// calls.
/// </remarks>
public sealed class FlowchartCanvas : Control
{
    /// <summary>Identifies the <see cref="Builder"/> dependency property — the builder this canvas edits.</summary>
    public static readonly DependencyProperty BuilderProperty = DependencyProperty.Register(
        nameof(Builder),
        typeof(FlowchartBuilderViewModel),
        typeof(FlowchartCanvas),
        new PropertyMetadata(defaultValue: null));

    private Line? _rubberBand;
    private FlowchartNodeViewModel? _dragNode;
    private FlowchartNodeViewModel? _connectFrom;
    private Point _dragMouseStart;
    private double _dragNodeStartX;
    private double _dragNodeStartY;

    /// <summary>The <see cref="FlowchartBuilderViewModel"/> whose Diagram Nodes and Edges this canvas presents and edits.</summary>
    public FlowchartBuilderViewModel? Builder
    {
        get => (FlowchartBuilderViewModel?)GetValue(BuilderProperty);
        set => SetValue(BuilderProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _rubberBand = GetTemplateChild("PART_RubberBand") as Line;
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Builder is null)
        {
            return;
        }

        EndEditing();
        var origin = e.OriginalSource as DependencyObject;
        var node = FindDataContext<FlowchartNodeViewModel>(origin);
        var edge = FindDataContext<FlowchartEdgeViewModel>(origin);

        if (node is not null && e.ClickCount == 2)
        {
            Builder.SelectNode(node);
            node.IsEditing = true; // double-click begins an inline rename
            e.Handled = true;
        }
        else if (node is not null && IsConnector(origin))
        {
            Builder.SelectNode(node);
            BeginConnect(node);
            e.Handled = true;
        }
        else if (node is not null)
        {
            Builder.SelectNode(node);
            BeginMove(node, e.GetPosition(this));
            e.Handled = true;
        }
        else if (edge is not null)
        {
            Builder.SelectEdge(edge);
            e.Handled = true;
        }
        else
        {
            Builder.ClearSelection();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Enter or Escape commits an in-progress inline rename (its Label has already bound through).
        if (e.Key is (Key.Enter or Key.Escape) && Builder is not null && Builder.Nodes.Any(node => node.IsEditing))
        {
            EndEditing();
            Focus();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Builder is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (_dragNode is not null)
        {
            Builder.MoveNode(
                _dragNode,
                Math.Max(0, _dragNodeStartX + (position.X - _dragMouseStart.X)),
                Math.Max(0, _dragNodeStartY + (position.Y - _dragMouseStart.Y)));
        }
        else if (_connectFrom is not null && _rubberBand is not null)
        {
            _rubberBand.X2 = position.X;
            _rubberBand.Y2 = position.Y;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (Builder is not null && _connectFrom is not null)
        {
            // The mouse is captured to this canvas, so e.OriginalSource is the canvas, not the node
            // under the cursor — hit-test at the drop point to find the target node instead.
            var dropped = InputHitTest(e.GetPosition(this)) as DependencyObject;
            var target = FindDataContext<FlowchartNodeViewModel>(dropped);
            if (target is not null && !ReferenceEquals(target, _connectFrom))
            {
                Builder.Connect(_connectFrom, target);
            }
        }

        _dragNode = null;
        _connectFrom = null;
        if (_rubberBand is not null)
        {
            _rubberBand.Visibility = Visibility.Collapsed;
        }

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void BeginMove(FlowchartNodeViewModel node, Point mouse)
    {
        _dragNode = node;
        _dragMouseStart = mouse;
        _dragNodeStartX = node.X;
        _dragNodeStartY = node.Y;
        CaptureMouse();
    }

    private void BeginConnect(FlowchartNodeViewModel from)
    {
        _connectFrom = from;
        if (_rubberBand is not null)
        {
            _rubberBand.X1 = from.CenterX;
            _rubberBand.Y1 = from.CenterY;
            _rubberBand.X2 = from.CenterX;
            _rubberBand.Y2 = from.CenterY;
            _rubberBand.Visibility = Visibility.Visible;
        }

        CaptureMouse();
    }

    private void EndEditing()
    {
        if (Builder is null)
        {
            return;
        }

        foreach (var node in Builder.Nodes)
        {
            node.IsEditing = false;
        }
    }

    // Whether the gesture began on a node's connector handle (tagged "Connector"), which starts an edge
    // rather than a move.
    private static bool IsConnector(DependencyObject? origin)
    {
        for (var current = origin; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Tag: "Connector" })
            {
                return true;
            }
        }

        return false;
    }

    // The nearest ancestor's data context of the given type, walking up from the hit element — the
    // node or edge presenter a gesture landed on, or null for empty canvas.
    private static T? FindDataContext<T>(DependencyObject? origin)
        where T : class
    {
        for (var current = origin; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: T match })
            {
                return match;
            }
        }

        return null;
    }
}
