using System.Text.RegularExpressions;

namespace Domain;

/// <summary>
/// The one place the Mermaid entity-relationship syntax lives: emits an Entity Relationship Diagram
/// <see cref="DiagramGraph"/> as canonical Mermaid source and parses that syntax back into a graph
/// (INV-051). Pure — no I/O, no state.
/// </summary>
/// <remarks>
/// An entity is written <c>id["label"]</c> — bare <c>id</c> when it has no Node Label, because Mermaid
/// rejects an empty one — and a relationship <c>from &lt;cardinality&gt; to : "label"</c>. Mermaid
/// demands a relationship label, so one with no Edge Label is written with an empty one. Mermaid spells
/// more cardinalities than the Edge Set holds; parsing folds each onto whether its end is one or many.
/// No <c>direction</c> statement is written: Mermaid lays an ER diagram out itself and reads one as two
/// more entities, so an Entity Relationship Diagram carries no Flow Direction at all (INV-051).
/// </remarks>
internal static partial class EntityRelationshipMermaidFormat
{
    /// <summary>Emits <paramref name="graph"/> as canonical Mermaid ER source (INV-051).</summary>
    public static string Emit(DiagramGraph graph)
    {
        var lines = new List<string> { "erDiagram" };

        lines.AddRange(graph.Nodes.Select(node => node.Label.Length == 0
            ? $"    {node.Id.Value}"
            : $"    {node.Id.Value}[{MermaidText.Quote(node.Label)}]"));

        lines.AddRange(graph.Edges.Select(edge =>
            $"    {edge.FromId.Value} {Cardinality(edge.Kind)} {edge.ToId.Value} : {MermaidText.Quote(edge.Label ?? string.Empty)}"));

        return string.Join("\n", lines);
    }

    /// <summary>Parses Mermaid ER source; false (empty graph) when it is not one.</summary>
    public static bool TryParse(string? source, out DiagramGraph graph)
    {
        graph = DiagramGraph.Empty(DiagramKind.EntityRelationshipDiagram, FlowDirection.TopDown);
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var lines = MermaidText.Lines(source);
        if (lines.Count == 0 || !HeaderPattern().IsMatch(lines[0]))
        {
            return false;
        }

        var nodes = new List<DiagramNode>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var edges = new List<DiagramEdge>();

        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];

            if (MermaidDirection.TryReadStatement(line, out _))
            {
                // A hand-authored `direction` statement is skipped rather than read: an ER diagram has
                // no Flow Direction, and reading it would otherwise declare an entity called "direction".
            }
            else if (RelationshipPattern().Match(line) is { Success: true } relationship)
            {
                var from = Declare(nodes, index, relationship.Groups["left"].Value, label: null);
                var to = Declare(nodes, index, relationship.Groups["right"].Value, label: null);
                edges.Add(new DiagramEdge(
                    from,
                    to,
                    MermaidText.Unquote(relationship.Groups["label"].Value),
                    Kind(relationship.Groups["from"].Value, relationship.Groups["to"].Value)));
            }
            else if (DeclarationPattern().Match(line) is { Success: true } declaration)
            {
                var raw = declaration.Groups["label"];
                Declare(nodes, index, declaration.Groups["id"].Value, raw.Success ? MermaidText.Unquote(raw.Value) : null);
            }

            // Any other line (an attribute block's contents, a comment, styling, …) is ignored.
        }

        try
        {
            graph = DiagramGraph.Create(
                DiagramKind.EntityRelationshipDiagram, FlowDirection.TopDown, nodes, edges);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string Cardinality(EdgeKind kind) => kind switch
    {
        EdgeKind.OneToOne => "||--||",
        EdgeKind.ManyToOne => "}o--||",
        EdgeKind.ManyToMany => "}o--o{",
        _ => "||--o{",
    };

    // Mermaid writes four cardinality markers per end; the Edge Set keeps the distinction that shows in
    // the diagram's shape — whether that end is one or many.
    private static EdgeKind Kind(string from, string to) => (from.StartsWith('}'), to.EndsWith('{')) switch
    {
        (false, false) => EdgeKind.OneToOne,
        (false, true) => EdgeKind.OneToMany,
        (true, false) => EdgeKind.ManyToOne,
        _ => EdgeKind.ManyToMany,
    };

    private static NodeId Declare(List<DiagramNode> nodes, Dictionary<string, int> index, string id, string? label)
    {
        if (index.TryGetValue(id, out var position))
        {
            if (label is not null)
            {
                nodes[position] = new DiagramNode(nodes[position].Id, label, nodes[position].Shape);
            }

            return nodes[position].Id;
        }

        var declared = new NodeId(id);
        nodes.Add(new DiagramNode(declared, label ?? string.Empty, NodeShape.Rectangle));
        index[id] = nodes.Count - 1;
        return declared;
    }

    [GeneratedRegex(@"^erDiagram[ \t]*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"^(?<id>[A-Za-z0-9_]+)(?:\[(?<label>.*)\])?[ \t]*(?:\{[ \t]*\}?)?[ \t]*$")]
    private static partial Regex DeclarationPattern();

    [GeneratedRegex(@"^(?<left>[A-Za-z0-9_]+)[ \t]*(?<from>\|\||\|o|\}o|\}\|)(?:--|\.\.)(?<to>\|\||o\||o\{|\|\{)[ \t]*(?<right>[A-Za-z0-9_]+)[ \t]*:[ \t]*(?<label>.*)$")]
    private static partial Regex RelationshipPattern();
}
