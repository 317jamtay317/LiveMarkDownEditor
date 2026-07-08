using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Scriptable <see cref="IUnsavedEditsPrompt"/> for tests: returns a pre-set decision and records how
/// many times, and about which document, it was asked.
/// </summary>
public sealed class StubUnsavedEditsPrompt : IUnsavedEditsPrompt
{
    /// <summary>The decision returned by <see cref="Confirm"/>.</summary>
    public UnsavedEditsDecision Decision { get; set; } = UnsavedEditsDecision.Discard;

    /// <summary>The number of times <see cref="Confirm"/> has been called.</summary>
    public int ConfirmCount { get; private set; }

    /// <summary>The document name passed to the most recent <see cref="Confirm"/> call, or <see langword="null"/>.</summary>
    public string? LastDocumentName { get; private set; }

    /// <inheritdoc />
    public UnsavedEditsDecision Confirm(string documentName)
    {
        ConfirmCount++;
        LastDocumentName = documentName;
        return Decision;
    }
}
