using Domain;

namespace UI.Tests.TestDoubles;

/// <summary>
/// A recording <see cref="IMermaidImageRenderer"/> for tests: it captures every
/// <c>(source, dark)</c> it is asked to render and, by default, yields a valid 1×1 PNG so the editor's
/// render coordinator fills each diagram's picture (INV-047). Constructed with <c>rendersNothing</c> it
/// yields <see langword="null"/> instead, standing in for a diagram the real renderer cannot produce so
/// the editor shows the source-text fallback — enough to satisfy a headless test without a browser.
/// </summary>
internal sealed class FakeMermaidImageRenderer(bool rendersNothing = false) : IMermaidImageRenderer
{
    // A valid 1×1 PNG, so the coordinator can decode it into an ImageSource.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    /// <summary>Each <c>(source, dark)</c> this renderer was asked to render, in order.</summary>
    public List<(string Source, bool Dark)> Calls { get; } = [];

    /// <inheritdoc />
    public Task<DiagramImage?> RenderAsync(string source, bool dark)
    {
        Calls.Add((source, dark));
        return Task.FromResult<DiagramImage?>(rendersNothing ? null : new DiagramImage(OnePixelPng, 1, 1));
    }
}
