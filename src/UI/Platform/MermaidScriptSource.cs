using System.IO;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// Reads the bundled Mermaid library from the app's assets, for embedding into a Standalone Page so
/// its Mermaid Diagrams render in a browser (INV-049). The library is copied beside the executable at
/// build time; when it cannot be read, <see cref="Read"/> yields <see langword="null"/> and the
/// export carries the diagrams as unrendered code blocks (Render stays pure — INV-002).
/// </summary>
public sealed class MermaidScriptSource : IMermaidScriptSource
{
    private static readonly string ScriptPath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Mermaid", "mermaid.min.js");

    private string? _cached;

    /// <inheritdoc />
    public string? Read()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        try
        {
            _cached = File.ReadAllText(ScriptPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return _cached;
    }
}
