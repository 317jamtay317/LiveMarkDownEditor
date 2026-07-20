using System.ComponentModel;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// One Diagram Edge on the Flowchart Builder's canvas: the two nodes it joins, an editable Edge Label
/// and <see cref="EdgeKind"/>, and the line geometry between the two node centres. The geometry
/// follows the nodes — moving either endpoint re-raises the endpoints so the drawn line tracks it —
/// and is view state only, never emitted (INV-051).
/// </summary>
public sealed class FlowchartEdgeViewModel : ObservableObject
{
    private string _label;
    private EdgeKind _kind;
    private bool _isSelected;

    /// <summary>Creates a canvas edge presenter joining two node presenters.</summary>
    /// <param name="from">The node the edge runs from.</param>
    /// <param name="to">The node the edge runs to.</param>
    /// <param name="label">The optional Edge Label, or null/blank for none.</param>
    /// <param name="kind">How the edge is drawn.</param>
    public FlowchartEdgeViewModel(FlowchartNodeViewModel from, FlowchartNodeViewModel to, string? label, EdgeKind kind)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        _label = label ?? string.Empty;
        _kind = kind;

        From.PropertyChanged += EndpointMoved;
        To.PropertyChanged += EndpointMoved;
    }

    /// <summary>The node the edge runs from.</summary>
    public FlowchartNodeViewModel From { get; }

    /// <summary>The node the edge runs to.</summary>
    public FlowchartNodeViewModel To { get; }

    /// <summary>The optional Edge Label shown on the line. Empty when the edge carries no text.</summary>
    public string Label
    {
        get => _label;
        set => Set(ref _label, value ?? string.Empty);
    }

    /// <summary>How the edge is drawn.</summary>
    public EdgeKind Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    /// <summary>Whether this edge is the current selection (highlighted, and the Delete target).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>The x of the line's start (the From node's centre).</summary>
    public double X1 => From.CenterX;

    /// <summary>The y of the line's start (the From node's centre).</summary>
    public double Y1 => From.CenterY;

    /// <summary>The x of the line's end (the To node's centre).</summary>
    public double X2 => To.CenterX;

    /// <summary>The y of the line's end (the To node's centre).</summary>
    public double Y2 => To.CenterY;

    /// <summary>The Diagram Edge this presenter stands for.</summary>
    /// <returns>The domain <see cref="DiagramEdge"/> (a blank label normalises to none).</returns>
    public DiagramEdge ToDomain() => new(From.Id, To.Id, Label, Kind);

    /// <summary>Stops tracking the endpoints' movement — called when the edge is removed.</summary>
    public void Detach()
    {
        From.PropertyChanged -= EndpointMoved;
        To.PropertyChanged -= EndpointMoved;
    }

    private void EndpointMoved(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FlowchartNodeViewModel.CenterX) or nameof(FlowchartNodeViewModel.CenterY)))
        {
            return;
        }

        Raise(nameof(X1));
        Raise(nameof(Y1));
        Raise(nameof(X2));
        Raise(nameof(Y2));
    }
}
