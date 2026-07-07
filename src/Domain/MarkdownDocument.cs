namespace Domain;

/// <summary>
/// The Markdown Document — the primary aggregate of the editor and the single canonical
/// representation of the content being edited. Its <see cref="Source"/> is what is persisted to
/// the Watched File and projected into the Visual Document the user edits.
/// </summary>
/// <remarks>
/// A Markdown Document is defined entirely by its source text: two documents with equal
/// <see cref="Source"/> are equal.
/// </remarks>
public sealed record MarkdownDocument
{
    /// <summary>The canonical Markdown source text of this document. Never <see langword="null"/>.</summary>
    public MarkdownSource Source { get; }

    /// <summary>Creates a Markdown Document from raw Markdown source text.</summary>
    /// <param name="source">The Markdown source text; the empty string for an empty document.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/> is <see langword="null"/> (violates INV-001).
    /// </exception>
    public MarkdownDocument(string source)
        : this(new MarkdownSource(source))
    {
    }

    /// <summary>Creates a Markdown Document from an existing <see cref="MarkdownSource"/>.</summary>
    /// <param name="source">The canonical source text value object.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    public MarkdownDocument(MarkdownSource source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>The empty Markdown Document.</summary>
    public static MarkdownDocument Empty { get; } = new(MarkdownSource.Empty);
}
