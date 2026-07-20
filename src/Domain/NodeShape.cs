namespace Domain;

/// <summary>
/// The outline a <see cref="DiagramNode"/> is drawn with, emitted as the matching Mermaid node-shape
/// delimiters around the quoted Node Label.
/// </summary>
public enum NodeShape
{
    /// <summary>A rectangle — Mermaid <c>id["label"]</c>.</summary>
    Rectangle,

    /// <summary>A rectangle with rounded corners — Mermaid <c>id("label")</c>.</summary>
    Rounded,

    /// <summary>A stadium (pill) — Mermaid <c>id(["label"])</c>.</summary>
    Stadium,

    /// <summary>A diamond, for a decision — Mermaid <c>id{"label"}</c>.</summary>
    Diamond,

    /// <summary>A circle — Mermaid <c>id(("label"))</c>.</summary>
    Circle,
}
