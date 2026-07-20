using Domain;

namespace UI.Tests.TestDoubles;

/// <summary>
/// An <see cref="IMermaidImageRenderer"/> that renders nothing (every diagram yields <see langword="null"/>),
/// so the editor shows the source-text fallback (INV-047) — enough to satisfy the Workspace's dependency
/// in a headless test without spinning up a browser.
/// </summary>
internal sealed class FakeMermaidImageRenderer : IMermaidImageRenderer
{
    /// <inheritdoc />
    public Task<DiagramImage?> RenderAsync(string source) => Task.FromResult<DiagramImage?>(null);
}
