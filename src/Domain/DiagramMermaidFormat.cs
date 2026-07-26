namespace Domain;

/// <summary>
/// Chooses the Mermaid format a <see cref="DiagramGraph"/> is written in and read back from: one
/// strategy per <see cref="DiagramKind"/>. Emission asks the graph's own kind; parsing asks each
/// strategy in turn, and they are discriminated by their Mermaid header, so exactly one can claim any
/// given source (INV-051). Pure — no I/O, no state.
/// </summary>
internal static class DiagramMermaidFormat
{
    /// <summary>Emits <paramref name="graph"/> as canonical Mermaid source in its own kind's syntax.</summary>
    /// <param name="graph">The Diagram Graph to write.</param>
    /// <returns>The canonical Mermaid source.</returns>
    public static string Emit(DiagramGraph graph) => graph.Kind switch
    {
        DiagramKind.StateDiagram => StateDiagramMermaidFormat.Emit(graph),
        DiagramKind.ClassDiagram => ClassDiagramMermaidFormat.Emit(graph),
        DiagramKind.EntityRelationshipDiagram => EntityRelationshipMermaidFormat.Emit(graph),
        _ => FlowchartMermaidFormat.Emit(graph),
    };

    /// <summary>
    /// Parses <paramref name="source"/> as whichever Diagram Kind its header names. Yields an empty
    /// Flowchart and <see langword="false"/> when no kind claims it — a sequence, gantt or pie diagram,
    /// or anything that is not a Mermaid diagram at all — so the Diagram Builder can start empty rather
    /// than guess (INV-053).
    /// </summary>
    /// <param name="source">The Mermaid source to read, or null.</param>
    /// <param name="graph">The parsed Diagram Graph, or an empty Flowchart when no kind claims the source.</param>
    /// <returns><see langword="true"/> when a Diagram Kind claimed the source.</returns>
    public static bool TryParse(string? source, out DiagramGraph graph)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            if (FlowchartMermaidFormat.TryParse(source, out var flowchart))
            {
                graph = flowchart;
                return true;
            }

            if (StateDiagramMermaidFormat.TryParse(source, out var state))
            {
                graph = state;
                return true;
            }

            if (ClassDiagramMermaidFormat.TryParse(source, out var classDiagram))
            {
                graph = classDiagram;
                return true;
            }

            if (EntityRelationshipMermaidFormat.TryParse(source, out var entities))
            {
                graph = entities;
                return true;
            }
        }

        graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown);
        return false;
    }
}
