namespace Domain;

/// <summary>
/// The outline a <see cref="DiagramNode"/> is drawn with, emitted as the matching Mermaid node syntax.
/// Which shapes a node may take depends on its <see cref="DiagramKind"/> — its Shape Set
/// (<see cref="DiagramKinds.ShapeSet"/>, INV-052).
/// </summary>
public enum NodeShape
{
    /// <summary>A rectangle — a Flowchart's <c>id["label"]</c>, and the box a class or an entity is drawn as.</summary>
    Rectangle,

    /// <summary>A rectangle with rounded corners — a Flowchart's <c>id("label")</c>, and a State Diagram's State.</summary>
    Rounded,

    /// <summary>A stadium (pill) — Mermaid <c>id(["label"])</c>.</summary>
    Stadium,

    /// <summary>A diamond, for a decision — Mermaid <c>id{"label"}</c>.</summary>
    Diamond,

    /// <summary>A circle — Mermaid <c>id(("label"))</c>.</summary>
    Circle,

    /// <summary>
    /// A State Diagram's Terminal — its start and end marker, written <c>[*]</c> and drawn as a filled
    /// circle. It carries no Node Id in the source, so a Round-Trip mints it a fresh one (INV-051).
    /// </summary>
    Terminal,
}
