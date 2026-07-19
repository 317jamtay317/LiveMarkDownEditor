using Domain;

namespace Infrastructure.Tests;

/// <summary>
/// Deterministic <see cref="IMermaidImageRenderer"/> for tests. By default it yields a valid 1×1 PNG
/// (so a composed image loads when the PDF is rendered); constructed with <c>rendersNothing</c> it
/// yields <see langword="null"/>, standing in for a diagram the real renderer cannot produce so the
/// exporter falls back to the code text (INV-050).
/// </summary>
internal sealed class FakeMermaidImageRenderer(bool rendersNothing = false) : IMermaidImageRenderer
{
    // A valid 1×1 PNG, so the temp image the exporter writes loads when MigraDoc renders the PDF.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    /// <summary>The diagram sources this renderer was asked to render, in order.</summary>
    public List<string> Rendered { get; } = [];

    /// <inheritdoc />
    public Task<DiagramImage?> RenderAsync(string source)
    {
        Rendered.Add(source);
        return Task.FromResult<DiagramImage?>(rendersNothing ? null : new DiagramImage(OnePixelPng, 1, 1));
    }
}
