using System.Text.RegularExpressions;

namespace Domain;

/// <summary>
/// The one place the Mermaid flowchart syntax lives: emits a <see cref="DiagramGraph"/> as canonical
/// Mermaid source and parses Mermaid flowchart source back into a graph (INV-051). Pure — no I/O, no
/// state — so <see cref="DiagramGraph.ToMermaidSource"/> and <see cref="DiagramGraph.Parse"/> stay
/// deterministic. Emission is exact and parsing is its inverse over the forms emit produces;
/// hand-authored source is parsed best-effort, and lines it cannot model are ignored.
/// </summary>
internal static partial class FlowchartMermaidFormat
{
    /// <summary>Emits <paramref name="graph"/> as canonical Mermaid source (INV-051).</summary>
    public static string Emit(DiagramGraph graph)
    {
        var lines = new List<string> { $"flowchart {DirectionToken(graph.Direction)}" };
        lines.AddRange(graph.Nodes.Select(node => "    " + NodeDeclaration(node)));
        lines.AddRange(graph.Edges.Select(edge => "    " + EdgeLine(edge)));
        return string.Join("\n", lines);
    }

    /// <summary>Parses Mermaid flowchart source into a graph; false (empty graph) when it is not a flowchart.</summary>
    public static bool TryParse(string? source, out DiagramGraph graph)
    {
        graph = DiagramGraph.Empty(DiagramKind.Flowchart, FlowDirection.TopDown);
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var i = 0;
        while (i < lines.Length && lines[i].Trim().Length == 0)
        {
            i++;
        }

        if (i >= lines.Length || !TryParseHeader(lines[i].Trim(), out var direction))
        {
            return false;
        }

        var nodes = new List<DiagramNode>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var edges = new List<DiagramEdge>();

        for (i++; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (TryParseEdge(line, out var fromSpec, out var toSpec, out var label, out var kind))
            {
                var from = Declare(nodes, index, fromSpec);
                var to = Declare(nodes, index, toSpec);
                edges.Add(new DiagramEdge(from, to, label, kind));
            }
            else if (TryParseNodeSpec(line, out var spec) && spec.Shape is not null)
            {
                Declare(nodes, index, spec);
            }

            // Any other line (subgraph, styling, comment, chained edge, …) is ignored — best-effort.
        }

        try
        {
            graph = DiagramGraph.Create(DiagramKind.Flowchart, direction, nodes, edges);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // --- Emit helpers ---

    private static string DirectionToken(FlowDirection direction) => direction switch
    {
        FlowDirection.LeftRight => "LR",
        FlowDirection.BottomUp => "BT",
        FlowDirection.RightLeft => "RL",
        _ => "TD",
    };

    private static string NodeDeclaration(DiagramNode node)
    {
        var id = node.Id.Value;
        var label = QuoteLabel(node.Label);
        return node.Shape switch
        {
            NodeShape.Rounded => $"{id}({label})",
            NodeShape.Stadium => $"{id}([{label}])",
            NodeShape.Diamond => $"{id}{{{label}}}",
            NodeShape.Circle => $"{id}(({label}))",
            _ => $"{id}[{label}]",
        };
    }

    private static string EdgeLine(DiagramEdge edge)
    {
        var op = EdgeOperator(edge.Kind);
        var from = edge.FromId.Value;
        var to = edge.ToId.Value;
        return edge.Label is { } label
            ? $"{from} {op}|{label.Replace("|", "#124;")}| {to}"
            : $"{from} {op} {to}";
    }

    private static string EdgeOperator(EdgeKind kind) => kind switch
    {
        EdgeKind.Dotted => "-.->",
        EdgeKind.Thick => "==>",
        EdgeKind.Open => "---",
        _ => "-->",
    };

    private static string QuoteLabel(string label) => "\"" + label.Replace("\"", "#quot;") + "\"";

    // --- Parse helpers ---

    private static bool TryParseHeader(string header, out FlowDirection direction)
    {
        direction = FlowDirection.TopDown;
        var match = HeaderPattern().Match(header);
        if (!match.Success)
        {
            return false;
        }

        direction = match.Groups["dir"].Value.ToUpperInvariant() switch
        {
            "LR" => FlowDirection.LeftRight,
            "BT" => FlowDirection.BottomUp,
            "RL" => FlowDirection.RightLeft,
            _ => FlowDirection.TopDown,
        };
        return true;
    }

    private static bool TryParseEdge(
        string line, out NodeSpec from, out NodeSpec to, out string? label, out EdgeKind kind)
    {
        from = default;
        to = default;
        label = null;
        kind = EdgeKind.Arrow;

        var match = EdgePattern().Match(line);
        if (!match.Success ||
            !TryParseNodeSpec(match.Groups["left"].Value, out from) ||
            !TryParseNodeSpec(match.Groups["right"].Value, out to))
        {
            return false;
        }

        kind = match.Groups["op"].Value switch
        {
            "-.->" => EdgeKind.Dotted,
            "==>" => EdgeKind.Thick,
            "---" => EdgeKind.Open,
            _ => EdgeKind.Arrow,
        };

        var raw = match.Groups["label"];
        label = raw.Success ? Unquote(raw.Value.Replace("#124;", "|")) : null;
        return true;
    }

    private static bool TryParseNodeSpec(string text, out NodeSpec spec)
    {
        spec = default;
        var match = NodeSpecPattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var id = match.Groups["id"].Value;
        var rest = match.Groups["rest"].Value.Trim();
        if (rest.Length == 0)
        {
            spec = new NodeSpec(id, null, null);
            return true;
        }

        if (!TryParseShape(rest, out var label, out var shape))
        {
            return false;
        }

        spec = new NodeSpec(id, label, shape);
        return true;
    }

    private static bool TryParseShape(string rest, out string label, out NodeShape shape)
    {
        (string Open, string Close, NodeShape Shape)[] shapes =
        [
            ("([", "])", NodeShape.Stadium),
            ("((", "))", NodeShape.Circle),
            ("[", "]", NodeShape.Rectangle),
            ("(", ")", NodeShape.Rounded),
            ("{", "}", NodeShape.Diamond),
        ];

        foreach (var (open, close, candidate) in shapes)
        {
            if (rest.Length >= open.Length + close.Length && rest.StartsWith(open, StringComparison.Ordinal) &&
                rest.EndsWith(close, StringComparison.Ordinal))
            {
                var inner = rest.Substring(open.Length, rest.Length - open.Length - close.Length);
                label = Unquote(inner);
                shape = candidate;
                return true;
            }
        }

        label = string.Empty;
        shape = NodeShape.Rectangle;
        return false;
    }

    private static NodeId Declare(List<DiagramNode> nodes, Dictionary<string, int> index, NodeSpec spec)
    {
        if (index.TryGetValue(spec.Id, out var position))
        {
            if (spec.Shape is { } shape)
            {
                var existing = nodes[position];
                nodes[position] = new DiagramNode(existing.Id, spec.Label ?? existing.Label, shape);
            }

            return nodes[position].Id;
        }

        var id = new NodeId(spec.Id);
        nodes.Add(new DiagramNode(id, spec.Label ?? string.Empty, spec.Shape ?? NodeShape.Rectangle));
        index[spec.Id] = nodes.Count - 1;
        return id;
    }

    private static string Unquote(string inner)
    {
        inner = inner.Trim();
        if (inner.Length >= 2 && inner[0] == '"' && inner[^1] == '"')
        {
            inner = inner.Substring(1, inner.Length - 2);
        }

        return inner.Replace("#quot;", "\"");
    }

    [GeneratedRegex(@"^(flowchart|graph)\b[ \t]*(?<dir>TD|TB|LR|RL|BT)?[ \t]*;?[ \t]*$", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"^(?<left>.+?)\s*(?<op>-\.->|==>|-->|---)\s*(?:\|(?<label>[^|]*)\|\s*)?(?<right>.+)$")]
    private static partial Regex EdgePattern();

    [GeneratedRegex(@"^(?<id>[A-Za-z0-9_]+)(?<rest>.*)$")]
    private static partial Regex NodeSpecPattern();

    private readonly record struct NodeSpec(string Id, string? Label, NodeShape? Shape);
}
