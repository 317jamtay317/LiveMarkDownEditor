using System.Text.RegularExpressions;

namespace Domain;

/// <summary>
/// The one place the Mermaid state-diagram syntax lives: emits a State Diagram
/// <see cref="DiagramGraph"/> as canonical Mermaid source and parses that syntax back into a graph
/// (INV-051). Pure — no I/O, no state.
/// </summary>
/// <remarks>
/// A State is written <c>id : label</c> (bare <c>id</c> when it has no Node Label) and a transition
/// <c>from --&gt; to : label</c>. A <see cref="NodeShape.Terminal"/> is written as Mermaid's
/// <c>[*]</c> start/end marker, which carries no identifier — so a Terminal's Node Id is never emitted,
/// and parsing reads back one Terminal per <c>[*]</c> occurrence (which is one circle per occurrence,
/// exactly what Mermaid draws) with a freshly minted Node Id (INV-051).
/// </remarks>
internal static partial class StateDiagramMermaidFormat
{
    private const string TerminalToken = "[*]";

    /// <summary>Emits <paramref name="graph"/> as canonical Mermaid state-diagram source (INV-051).</summary>
    public static string Emit(DiagramGraph graph)
    {
        var terminals = new HashSet<string>(
            graph.Nodes.Where(node => node.Shape == NodeShape.Terminal).Select(node => node.Id.Value),
            StringComparer.Ordinal);
        var connected = new HashSet<string>(
            graph.Edges.SelectMany(edge => new[] { edge.FromId.Value, edge.ToId.Value }), StringComparer.Ordinal);

        var lines = new List<string> { "stateDiagram-v2", MermaidDirection.Statement(graph.Direction) };

        foreach (var node in graph.Nodes)
        {
            if (node.Shape == NodeShape.Terminal)
            {
                // A Terminal appears in the source only through the transitions that touch it; one with
                // none is written on its own line so it is not lost (INV-051).
                if (!connected.Contains(node.Id.Value))
                {
                    lines.Add("    " + TerminalToken);
                }
            }
            else
            {
                lines.Add("    " + (node.Label.Length == 0
                    ? node.Id.Value
                    : $"{node.Id.Value} : {MermaidText.Encode(node.Label, ":")}"));
            }
        }

        foreach (var edge in graph.Edges)
        {
            var from = terminals.Contains(edge.FromId.Value) ? TerminalToken : edge.FromId.Value;
            var to = terminals.Contains(edge.ToId.Value) ? TerminalToken : edge.ToId.Value;
            lines.Add("    " + (edge.Label is { } label
                ? $"{from} --> {to} : {MermaidText.Encode(label, ":")}"
                : $"{from} --> {to}"));
        }

        return string.Join("\n", lines);
    }

    /// <summary>Parses Mermaid state-diagram source; false (empty graph) when it is not one.</summary>
    public static bool TryParse(string? source, out DiagramGraph graph)
    {
        graph = DiagramGraph.Empty(DiagramKind.StateDiagram, FlowDirection.TopDown);
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
            else if (TransitionPattern().Match(line) is { Success: true } transition)
            {
                var from = Declare(nodes, index, transition.Groups["left"].Value, label: null);
                var to = Declare(nodes, index, transition.Groups["right"].Value, label: null);
                var raw = transition.Groups["label"];
                edges.Add(new DiagramEdge(from, to, raw.Success ? MermaidText.Decode(raw.Value.Trim()) : null, EdgeKind.Arrow));
            }
            else if (line == TerminalToken)
            {
                Declare(nodes, index, TerminalToken, label: null);
            }
            else if (DeclarationPattern().Match(line) is { Success: true } declaration)
            {
                var raw = declaration.Groups["label"];
                Declare(
                    nodes,
                    index,
                    declaration.Groups["id"].Value,
                    raw.Success ? MermaidText.Decode(raw.Value.Trim()) : null);
            }

            // Any other line (a composite state, a note, a comment, styling, …) is ignored —
            // best-effort, as the Flowchart's own parse is.
        }

        try
        {
            graph = DiagramGraph.Create(DiagramKind.StateDiagram, direction, nodes, edges);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // Declares (or updates) the node a token names, and returns its Node Id. Every [*] occurrence
    // becomes its own Terminal with a minted Id — the token names no node, and Mermaid draws one
    // circle per occurrence (INV-051). The minted Id holds a '*', which no parsed identifier can, so
    // it can never collide with one read from the source.
    private static NodeId Declare(List<DiagramNode> nodes, Dictionary<string, int> index, string token, string? label)
    {
        if (token == TerminalToken)
        {
            var terminal = new NodeId("*" + (nodes.Count(node => node.Shape == NodeShape.Terminal) + 1));
            nodes.Add(new DiagramNode(terminal, string.Empty, NodeShape.Terminal));
            return terminal;
        }

        if (index.TryGetValue(token, out var position))
        {
            if (label is not null)
            {
                nodes[position] = new DiagramNode(nodes[position].Id, label, nodes[position].Shape);
            }

            return nodes[position].Id;
        }

        var id = new NodeId(token);
        nodes.Add(new DiagramNode(id, label ?? string.Empty, NodeShape.Rounded));
        index[token] = nodes.Count - 1;
        return id;
    }

    [GeneratedRegex(@"^stateDiagram(-v2)?[ \t]*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"^(?<left>[A-Za-z0-9_]+|\[\*\])[ \t]*-->[ \t]*(?<right>[A-Za-z0-9_]+|\[\*\])[ \t]*(?::[ \t]*(?<label>.*))?$")]
    private static partial Regex TransitionPattern();

    [GeneratedRegex(@"^(?<id>[A-Za-z0-9_]+)[ \t]*(?::[ \t]*(?<label>.*))?$")]
    private static partial Regex DeclarationPattern();
}
