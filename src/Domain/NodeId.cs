namespace Domain;

/// <summary>
/// The stable identifier that names a <see cref="DiagramNode"/> in Mermaid source and that a
/// <see cref="DiagramEdge"/> references. A value object: never blank and free of whitespace (INV-052),
/// compared by value and ordinally — Mermaid identifiers are case-sensitive.
/// </summary>
public sealed record NodeId
{
    /// <summary>Creates a Node Id.</summary>
    /// <param name="value">The identifier text — non-blank and containing no whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, blank, or contains whitespace.</exception>
    public NodeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("A Node Id contains no whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
