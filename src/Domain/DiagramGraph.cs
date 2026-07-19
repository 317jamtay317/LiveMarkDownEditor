namespace Domain;

/// <summary>
/// The structured node/arrow model of a Mermaid Diagram that the Flowchart Builder edits: a
/// <see cref="DiagramKind"/>, a <see cref="FlowDirection"/>, an ordered list of Diagram Nodes, and an
/// ordered list of Diagram Edges. It is the graphical counterpart of the diagram's Mermaid source —
/// <see cref="Parse"/>d from that source and emitted back with <see cref="ToMermaidSource"/> as
/// canonical Mermaid, so the source text stays the single source of truth (INV-051).
/// </summary>
/// <remarks>
/// An immutable value object (the <see cref="RecentFiles"/> pattern): it is constructed valid and its
/// operations return new graphs rather than mutating. It cannot be built in an invalid state — Node
/// Ids are unique and non-blank, and every Diagram Edge references declared Diagram Nodes (INV-052). A
/// Diagram Graph models only structure; node layout is Mermaid's to compute and is not part of it, so
/// the builder's on-canvas positions live outside the graph.
/// </remarks>
public sealed class DiagramGraph
{
    private readonly List<DiagramNode> _nodes;
    private readonly List<DiagramEdge> _edges;

    private DiagramGraph(DiagramKind kind, FlowDirection direction, List<DiagramNode> nodes, List<DiagramEdge> edges)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (!ids.Add(node.Id.Value))
            {
                throw new ArgumentException($"Duplicate Node Id '{node.Id.Value}'.", nameof(nodes));
            }
        }

        foreach (var edge in edges)
        {
            if (!ids.Contains(edge.FromId.Value) || !ids.Contains(edge.ToId.Value))
            {
                throw new ArgumentException("A Diagram Edge references an undeclared Diagram Node.", nameof(edges));
            }
        }

        Kind = kind;
        Direction = direction;
        _nodes = nodes;
        _edges = edges;
    }

    /// <summary>An empty Diagram Graph of the given kind and direction.</summary>
    /// <param name="kind">Which node/arrow diagram this is.</param>
    /// <param name="direction">The direction it flows.</param>
    /// <returns>An empty Diagram Graph.</returns>
    public static DiagramGraph Empty(DiagramKind kind, FlowDirection direction) => new(kind, direction, [], []);

    /// <summary>
    /// Builds a Diagram Graph from nodes and edges, enforcing validity (INV-052): unique non-blank Node
    /// Ids, and every edge referencing a declared node.
    /// </summary>
    /// <param name="kind">Which node/arrow diagram this is.</param>
    /// <param name="direction">The direction it flows.</param>
    /// <param name="nodes">The Diagram Nodes, in order.</param>
    /// <param name="edges">The Diagram Edges, in order.</param>
    /// <returns>The Diagram Graph.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nodes"/> or <paramref name="edges"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a Node Id is duplicated or an edge references an undeclared node.</exception>
    public static DiagramGraph Create(
        DiagramKind kind, FlowDirection direction, IEnumerable<DiagramNode> nodes, IEnumerable<DiagramEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        return new DiagramGraph(kind, direction, [.. nodes], [.. edges]);
    }

    /// <summary>Which node/arrow diagram this is.</summary>
    public DiagramKind Kind { get; }

    /// <summary>The direction the graph flows.</summary>
    public FlowDirection Direction { get; }

    /// <summary>The Diagram Nodes, in declaration order.</summary>
    public IReadOnlyList<DiagramNode> Nodes => _nodes;

    /// <summary>The Diagram Edges, in order.</summary>
    public IReadOnlyList<DiagramEdge> Edges => _edges;

    /// <summary>Returns a copy of this graph flowing in the given direction.</summary>
    /// <param name="direction">The new Flow Direction.</param>
    /// <returns>A new Diagram Graph; this instance is unchanged.</returns>
    public DiagramGraph WithDirection(FlowDirection direction) =>
        new(Kind, direction, [.. _nodes], [.. _edges]);

    /// <summary>
    /// Returns a copy of this graph with a new Diagram Node appended, its Node Id freshly minted
    /// (<c>n1</c>, <c>n2</c>, …) so it is unique within the graph.
    /// </summary>
    /// <param name="label">The new node's Node Label. May be empty; never null.</param>
    /// <param name="shape">The new node's shape.</param>
    /// <returns>A new Diagram Graph; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
    public DiagramGraph AddNode(string label, NodeShape shape)
    {
        ArgumentNullException.ThrowIfNull(label);

        var node = new DiagramNode(MintId(), label, shape);
        return new DiagramGraph(Kind, Direction, [.. _nodes, node], [.. _edges]);
    }

    /// <summary>Returns a copy of this graph with the given node's Node Label changed.</summary>
    /// <param name="id">The Node Id of the node to rename.</param>
    /// <param name="label">The new Node Label.</param>
    /// <returns>A new Diagram Graph; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> or <paramref name="label"/> is null.</exception>
    public DiagramGraph RenameNode(NodeId id, string label)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(label);

        var nodes = _nodes.Select(node => node.Id == id ? new DiagramNode(node.Id, label, node.Shape) : node).ToList();
        return new DiagramGraph(Kind, Direction, nodes, [.. _edges]);
    }

    /// <summary>Returns a copy of this graph with the given node's shape changed.</summary>
    /// <param name="id">The Node Id of the node to reshape.</param>
    /// <param name="shape">The new shape.</param>
    /// <returns>A new Diagram Graph; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
    public DiagramGraph SetNodeShape(NodeId id, NodeShape shape)
    {
        ArgumentNullException.ThrowIfNull(id);

        var nodes = _nodes.Select(node => node.Id == id ? new DiagramNode(node.Id, node.Label, shape) : node).ToList();
        return new DiagramGraph(Kind, Direction, nodes, [.. _edges]);
    }

    /// <summary>
    /// Returns a copy of this graph with the given node removed, together with every Diagram Edge that
    /// touches it — so no dangling edge is left behind (INV-052).
    /// </summary>
    /// <param name="id">The Node Id of the node to remove.</param>
    /// <returns>A new Diagram Graph; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
    public DiagramGraph RemoveNode(NodeId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        var nodes = _nodes.Where(node => node.Id != id).ToList();
        var edges = _edges.Where(edge => edge.FromId != id && edge.ToId != id).ToList();
        return new DiagramGraph(Kind, Direction, nodes, edges);
    }

    /// <summary>
    /// Returns a copy of this graph with a Diagram Edge added between two declared nodes.
    /// </summary>
    /// <param name="fromId">The Node Id the edge runs from — must be declared in this graph.</param>
    /// <param name="toId">The Node Id the edge runs to — must be declared in this graph.</param>
    /// <param name="label">The optional Edge Label, or null/blank for none.</param>
    /// <param name="kind">How the edge is drawn.</param>
    /// <returns>A new Diagram Graph; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fromId"/> or <paramref name="toId"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an endpoint is not a declared node (INV-052).</exception>
    public DiagramGraph Connect(NodeId fromId, NodeId toId, string? label, EdgeKind kind)
    {
        ArgumentNullException.ThrowIfNull(fromId);
        ArgumentNullException.ThrowIfNull(toId);

        var edge = new DiagramEdge(fromId, toId, label, kind);
        return new DiagramGraph(Kind, Direction, [.. _nodes], [.. _edges, edge]);
    }

    /// <summary>Returns a copy of this graph with the given Diagram Edge removed.</summary>
    /// <param name="edge">The edge to remove.</param>
    /// <returns>A new Diagram Graph; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="edge"/> is null.</exception>
    public DiagramGraph RemoveEdge(DiagramEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        var edges = new List<DiagramEdge>(_edges);
        edges.Remove(edge);
        return new DiagramGraph(Kind, Direction, [.. _nodes], edges);
    }

    /// <summary>
    /// Emits this Diagram Graph as canonical Mermaid source — the header, one declaration per Diagram
    /// Node in order, then one line per Diagram Edge (INV-051). Layout is not emitted; Mermaid computes
    /// it.
    /// </summary>
    /// <returns>The canonical Mermaid source.</returns>
    public string ToMermaidSource() => FlowchartMermaidFormat.Emit(this);

    /// <summary>
    /// Parses Mermaid flowchart source into a Diagram Graph — the inverse of
    /// <see cref="ToMermaidSource"/> over the forms it emits, and best-effort over hand-authored source
    /// (INV-051).
    /// </summary>
    /// <param name="source">The Mermaid source to parse.</param>
    /// <returns>The parsed Diagram Graph.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="source"/> is not a Mermaid flowchart.</exception>
    public static DiagramGraph Parse(string source) =>
        TryParse(source, out var graph) ? graph : throw new FormatException("The source is not a Mermaid flowchart.");

    /// <summary>
    /// Tries to parse Mermaid flowchart source into a Diagram Graph. Returns <see langword="false"/>
    /// (with an empty graph) when the source is not a flowchart, so the Flowchart Builder can start
    /// empty rather than guessing (INV-053).
    /// </summary>
    /// <param name="source">The Mermaid source to parse, or null.</param>
    /// <param name="graph">The parsed Diagram Graph, or an empty flowchart when parsing fails.</param>
    /// <returns><see langword="true"/> when the source parses as a flowchart; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? source, out DiagramGraph graph) =>
        FlowchartMermaidFormat.TryParse(source, out graph);

    // Mints the next free `n#` identifier not already used by a node in this graph.
    private NodeId MintId()
    {
        var used = new HashSet<string>(_nodes.Select(node => node.Id.Value), StringComparer.Ordinal);
        for (var i = 1; ; i++)
        {
            var candidate = $"n{i}";
            if (used.Add(candidate))
            {
                return new NodeId(candidate);
            }
        }
    }
}
