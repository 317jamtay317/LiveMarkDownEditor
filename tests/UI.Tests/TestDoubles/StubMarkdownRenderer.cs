using Domain;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Deterministic <see cref="IMarkdownRenderer"/> for tests. It fakes rendering with a fixed,
/// obviously-not-Markdig wrapper — the real Markdig adapter is tested in Infrastructure.Tests, so a
/// ViewModel test only needs to know that <em>this document's</em> Rendered Output was the one
/// exported (INV-032).
/// </summary>
public sealed class StubMarkdownRenderer : IMarkdownRenderer
{
    /// <summary>The Markdown Documents this renderer was asked to render, in order.</summary>
    public List<string> Rendered { get; } = [];

    /// <inheritdoc />
    public RenderedOutput Render(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Rendered.Add(document.Source.Text);
        return new RenderedOutput($"<article>{document.Source.Text}</article>");
    }
}
