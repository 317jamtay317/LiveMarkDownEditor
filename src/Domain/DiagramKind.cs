namespace Domain;

/// <summary>
/// Which node/arrow diagram a <see cref="DiagramGraph"/> is — its Diagram Kind. Only
/// <see cref="Flowchart"/> exists today; further node/arrow kinds (state, ER, class) may follow. The
/// kind selects the Mermaid header keyword and the node/edge syntax used to parse and emit the graph.
/// Non-graph Mermaid diagrams (sequence, gantt, pie) are not Diagram Kinds — they are authored as text.
/// </summary>
public enum DiagramKind
{
    /// <summary>A Mermaid flowchart — Diagram Nodes joined by directed Diagram Edges, headed <c>flowchart</c>.</summary>
    Flowchart,
}
