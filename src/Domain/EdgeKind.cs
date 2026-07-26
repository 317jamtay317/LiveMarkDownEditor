namespace Domain;

/// <summary>
/// How a <see cref="DiagramEdge"/> is drawn, emitted as the matching Mermaid link operator. Which kinds
/// an edge may take depends on its <see cref="DiagramKind"/> — its Edge Set
/// (<see cref="DiagramKinds.EdgeSet"/>, INV-052).
/// </summary>
public enum EdgeKind
{
    /// <summary>A solid arrow — a Flowchart's <c>--&gt;</c>, a State Diagram's transition, a Class Diagram's association.</summary>
    Arrow,

    /// <summary>A dotted arrow — Mermaid <c>-.-&gt;</c>. Flowchart only.</summary>
    Dotted,

    /// <summary>A thick arrow — Mermaid <c>==&gt;</c>. Flowchart only.</summary>
    Thick,

    /// <summary>An open line with no arrowhead — a Flowchart's <c>---</c>, a Class Diagram's link <c>--</c>.</summary>
    Open,

    /// <summary>Class Diagram inheritance — Mermaid <c>&lt;|--</c>, with the hollow triangle at the From end.</summary>
    Inheritance,

    /// <summary>Class Diagram composition — Mermaid <c>*--</c>, with the filled diamond at the From end.</summary>
    Composition,

    /// <summary>Class Diagram aggregation — Mermaid <c>o--</c>, with the hollow diamond at the From end.</summary>
    Aggregation,

    /// <summary>Class Diagram dependency — Mermaid <c>..&gt;</c>, a dashed arrow.</summary>
    Dependency,

    /// <summary>An Entity Relationship cardinality of exactly one to exactly one — Mermaid <c>||--||</c>.</summary>
    OneToOne,

    /// <summary>An Entity Relationship cardinality of exactly one to many — Mermaid <c>||--o{</c>.</summary>
    OneToMany,

    /// <summary>An Entity Relationship cardinality of many to exactly one — Mermaid <c>}o--||</c>.</summary>
    ManyToOne,

    /// <summary>An Entity Relationship cardinality of many to many — Mermaid <c>}o--o{</c>.</summary>
    ManyToMany,
}
