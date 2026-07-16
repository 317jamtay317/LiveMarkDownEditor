namespace UI.Core;

/// <summary>
/// Abstraction over the Round-Trip — a Project immediately followed by a Capture — so a ViewModel can
/// obtain the Canonical Markdown of some source text without depending on WPF.
/// </summary>
/// <remarks>
/// A Round-Trip is realised over a Visual Document (a WPF <c>FlowDocument</c>), which must be built
/// and read on an STA thread. This port keeps that requirement, and the whole WYSIWYG projection,
/// out of the ViewModels that only need the canonical form of some Markdown (INV-025).
/// </remarks>
public interface IMarkdownRoundTrip
{
    /// <summary>Round-Trips source text, yielding its Canonical Markdown.</summary>
    /// <param name="markdown">The Markdown source text to Round-Trip.</param>
    /// <returns>
    /// The Canonical Markdown Capture emits for <paramref name="markdown"/>. Round-Tripping text that
    /// is already canonical returns it unchanged (INV-005).
    /// </returns>
    string RoundTrip(string markdown);
}
