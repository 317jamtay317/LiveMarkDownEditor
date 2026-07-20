using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Configurable <see cref="IMermaidScriptSource"/> for tests. It yields whatever
/// <see cref="Script"/> is set to, standing in for the bundled Mermaid library a Standalone Page
/// embeds (INV-049).
/// </summary>
public sealed class FakeMermaidScriptSource : IMermaidScriptSource
{
    /// <summary>The script text this source yields, or <see langword="null"/> for none.</summary>
    public string? Script { get; set; }

    /// <inheritdoc />
    public string? Read() => Script;
}
