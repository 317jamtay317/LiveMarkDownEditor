namespace Domain;

/// <summary>
/// Which node/arrow diagram a <see cref="DiagramGraph"/> is — its Diagram Kind. The kind selects the
/// Mermaid header keyword, the node/edge syntax used to parse and emit the graph, and the Shape Set and
/// Edge Set it allows (<see cref="DiagramKinds"/>). Non-graph Mermaid diagrams (sequence, gantt, pie)
/// are not Diagram Kinds — they are authored as text with a live Diagram Preview.
/// </summary>
public enum DiagramKind
{
    /// <summary>A Mermaid flowchart — Diagram Nodes joined by directed Diagram Edges, headed <c>flowchart</c>.</summary>
    Flowchart,

    /// <summary>
    /// A Mermaid state diagram — States joined by transitions, with Mermaid's <c>[*]</c> Terminal as the
    /// start and end marker. Headed <c>stateDiagram-v2</c>.
    /// </summary>
    StateDiagram,

    /// <summary>
    /// A Mermaid class diagram — classes joined by inheritance, composition, aggregation, association
    /// and dependency relationships. Headed <c>classDiagram</c>.
    /// </summary>
    ClassDiagram,

    /// <summary>
    /// A Mermaid entity relationship diagram — entities joined by relationships that carry a
    /// cardinality. Headed <c>erDiagram</c>.
    /// </summary>
    EntityRelationshipDiagram,
}
