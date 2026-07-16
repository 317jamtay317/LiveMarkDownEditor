using UI.Core;

namespace UI.Wysiwyg;

/// <summary>
/// Realises the Round-Trip port over the WYSIWYG projection: a Project into a Visual Document
/// immediately followed by a Capture back out of it, which yields the source text's Canonical
/// Markdown (INV-005, INV-025).
/// </summary>
/// <remarks>
/// Builds a <see cref="System.Windows.Documents.FlowDocument"/>, so it must be called on the STA
/// thread that owns the Visual Document — in the running app, the UI thread.
/// </remarks>
public sealed class FlowDocumentRoundTrip : IMarkdownRoundTrip
{
    private readonly MarkdownToFlowDocumentProjector _projector = new();
    private readonly FlowDocumentToMarkdownCapturer _capturer = new();

    /// <inheritdoc />
    public string RoundTrip(string markdown) => _capturer.Capture(_projector.Project(markdown));
}
