namespace Domain;

/// <summary>
/// One Diagram Node of a <see cref="DiagramGraph"/>: a <see cref="NodeId"/>, a Node Label (the text
/// shown in the node), and a <see cref="NodeShape"/>. An immutable value object, compared by value.
/// </summary>
public sealed record DiagramNode
{
    /// <summary>Creates a Diagram Node.</summary>
    /// <param name="id">The node's stable identifier in Mermaid source.</param>
    /// <param name="label">The Node Label — the text shown in the node. May be empty; never null.</param>
    /// <param name="shape">The outline the node is drawn with.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> or <paramref name="label"/> is null.</exception>
    public DiagramNode(NodeId id, string label, NodeShape shape)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(label);

        Id = id;
        Label = label;
        Shape = shape;
    }

    /// <summary>The node's stable identifier in Mermaid source.</summary>
    public NodeId Id { get; }

    /// <summary>The Node Label — the text shown in the node. May be empty.</summary>
    public string Label { get; }

    /// <summary>The outline the node is drawn with.</summary>
    public NodeShape Shape { get; }
}
