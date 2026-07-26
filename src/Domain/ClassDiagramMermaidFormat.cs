using System.Text.RegularExpressions;

namespace Domain;

/// <summary>
/// The one place the Mermaid class-diagram syntax lives: emits a Class Diagram
/// <see cref="DiagramGraph"/> as canonical Mermaid source and parses that syntax back into a graph
/// (INV-051). Pure — no I/O, no state.
/// </summary>
/// <remarks>
/// A class is written <c>class id["label"]</c> — bare <c>class id</c> when it has no Node Label,
/// because Mermaid rejects an empty one — and a relationship <c>from &lt;op&gt; to : label</c>, where
/// the operator's marker (a hollow triangle, a diamond) sits at the <b>From</b> end. The
/// <c>direction</c> statement is always written, which is also what makes an otherwise empty
/// <c>classDiagram</c> parseable.
/// </remarks>
internal static partial class ClassDiagramMermaidFormat
{
    /// <summary>Emits <paramref name="graph"/> as canonical Mermaid class-diagram source (INV-051).</summary>
    public static string Emit(DiagramGraph graph)
    {
        var lines = new List<string> { "classDiagram", MermaidDirection.Statement(graph.Direction) };

        lines.AddRange(graph.Nodes.Select(node => node.Label.Length == 0
            ? $"    class {node.Id.Value}"
            : $"    class {node.Id.Value}[{MermaidText.Quote(node.Label)}]"));

        lines.AddRange(graph.Edges.Select(edge =>
        {
            var relationship = $"    {edge.FromId.Value} {Operator(edge.Kind)} {edge.ToId.Value}";
            return edge.Label is { } label ? $"{relationship} : {MermaidText.Encode(label, ":;")}" : relationship;
        }));

        return string.Join("\n", lines);
    }

    /// <summary>Parses Mermaid class-diagram source; false (empty graph) when it is not one.</summary>
    public static bool TryParse(string? source, out DiagramGraph graph)
    {
        graph = DiagramGraph.Empty(DiagramKind.ClassDiagram, FlowDirection.TopDown);
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var lines = MermaidText.Lines(source);
        if (lines.Count == 0 || !HeaderPattern().IsMatch(lines[0]))
        {
            return false;
        }

        var direction = FlowDirection.TopDown;
        var nodes = new List<DiagramNode>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var edges = new List<DiagramEdge>();

        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];

            if (MermaidDirection.TryReadStatement(line, out var read))
            {
                direction = read;
            }
            else if (RelationshipPattern().Match(line) is { Success: true } relationship)
            {
                var from = Declare(nodes, index, relationship.Groups["left"].Value, label: null);
                var to = Declare(nodes, index, relationship.Groups["right"].Value, label: null);
                var raw = relationship.Groups["label"];
                edges.Add(new DiagramEdge(
                    from, to, raw.Success ? MermaidText.Decode(raw.Value.Trim()) : null, Kind(relationship.Groups["op"].Value)));
            }
            else if (DeclarationPattern().Match(line) is { Success: true } declaration)
            {
                var raw = declaration.Groups["label"];
                Declare(nodes, index, declaration.Groups["id"].Value, raw.Success ? MermaidText.Unquote(raw.Value) : null);
            }

            // Any other line (a member block, a note, a comment, styling, …) is ignored — best-effort.
        }

        try
        {
            graph = DiagramGraph.Create(DiagramKind.ClassDiagram, direction, nodes, edges);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string Operator(EdgeKind kind) => kind switch
    {
        EdgeKind.Inheritance => "<|--",
        EdgeKind.Composition => "*--",
        EdgeKind.Aggregation => "o--",
        EdgeKind.Dependency => "..>",
        EdgeKind.Open => "--",
        _ => "-->",
    };

    // Reads the operator back, mapping the forms Mermaid also accepts onto the Edge Set — a dashed
    // inheritance (a realisation) reads as Inheritance, a dashed link as Open, and so on.
    private static EdgeKind Kind(string op) => op switch
    {
        "<|--" or "<|.." or "..|>" or "--|>" => EdgeKind.Inheritance,
        "*--" or "*.." => EdgeKind.Composition,
        "o--" or "o.." => EdgeKind.Aggregation,
        "..>" or "<.." => EdgeKind.Dependency,
        "--" or ".." => EdgeKind.Open,
        _ => EdgeKind.Arrow,
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

    [GeneratedRegex(@"^classDiagram(-v2)?[ \t]*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"^class[ \t]+(?<id>[A-Za-z0-9_]+)(?:\[(?<label>.*)\])?[ \t]*;?$")]
    private static partial Regex DeclarationPattern();

    [GeneratedRegex(@"^(?<left>[A-Za-z0-9_]+)[ \t]*(?<op>\.\.\|>|--\|>|<\|--|<\|\.\.|\*--|\*\.\.|o--|o\.\.|<\.\.|\.\.>|<--|-->|--|\.\.)[ \t]*(?<right>[A-Za-z0-9_]+)[ \t]*(?::[ \t]*(?<label>.*))?$")]
    private static partial Regex RelationshipPattern();
}
