namespace Domain;

/// <summary>
/// What each <see cref="DiagramKind"/> allows: its <b>Shape Set</b> — the Node Shapes its Diagram Nodes
/// may take — and its <b>Edge Set</b> — the Edge Kinds its Diagram Edges may take. A
/// <see cref="DiagramGraph"/> is valid only when every node and edge draws from these sets (INV-052),
/// and the Diagram Builder offers exactly them in its shape and edge pickers (INV-070).
/// </summary>
/// <remarks>
/// The first entry of each set is that kind's default — what a newly added Diagram Node is shaped as,
/// what a newly drawn Diagram Edge is, and what <see cref="Coerce(DiagramKind, NodeShape)"/> falls back
/// to when the diagram changes kind.
/// </remarks>
public static class DiagramKinds
{
    private static readonly NodeShape[] FlowchartShapes =
        [NodeShape.Rectangle, NodeShape.Rounded, NodeShape.Stadium, NodeShape.Diamond, NodeShape.Circle];

    private static readonly NodeShape[] StateShapes = [NodeShape.Rounded, NodeShape.Terminal];

    private static readonly NodeShape[] BoxShapes = [NodeShape.Rectangle];

    private static readonly EdgeKind[] FlowchartEdges =
        [EdgeKind.Arrow, EdgeKind.Dotted, EdgeKind.Thick, EdgeKind.Open];

    private static readonly EdgeKind[] StateEdges = [EdgeKind.Arrow];

    private static readonly EdgeKind[] ClassEdges =
    [
        EdgeKind.Arrow, EdgeKind.Inheritance, EdgeKind.Composition, EdgeKind.Aggregation,
        EdgeKind.Dependency, EdgeKind.Open,
    ];

    private static readonly EdgeKind[] CardinalityEdges =
        [EdgeKind.OneToMany, EdgeKind.OneToOne, EdgeKind.ManyToOne, EdgeKind.ManyToMany];

    /// <summary>The Shape Set <paramref name="kind"/> allows, its default first.</summary>
    /// <param name="kind">The Diagram Kind to ask.</param>
    /// <returns>The Node Shapes a Diagram Node of that kind may take.</returns>
    public static IReadOnlyList<NodeShape> ShapeSet(this DiagramKind kind) => kind switch
    {
        DiagramKind.StateDiagram => StateShapes,
        DiagramKind.ClassDiagram => BoxShapes,
        DiagramKind.EntityRelationshipDiagram => BoxShapes,
        _ => FlowchartShapes,
    };

    /// <summary>The Edge Set <paramref name="kind"/> allows, its default first.</summary>
    /// <param name="kind">The Diagram Kind to ask.</param>
    /// <returns>The Edge Kinds a Diagram Edge of that kind may take.</returns>
    public static IReadOnlyList<EdgeKind> EdgeSet(this DiagramKind kind) => kind switch
    {
        DiagramKind.StateDiagram => StateEdges,
        DiagramKind.ClassDiagram => ClassEdges,
        DiagramKind.EntityRelationshipDiagram => CardinalityEdges,
        _ => FlowchartEdges,
    };

    /// <summary>
    /// Whether <paramref name="kind"/>'s Mermaid carries a <see cref="FlowDirection"/> at all. An Entity
    /// Relationship Diagram does not — Mermaid lays it out itself, and reads a <c>direction</c>
    /// statement in one as two more entities — so its Flow Direction is always Top-Down (INV-051).
    /// </summary>
    /// <param name="kind">The Diagram Kind to ask.</param>
    /// <returns><see langword="true"/> when the kind's source can say which way it flows.</returns>
    public static bool CarriesFlowDirection(this DiagramKind kind) =>
        kind is not DiagramKind.EntityRelationshipDiagram;

    /// <summary>The Node Shape a newly added Diagram Node of <paramref name="kind"/> takes.</summary>
    /// <param name="kind">The Diagram Kind to ask.</param>
    /// <returns>The first shape of its Shape Set.</returns>
    public static NodeShape DefaultShape(this DiagramKind kind) => ShapeSet(kind)[0];

    /// <summary>The Edge Kind a newly drawn Diagram Edge of <paramref name="kind"/> takes.</summary>
    /// <param name="kind">The Diagram Kind to ask.</param>
    /// <returns>The first kind of its Edge Set.</returns>
    public static EdgeKind DefaultEdgeKind(this DiagramKind kind) => EdgeSet(kind)[0];

    /// <summary>Whether <paramref name="kind"/> allows <paramref name="shape"/> (INV-052).</summary>
    /// <param name="kind">The Diagram Kind to ask.</param>
    /// <param name="shape">The Node Shape in question.</param>
    /// <returns><see langword="true"/> when the shape is in the kind's Shape Set.</returns>
    public static bool Allows(this DiagramKind kind, NodeShape shape) => ShapeSet(kind).Contains(shape);

    /// <summary>Whether <paramref name="kind"/> allows <paramref name="edgeKind"/> (INV-052).</summary>
    /// <param name="kind">The Diagram Kind to ask.</param>
    /// <param name="edgeKind">The Edge Kind in question.</param>
    /// <returns><see langword="true"/> when the edge kind is in the kind's Edge Set.</returns>
    public static bool Allows(this DiagramKind kind, EdgeKind edgeKind) => EdgeSet(kind).Contains(edgeKind);

    /// <summary>
    /// <paramref name="shape"/> when <paramref name="kind"/> allows it, otherwise that kind's default —
    /// how a Diagram Graph keeps its nodes when it changes kind (INV-070).
    /// </summary>
    /// <param name="kind">The Diagram Kind to fit the shape to.</param>
    /// <param name="shape">The Node Shape to keep or replace.</param>
    /// <returns>A Node Shape the kind allows.</returns>
    public static NodeShape Coerce(this DiagramKind kind, NodeShape shape) =>
        Allows(kind, shape) ? shape : DefaultShape(kind);

    /// <summary>
    /// <paramref name="edgeKind"/> when <paramref name="kind"/> allows it, otherwise that kind's
    /// default — how a Diagram Graph keeps its edges when it changes kind (INV-070).
    /// </summary>
    /// <param name="kind">The Diagram Kind to fit the edge kind to.</param>
    /// <param name="edgeKind">The Edge Kind to keep or replace.</param>
    /// <returns>An Edge Kind the kind allows.</returns>
    public static EdgeKind Coerce(this DiagramKind kind, EdgeKind edgeKind) =>
        Allows(kind, edgeKind) ? edgeKind : DefaultEdgeKind(kind);
}
