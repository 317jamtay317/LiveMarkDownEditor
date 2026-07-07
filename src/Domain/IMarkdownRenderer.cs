namespace Domain;

/// <summary>
/// Port for rendering a <see cref="MarkdownDocument"/> to its <see cref="RenderedOutput"/> (HTML).
/// The Domain owns this contract; an adapter in the Infrastructure layer implements it.
/// </summary>
/// <remarks>
/// Enforces INV-002: an implementation MUST be a pure function of the document's source text —
/// rendering the same source always yields the same Rendered Output, with no hidden state.
/// </remarks>
public interface IMarkdownRenderer
{
    /// <summary>Renders the given Markdown Document to HTML.</summary>
    /// <param name="document">The Markdown Document to render.</param>
    /// <returns>The <see cref="RenderedOutput"/> HTML for the document.</returns>
    RenderedOutput Render(MarkdownDocument document);
}
