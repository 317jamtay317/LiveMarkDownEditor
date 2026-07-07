namespace Domain;

/// <summary>
/// The Rendered Output — the HTML produced by rendering a <see cref="MarkdownDocument"/>.
/// Used for export and interoperability, not for the on-screen editing surface.
/// </summary>
/// <remarks>This is a value object: two instances with the same <see cref="Html"/> are equal.</remarks>
public sealed record RenderedOutput
{
    /// <summary>The empty Rendered Output — the HTML of an empty document.</summary>
    public static RenderedOutput Empty { get; } = new(string.Empty);

    /// <summary>The rendered HTML. Never <see langword="null"/>.</summary>
    public string Html { get; }

    /// <summary>Creates a <see cref="RenderedOutput"/> wrapping the given HTML.</summary>
    /// <param name="html">The rendered HTML; the empty string when nothing was rendered.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is <see langword="null"/>.</exception>
    public RenderedOutput(string html)
    {
        Html = html ?? throw new ArgumentNullException(nameof(html));
    }
}
