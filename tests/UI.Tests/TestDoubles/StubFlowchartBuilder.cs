using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// An <see cref="IFlowchartBuilder"/> that returns whatever it was handed and records the existing
/// source it was asked with — so Open Flowchart Builder (INV-053) can be driven headlessly, including
/// the Cancel case (a <see langword="null"/> result).
/// </summary>
/// <param name="result">The Mermaid source to return, or <see langword="null"/> to act as if cancelled.</param>
internal sealed class StubFlowchartBuilder(string? result) : IFlowchartBuilder
{
    /// <summary>The existing source the builder was last asked with.</summary>
    internal string? ReceivedExistingSource { get; private set; }

    /// <summary>How many times the builder was asked.</summary>
    internal int TimesAsked { get; private set; }

    /// <inheritdoc />
    public string? Build(string? existingSource)
    {
        ReceivedExistingSource = existingSource;
        TimesAsked++;
        return result;
    }
}
