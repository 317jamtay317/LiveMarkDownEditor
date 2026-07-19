using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// One Diagram Node on the Flowchart Builder's canvas: its <see cref="NodeId"/>, editable Node Label
/// and <see cref="NodeShape"/>, and its on-canvas position (<see cref="X"/>/<see cref="Y"/>). The
/// position is builder view state only — it is never emitted to Mermaid, which computes layout itself
/// (INV-051), so moving a node changes no Diagram Graph.
/// </summary>
public sealed class FlowchartNodeViewModel : ObservableObject
{
    /// <summary>The node box's width on the canvas, in device-independent pixels.</summary>
    public const double Width = 132;

    /// <summary>The node box's height on the canvas, in device-independent pixels.</summary>
    public const double Height = 56;

    private string _label;
    private NodeShape _shape;
    private double _x;
    private double _y;
    private bool _isSelected;
    private bool _isEditing;

    /// <summary>Creates a canvas node presenter.</summary>
    /// <param name="id">The node's stable Node Id.</param>
    /// <param name="label">The Node Label shown in the box.</param>
    /// <param name="shape">The node's shape.</param>
    /// <param name="x">The left edge of the node box on the canvas.</param>
    /// <param name="y">The top edge of the node box on the canvas.</param>
    public FlowchartNodeViewModel(NodeId id, string label, NodeShape shape, double x, double y)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        _label = label ?? throw new ArgumentNullException(nameof(label));
        _shape = shape;
        _x = x;
        _y = y;
    }

    /// <summary>The node's stable Node Id — fixed for the life of the node.</summary>
    public NodeId Id { get; }

    /// <summary>The Node Label shown in the box.</summary>
    public string Label
    {
        get => _label;
        set => Set(ref _label, value ?? string.Empty);
    }

    /// <summary>The node's shape.</summary>
    public NodeShape Shape
    {
        get => _shape;
        set => Set(ref _shape, value);
    }

    /// <summary>The left edge of the node box on the canvas. View state only — never emitted (INV-051).</summary>
    public double X
    {
        get => _x;
        set
        {
            if (Set(ref _x, value))
            {
                Raise(nameof(CenterX));
            }
        }
    }

    /// <summary>The top edge of the node box on the canvas. View state only — never emitted (INV-051).</summary>
    public double Y
    {
        get => _y;
        set
        {
            if (Set(ref _y, value))
            {
                Raise(nameof(CenterY));
            }
        }
    }

    /// <summary>The horizontal centre of the node box — where an incident edge meets it.</summary>
    public double CenterX => _x + (Width / 2);

    /// <summary>The vertical centre of the node box — where an incident edge meets it.</summary>
    public double CenterY => _y + (Height / 2);

    /// <summary>Whether this node is the current selection (highlighted, and the Delete target).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>Whether the node's label is being edited in place (a double-click begins it).</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    /// <summary>The Diagram Node this presenter stands for.</summary>
    /// <returns>The domain <see cref="DiagramNode"/>.</returns>
    public DiagramNode ToDomain() => new(Id, Label, Shape);
}
