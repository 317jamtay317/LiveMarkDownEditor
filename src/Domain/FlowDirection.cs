namespace Domain;

/// <summary>
/// The direction a <see cref="DiagramGraph"/> flows, emitted as Mermaid's <c>TD</c> / <c>LR</c> /
/// <c>BT</c> / <c>RL</c> header token.
/// </summary>
public enum FlowDirection
{
    /// <summary>Top to bottom (Mermaid <c>TD</c>).</summary>
    TopDown,

    /// <summary>Left to right (Mermaid <c>LR</c>).</summary>
    LeftRight,

    /// <summary>Bottom to top (Mermaid <c>BT</c>).</summary>
    BottomUp,

    /// <summary>Right to left (Mermaid <c>RL</c>).</summary>
    RightLeft,
}
