using Domain;

namespace UI.ViewModels;

// The Diagram Builder's selection: which Diagram Node or Diagram Edge is the current one, and the
// editors that act on it — the shape picker, the edge kind picker, and the Edge Label box. Exactly one
// of a node or an edge is ever selected, and it is the Delete target. Selecting is view state: it
// changes no Diagram Graph and no Markdown Document (INV-053).
public sealed partial class DiagramBuilderViewModel
{
    private DiagramNodeViewModel? _selectedNode;
    private DiagramEdgeViewModel? _selectedEdge;

    /// <summary>The selected Diagram Node, or <see langword="null"/> when none (or an edge) is selected.</summary>
    public DiagramNodeViewModel? SelectedNode
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
    public DiagramEdgeViewModel? SelectedEdge
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

    /// <summary>
    /// The selected node's shape; setting it reshapes that node. The current kind's default when none is
    /// selected, so the picker never shows a shape the kind does not offer (INV-070).
    /// </summary>
    public NodeShape SelectedNodeShape
    {
        get => SelectedNode?.Shape ?? Kind.DefaultShape();
        set
        {
            if (SelectedNode is { } node)
            {
                node.Shape = value;
            }
        }
    }

    /// <summary>
    /// The selected edge's kind; setting it changes how that edge is drawn. The current kind's default
    /// when none is selected (INV-070).
    /// </summary>
    public EdgeKind SelectedEdgeKind
    {
        get => SelectedEdge?.Kind ?? Kind.DefaultEdgeKind();
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
    public void SelectNode(DiagramNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ClearSelectionFlags();
        node.IsSelected = true;
        SelectedEdge = null;
        SelectedNode = node;
    }

    /// <summary>Makes <paramref name="edge"/> the selection (clearing any other).</summary>
    /// <param name="edge">The edge to select.</param>
    public void SelectEdge(DiagramEdgeViewModel edge)
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
}
