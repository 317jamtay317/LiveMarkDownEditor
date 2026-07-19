namespace Domain;

/// <summary>How a <see cref="DiagramEdge"/> is drawn, emitted as the matching Mermaid link operator.</summary>
public enum EdgeKind
{
    /// <summary>A solid arrow — Mermaid <c>--&gt;</c>.</summary>
    Arrow,

    /// <summary>A dotted arrow — Mermaid <c>-.-&gt;</c>.</summary>
    Dotted,

    /// <summary>A thick arrow — Mermaid <c>==&gt;</c>.</summary>
    Thick,

    /// <summary>An open line with no arrowhead — Mermaid <c>---</c>.</summary>
    Open,
}
