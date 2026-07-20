namespace Domain;

/// <summary>
/// One Diagram Edge of a <see cref="DiagramGraph"/>: a directed connection from <see cref="FromId"/>
/// to <see cref="ToId"/>, an optional Edge Label, and an <see cref="EdgeKind"/>. An immutable value
/// object, compared by value. A blank Edge Label is normalised to none, so an edge that carries no
/// text is always <see cref="Label"/> <see langword="null"/> rather than empty (keeping the Round-Trip
/// canonical — INV-051).
/// </summary>
public sealed record DiagramEdge
{
    /// <summary>Creates a Diagram Edge.</summary>
    /// <param name="fromId">The Node Id the edge runs from.</param>
    /// <param name="toId">The Node Id the edge runs to.</param>
    /// <param name="label">The optional Edge Label, or null/blank when the edge carries no text.</param>
    /// <param name="kind">How the edge is drawn.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fromId"/> or <paramref name="toId"/> is null.</exception>
    public DiagramEdge(NodeId fromId, NodeId toId, string? label, EdgeKind kind)
    {
        ArgumentNullException.ThrowIfNull(fromId);
        ArgumentNullException.ThrowIfNull(toId);

        FromId = fromId;
        ToId = toId;
        Label = string.IsNullOrWhiteSpace(label) ? null : label;
        Kind = kind;
    }

    /// <summary>The Node Id the edge runs from.</summary>
    public NodeId FromId { get; }

    /// <summary>The Node Id the edge runs to.</summary>
    public NodeId ToId { get; }

    /// <summary>The optional Edge Label, or <see langword="null"/> when the edge carries no text.</summary>
    public string? Label { get; }

    /// <summary>How the edge is drawn.</summary>
    public EdgeKind Kind { get; }
}
