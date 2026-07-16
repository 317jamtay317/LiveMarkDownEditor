using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// An <see cref="ILinkPrompt"/> that answers with whatever it was handed, and records what it was
/// asked — so Insert Link and Insert Image (INV-030) can be driven headlessly, including the case
/// where the Link Prompt is dismissed (a <see langword="null"/> answer).
/// </summary>
/// <param name="answer">The answer to give, or <see langword="null"/> to act as if dismissed.</param>
internal sealed class StubLinkPrompt(LinkDetails? answer) : ILinkPrompt
{
    /// <summary>The proposed text the Link Prompt was last asked with.</summary>
    internal string? ProposedText { get; private set; }

    /// <summary>How many times the Link Prompt was asked.</summary>
    internal int TimesAsked { get; private set; }

    /// <inheritdoc />
    public LinkDetails? AskForLink(string proposedText) => Record(proposedText);

    /// <inheritdoc />
    public LinkDetails? AskForImage(string proposedAlt) => Record(proposedAlt);

    private LinkDetails? Record(string proposedText)
    {
        ProposedText = proposedText;
        TimesAsked++;
        return answer;
    }
}
