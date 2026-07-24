using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Canned <see cref="ICustomMarginsPrompt"/>: answers every ask with the margins it was built with —
/// <see langword="null"/> simulating the user dismissing the Custom Margins Prompt (INV-061).
/// </summary>
/// <param name="answer">The margins to answer with, or <see langword="null"/> to dismiss.</param>
public sealed class StubCustomMarginsPrompt(PrintMargins? answer) : ICustomMarginsPrompt
{
    /// <summary>The margins the prompt was last seeded with, or <see langword="null"/> when never asked.</summary>
    public PrintMargins? LastSeeded { get; private set; }

    /// <inheritdoc />
    public PrintMargins? Ask(PrintMargins current)
    {
        LastSeeded = current;
        return answer;
    }
}
