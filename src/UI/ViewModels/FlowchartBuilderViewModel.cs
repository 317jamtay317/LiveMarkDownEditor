using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Flowchart Builder's state and behaviour: the Diagram Nodes and Diagram Edges on the canvas, the
/// Flow Direction, and the commands that add, connect, reshape, and delete them. It edits a Diagram
/// Graph and exposes its canonical Mermaid source (<see cref="MermaidSource"/>), but touches no
/// Markdown Document — it only yields a <see cref="Result"/> when the user Inserts, and
/// <see langword="null"/> when they Cancel (INV-053). The heart of the builder lives here, kept free of
/// WPF so it is unit-testable (the <see cref="LinkPromptViewModel"/> pattern).
/// </summary>
public sealed class FlowchartBuilderViewModel : ObservableObject
{
    private const double Margin = 40;
    private const double Gap = 44;

    private readonly RelayCommand _deleteSelectedCommand;
    private FlowchartNodeViewModel? _selectedNode;
    private FlowchartEdgeViewModel? _selectedEdge;
    private FlowDirection _direction;
    private bool? _dialogResult;
    private int _placed;

    /// <summary>Creates the Flowchart Builder, seeded from an existing Mermaid Diagram or empty.</summary>
    /// <param name="existingSource">The Mermaid source to edit graphically, or null to start empty (INV-053).</param>
    /// <param name="isDark">Whether the app is in dark theme, so the live Diagram Preview matches it.</param>
    public FlowchartBuilderViewModel(string? existingSource, bool isDark = false)
    {
        IsDark = isDark;
        InsertCommand = new RelayCommand(() => DialogResult = true);
        CancelCommand = new RelayCommand(() => DialogResult = false);
        AddNodeCommand = new RelayCommand(AddNode);
        _deleteSelectedCommand = new RelayCommand(DeleteSelected, () => HasSelection);

        DiagramGraph.TryParse(existingSource, out var graph);
        _direction = graph.Direction;
        Seed(graph);
    }

    /// <summary>Whether the app is in dark theme, so the live Diagram Preview follows the editor palette.</summary>
    public bool IsDark { get; }

    /// <summary>The Diagram Nodes on the canvas, in order.</summary>
    public ObservableCollection<FlowchartNodeViewModel> Nodes { get; } = [];

    /// <summary>The Diagram Edges on the canvas, in order.</summary>
    public ObservableCollection<FlowchartEdgeViewModel> Edges { get; } = [];

    /// <summary>The Flow Directions the direction picker offers.</summary>
    public IReadOnlyList<FlowDirection> Directions { get; } = Enum.GetValues<FlowDirection>();

    /// <summary>The Node Shapes the shape picker offers.</summary>
    public IReadOnlyList<NodeShape> NodeShapes { get; } = Enum.GetValues<NodeShape>();

    /// <summary>The Edge Kinds the edge kind picker offers.</summary>
    public IReadOnlyList<EdgeKind> EdgeKinds { get; } = Enum.GetValues<EdgeKind>();

    /// <summary>The direction the flowchart flows. Changing it re-emits the source.</summary>
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

    /// <summary>The selected Diagram Node, or <see langword="null"/> when none (or an edge) is selected.</summary>
    public FlowchartNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set
        {
            if (Set(ref _selectedNode, value))
            {
                Raise(nameof(HasSelection));
                Raise(nameof(HasSelectedNode));
                Raise(nameof(SelectedNodeShape));
                _deleteSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>The selected Diagram Edge, or <see langword="null"/> when none (or a node) is selected.</summary>
    public FlowchartEdgeViewModel? SelectedEdge
    {
        get => _selectedEdge;
        private set
        {
            if (Set(ref _selectedEdge, value))
            {
                Raise(nameof(HasSelection));
                Raise(nameof(HasSelectedEdge));
                Raise(nameof(SelectedEdgeKind));
                Raise(nameof(SelectedEdgeLabel));
                _deleteSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Whether a Diagram Node or Diagram Edge is currently selected.</summary>
    public bool HasSelection => SelectedNode is not null || SelectedEdge is not null;

    /// <summary>Whether a Diagram Node is selected — enables the shape picker.</summary>
    public bool HasSelectedNode => SelectedNode is not null;

    /// <summary>Whether a Diagram Edge is selected — enables the edge kind and label editors.</summary>
    public bool HasSelectedEdge => SelectedEdge is not null;

    /// <summary>The selected node's shape; setting it reshapes that node. Rectangle when none is selected.</summary>
    public NodeShape SelectedNodeShape
    {
        get => SelectedNode?.Shape ?? NodeShape.Rectangle;
        set
        {
            if (SelectedNode is { } node)
            {
                node.Shape = value;
            }
        }
    }

    /// <summary>The selected edge's kind; setting it changes how that edge is drawn. Arrow when none is selected.</summary>
    public EdgeKind SelectedEdgeKind
    {
        get => SelectedEdge?.Kind ?? EdgeKind.Arrow;
        set
        {
            if (SelectedEdge is { } edge)
            {
                edge.Kind = value;
            }
        }
    }

    /// <summary>The selected edge's label; setting it relabels that edge. Empty when none is selected.</summary>
    public string SelectedEdgeLabel
    {
        get => SelectedEdge?.Label ?? string.Empty;
        set
        {
            if (SelectedEdge is { } edge)
            {
                edge.Label = value;
            }
        }
    }

    /// <summary>Makes <paramref name="node"/> the selection (clearing any other).</summary>
    /// <param name="node">The node to select.</param>
    public void SelectNode(FlowchartNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ClearSelectionFlags();
        node.IsSelected = true;
        SelectedEdge = null;
        SelectedNode = node;
    }

    /// <summary>Makes <paramref name="edge"/> the selection (clearing any other).</summary>
    /// <param name="edge">The edge to select.</param>
    public void SelectEdge(FlowchartEdgeViewModel edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ClearSelectionFlags();
        edge.IsSelected = true;
        SelectedNode = null;
        SelectedEdge = edge;
    }

    /// <summary>Clears the selection.</summary>
    public void ClearSelection()
    {
        ClearSelectionFlags();
        SelectedNode = null;
        SelectedEdge = null;
    }

    /// <summary>Moves a node to a new canvas position. View-only — it never changes the source (INV-051).</summary>
    /// <param name="node">The node to move.</param>
    /// <param name="x">The node box's new left edge.</param>
    /// <param name="y">The node box's new top edge.</param>
    public void MoveNode(FlowchartNodeViewModel node, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(node);
        node.X = x;
        node.Y = y;
    }

    /// <summary>Connects two nodes with a new Arrow edge and re-emits the source.</summary>
    /// <param name="from">The node the edge runs from.</param>
    /// <param name="to">The node the edge runs to.</param>
    /// <returns>The new edge presenter.</returns>
    public FlowchartEdgeViewModel Connect(FlowchartNodeViewModel from, FlowchartNodeViewModel to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var edge = new FlowchartEdgeViewModel(from, to, label: null, EdgeKind.Arrow);
        edge.PropertyChanged += OnEdgeChanged;
        Edges.Add(edge);
        RaiseSource();
        return edge;
    }

    private void AddNode()
    {
        var (x, y) = NextSpot();
        var node = new FlowchartNodeViewModel(NextId(), "Node", NodeShape.Rectangle, x, y);
        node.PropertyChanged += OnNodeChanged;
        Nodes.Add(node);
        SelectNode(node);
        RaiseSource();
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

    private void RemoveEdge(FlowchartEdgeViewModel edge)
    {
        edge.Detach();
        edge.PropertyChanged -= OnEdgeChanged;
        Edges.Remove(edge);
    }

    private void Seed(DiagramGraph graph)
    {
        var byId = new Dictionary<NodeId, FlowchartNodeViewModel>();
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            var (x, y) = LayOut(i, graph.Nodes.Count);
            var presenter = new FlowchartNodeViewModel(node.Id, node.Label, node.Shape, x, y);
            presenter.PropertyChanged += OnNodeChanged;
            Nodes.Add(presenter);
            byId[node.Id] = presenter;
        }

        foreach (var edge in graph.Edges)
        {
            var presenter = new FlowchartEdgeViewModel(byId[edge.FromId], byId[edge.ToId], edge.Label, edge.Kind);
            presenter.PropertyChanged += OnEdgeChanged;
            Edges.Add(presenter);
        }

        _placed = graph.Nodes.Count;
    }

    // Builds the domain Diagram Graph from the current presenters — the one place structure becomes a
    // DiagramGraph, validated by Create (INV-052) and emitted canonically (INV-051).
    private DiagramGraph BuildGraph() => DiagramGraph.Create(
        DiagramKind.Flowchart, Direction, Nodes.Select(node => node.ToDomain()), Edges.Select(edge => edge.ToDomain()));

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FlowchartNodeViewModel.Label) or nameof(FlowchartNodeViewModel.Shape)))
        {
            return; // a move (X/Y) is view-only and never re-emits (INV-051)
        }

        RaiseSource();
        if (ReferenceEquals(sender, SelectedNode) && e.PropertyName == nameof(FlowchartNodeViewModel.Shape))
        {
            Raise(nameof(SelectedNodeShape));
        }
    }

    private void OnEdgeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(FlowchartEdgeViewModel.Label) or nameof(FlowchartEdgeViewModel.Kind)))
        {
            return;
        }

        RaiseSource();
        if (!ReferenceEquals(sender, SelectedEdge))
        {
            return;
        }

        Raise(e.PropertyName == nameof(FlowchartEdgeViewModel.Kind) ? nameof(SelectedEdgeKind) : nameof(SelectedEdgeLabel));
    }

    private void RaiseSource() => Raise(nameof(MermaidSource));

    private void ClearSelectionFlags()
    {
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }

        foreach (var edge in Edges)
        {
            edge.IsSelected = false;
        }
    }

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
        return (Margin + (column * (FlowchartNodeViewModel.Width + Gap)),
            Margin + (row * (FlowchartNodeViewModel.Height + Gap)));
    }
}
