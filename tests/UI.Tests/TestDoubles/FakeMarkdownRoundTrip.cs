using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// A stand-in for the Project/Capture Round-Trip, so ViewModel tests can exercise Canonical Markdown
/// (INV-025) without a WPF <c>FlowDocument</c> and the STA thread the real Round-Trip demands.
/// Round-Trips to the source text unchanged until <see cref="Canonicalise"/> supplies a rule.
/// </summary>
internal sealed class FakeMarkdownRoundTrip : IMarkdownRoundTrip
{
    /// <summary>
    /// The stand-in for Capture's normalisation: maps source text to its Canonical Markdown.
    /// Defaults to the identity, which models Markdown that is already canonical.
    /// </summary>
    public Func<string, string> Canonicalise { get; set; } = markdown => markdown;

    /// <inheritdoc />
    public string RoundTrip(string markdown) => Canonicalise(markdown);
}
