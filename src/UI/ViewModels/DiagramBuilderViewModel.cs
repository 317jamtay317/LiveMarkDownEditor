using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Diagram Builder's state and behaviour: the Diagram Kind it is authoring, the Diagram Nodes and
/// Diagram Edges on the canvas, the Flow Direction, and the commands that add, connect, reshape, and
/// delete them. It edits a Diagram Graph and exposes its canonical Mermaid source
/// (<see cref="MermaidSource"/>), but touches no Markdown Document — it only yields a
/// <see cref="Result"/> when the user Inserts, and <see langword="null"/> when they Cancel (INV-053).
/// The heart of the builder lives here, kept free of WPF so it is unit-testable (the
/// <see cref="LinkPromptViewModel"/> pattern).
/// </summary>
public sealed partial class DiagramBuilderViewModel : ObservableObject
{
    private const double Margin = 40;
    private const double Gap = 44;

    private readonly RelayCommand _deleteSelectedCommand;
    private DiagramKind _kind;
    private FlowDirection _direction;
    private bool? _dialogResult;
    private int _placed;

    /// <summary>Creates the Diagram Builder, seeded from an existing Mermaid Diagram or empty.</summary>
    /// <param name="existingSource">The Mermaid source to edit graphically, or null to start empty (INV-053).</param>
    /// <param name="isDark">Whether the app is in dark theme, so the live Diagram Preview matches it.</param>
    public DiagramBuilderViewModel(string? existingSource, bool isDark = false)
    {
        IsDark = isDark;
        InsertCommand = new RelayCommand(() => DialogResult = true);
        CancelCommand = new RelayCommand(() => DialogResult = false);
        AddNodeCommand = new RelayCommand(AddNode);
        _deleteSelectedCommand = new RelayCommand(DeleteSelected, () => HasSelection);

        DiagramGraph.TryParse(existingSource, out var graph);
        _kind = graph.Kind;
        _direction = graph.Direction;
        Seed(graph);
    }

    /// <summary>Whether the app is in dark theme, so the live Diagram Preview follows the editor palette.</summary>
    public bool IsDark { get; }

    /// <summary>The Diagram Nodes on the canvas, in order.</summary>
    public ObservableCollection<DiagramNodeViewModel> Nodes { get; } = [];

    /// <summary>The Diagram Edges on the canvas, in order.</summary>
    public ObservableCollection<DiagramEdgeViewModel> Edges { get; } = [];

    /// <summary>The Diagram Kinds the kind picker offers — every node/arrow diagram the builder authors.</summary>
    public IReadOnlyList<DiagramKind> Kinds { get; } = Enum.GetValues<DiagramKind>();

    /// <summary>The Flow Directions the direction picker offers.</summary>
    public IReadOnlyList<FlowDirection> Directions { get; } = Enum.GetValues<FlowDirection>();

    /// <summary>The Node Shapes the shape picker offers — the current Diagram Kind's Shape Set (INV-070).</summary>
    public IReadOnlyList<NodeShape> NodeShapes => Kind.ShapeSet();

    /// <summary>The Edge Kinds the edge kind picker offers — the current Diagram Kind's Edge Set (INV-070).</summary>
    public IReadOnlyList<EdgeKind> EdgeKinds => Kind.EdgeSet();

    /// <summary>
    /// Which node/arrow diagram is being authored. Changing it keeps every Diagram Node and Diagram
    /// Edge, coercing any Node Shape or Edge Kind the new kind does not allow to that kind's default,
    /// and re-emits the source in the new kind's syntax (INV-070). It writes no Markdown Document
    /// (INV-053).
    /// </summary>
    public DiagramKind Kind
    {
        get => _kind;
        set
        {
            if (!Set(ref _kind, value))
            {
                return;
            }

            CoerceToKind();
            Raise(nameof(NodeShapes));
            Raise(nameof(EdgeKinds));
            Raise(nameof(HasFlowDirection));
            Raise(nameof(SelectedNodeShape));
            Raise(nameof(SelectedEdgeKind));
            RaiseSource();
        }
    }

    /// <summary>
    /// Whether the current Diagram Kind's Mermaid carries a Flow Direction at all — enables the
    /// direction picker. An Entity Relationship Diagram's does not; Mermaid lays it out itself
    /// (INV-051). The chosen <see cref="Direction"/> is kept regardless, so it returns when the kind
    /// changes back.
    /// </summary>
    public bool HasFlowDirection => Kind.CarriesFlowDirection();

    /// <summary>The direction the diagram flows. Changing it re-emits the source.</summary>
    public FlowDirection Direction
    {
        get => _direction;
        set
        {
            if (Set(ref _direction, value))
            {
                RaiseSource();
            }
        }
    }

    /// <summary>
    /// The canonical Mermaid source of the current Diagram Graph — bound to the live Diagram Preview
    /// and returned on Insert. Recomputed on every structural or attribute change (INV-051).
    /// </summary>
    public string MermaidSource => BuildGraph().ToMermaidSource();

    /// <summary>
    /// The Mermaid source the user chose to write, or <see langword="null"/> when the builder was
    /// cancelled or is still open. It is non-null only once Insert has been chosen (INV-053).
    /// </summary>
    public string? Result => DialogResult is true ? MermaidSource : null;

    /// <summary>
    /// The builder's outcome: <see langword="true"/> once Inserted, <see langword="false"/> once
    /// Cancelled, and <see langword="null"/> while still open. The window closes itself when this is set.
    /// </summary>
    public bool? DialogResult
    {
        get => _dialogResult;
        private set => Set(ref _dialogResult, value);
    }

    /// <summary>Writes the current Diagram Graph back as the Mermaid Diagram's source (INV-053).</summary>
    public ICommand InsertCommand { get; }

    /// <summary>Dismisses the builder, making no edit (INV-053).</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Adds a new Diagram Node to the canvas and selects it.</summary>
    public ICommand AddNodeCommand { get; }

    /// <summary>Removes the selected Diagram Node (with its incident edges) or the selected Diagram Edge.</summary>
    public ICommand DeleteSelectedCommand => _deleteSelectedCommand;

    /// <summary>Moves a node to a new canvas position. View-only — it never changes the source (INV-051).</summary>
    /// <param name="node">The node to move.</param>
    /// <param name="x">The node box's new left edge.</param>
    /// <param name="y">The node box's new top edge.</param>
    public void MoveNode(DiagramNodeViewModel node, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.X = x;
        node.Y = y;
    }

    /// <summary>
    /// Connects two nodes with a new Diagram Edge of the current Diagram Kind's default Edge Kind, and
    /// re-emits the source (INV-070).
    /// </summary>
    /// <param name="from">The node the edge runs from.</param>
    /// <param name="to">The node the edge runs to.</param>
    /// <returns>The new edge presenter.</returns>
    public DiagramEdgeViewModel Connect(DiagramNodeViewModel from, DiagramNodeViewModel to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var edge = new DiagramEdgeViewModel(from, to, label: null, Kind.DefaultEdgeKind());
        edge.PropertyChanged += OnEdgeChanged;
        Edges.Add(edge);
        RaiseSource();
        return edge;
    }

    private void AddNode()
    {
        var (x, y) = NextSpot();
        var node = new DiagramNodeViewModel(NextId(), NewNodeLabel(), Kind.DefaultShape(), x, y);
        node.PropertyChanged += OnNodeChanged;
        Nodes.Add(node);
        SelectNode(node);
        RaiseSource();
    }

    // What the current Diagram Kind calls a node, so a new one reads as what it is on the canvas.
    private string NewNodeLabel() => Kind switch
    {
        DiagramKind.StateDiagram => "State",
        DiagramKind.ClassDiagram => "Class",
        DiagramKind.EntityRelationshipDiagram => "Entity",
        _ => "Node",
    };

    // Fits every node and edge to the new Diagram Kind's Shape Set and Edge Set, so the graph stays
    // valid across the change (INV-052/INV-070). Each setter raises, which re-emits the source.
    private void CoerceToKind()
    {
        foreach (var node in Nodes)
        {
            node.Shape = Kind.Coerce(node.Shape);
        }

        foreach (var edge in Edges)
        {
            edge.Kind = Kind.Coerce(edge.Kind);
        }
    }

    private void DeleteSelected()
    {
        if (SelectedNode is { } node)
        {
            foreach (var edge in Edges.Where(e => e.From == node || e.To == node).ToList())
            {
                RemoveEdge(edge);
            }

            node.PropertyChanged -= OnNodeChanged;
            Nodes.Remove(node);
            SelectedNode = null;
            RaiseSource();
        }
        else if (SelectedEdge is { } selected)
        {
            RemoveEdge(selected);
            SelectedEdge = null;
            RaiseSource();
        }
    }

    private void RemoveEdge(DiagramEdgeViewModel edge)
    {
        edge.Detach();
        edge.PropertyChanged -= OnEdgeChanged;
        Edges.Remove(edge);
    }

    private void Seed(DiagramGraph graph)
    {
        var byId = new Dictionary<NodeId, DiagramNodeViewModel>();
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            var (x, y) = LayOut(i, graph.Nodes.Count);
            var presenter = new DiagramNodeViewModel(node.Id, node.Label, node.Shape, x, y);
            presenter.PropertyChanged += OnNodeChanged;
            Nodes.Add(presenter);
            byId[node.Id] = presenter;
        }

        foreach (var edge in graph.Edges)
        {
            var presenter = new DiagramEdgeViewModel(byId[edge.FromId], byId[edge.ToId], edge.Label, edge.Kind);
            presenter.PropertyChanged += OnEdgeChanged;
            Edges.Add(presenter);
        }

        _placed = graph.Nodes.Count;
    }

    // Builds the domain Diagram Graph from the current presenters — the one place structure becomes a
    // DiagramGraph, validated by Create (INV-052) and emitted canonically (INV-051).
    private DiagramGraph BuildGraph() => DiagramGraph.Create(
        Kind, Direction, Nodes.Select(node => node.ToDomain()), Edges.Select(edge => edge.ToDomain()));

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(DiagramNodeViewModel.Label) or nameof(DiagramNodeViewModel.Shape)))
        {
            return; // a move (X/Y) is view-only and never re-emits (INV-051)
        }

        RaiseSource();
        if (ReferenceEquals(sender, SelectedNode) && e.PropertyName == nameof(DiagramNodeViewModel.Shape))
        {
            Raise(nameof(SelectedNodeShape));
        }
    }

    private void OnEdgeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(DiagramEdgeViewModel.Label) or nameof(DiagramEdgeViewModel.Kind)))
        {
            return;
        }

        RaiseSource();
        if (!ReferenceEquals(sender, SelectedEdge))
        {
            return;
        }

        Raise(e.PropertyName == nameof(DiagramEdgeViewModel.Kind) ? nameof(SelectedEdgeKind) : nameof(SelectedEdgeLabel));
    }

    private void RaiseSource() => Raise(nameof(MermaidSource));

    private NodeId NextId()
    {
        var used = new HashSet<string>(Nodes.Select(node => node.Id.Value), StringComparer.Ordinal);
        for (var i = 1; ; i++)
        {
            var candidate = $"n{i}";
            if (!used.Contains(candidate))
            {
                return new NodeId(candidate);
            }
        }
    }

    private (double X, double Y) NextSpot()
    {
        var spot = LayOut(_placed, Math.Max(_placed + 1, 4));
        _placed++;
        return spot;
    }

    // A simple grid placement so seeded or added nodes do not stack; the user drags them into shape.
    private static (double X, double Y) LayOut(int i, int total)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(total)));
        var column = i % columns;
        var row = i / columns;
        return (Margin + (column * (DiagramNodeViewModel.Width + Gap)),
            Margin + (row * (DiagramNodeViewModel.Height + Gap)));
    }
}
