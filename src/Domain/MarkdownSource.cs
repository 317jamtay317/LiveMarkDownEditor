namespace Domain;

/// <summary>
/// The source text of a <see cref="MarkdownDocument"/>, authored in Markdown syntax.
/// </summary>
/// <remarks>
/// Enforces INV-001: the source text is never <see langword="null"/>. An empty document is
/// represented by <see cref="Empty"/> (the empty string), never by <see langword="null"/>.
/// This is a value object: two instances with the same <see cref="Text"/> are equal.
/// </remarks>
public sealed record MarkdownSource
{
    /// <summary>The empty Markdown Document — an empty string, not <see langword="null"/>.</summary>
    public static MarkdownSource Empty { get; } = new(string.Empty);

    /// <summary>The Markdown source text. Never <see langword="null"/>.</summary>
    public string Text { get; }

    /// <summary>Creates a <see cref="MarkdownSource"/> from the given Markdown text.</summary>
    /// <param name="text">The Markdown source text; the empty string for an empty document.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="text"/> is <see langword="null"/> (violates INV-001).
    /// </exception>
    public MarkdownSource(string text)
    {
        Text = text ?? throw new ArgumentNullException(
            nameof(text),
            "A Markdown Document's source text is never null (INV-001).");
    }
}
