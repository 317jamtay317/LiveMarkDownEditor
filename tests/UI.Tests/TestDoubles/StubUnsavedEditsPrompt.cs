using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Scriptable <see cref="IUnsavedEditsPrompt"/> for tests: returns a pre-set decision and records how
/// many times, and about which documents, it was asked. A bulk close (Close All Tabs, INV-072) asks
/// about each Tab in turn, so <see cref="DecisionFor"/> can vary the answer per document — which is
/// how a test scripts Cancel on one Tab alone.
/// </summary>
public sealed class StubUnsavedEditsPrompt : IUnsavedEditsPrompt
{
    private readonly List<string> _documentNames = [];

    /// <summary>The decision returned by <see cref="Confirm"/> when <see cref="DecisionFor"/> is unset.</summary>
    public UnsavedEditsDecision Decision { get; set; } = UnsavedEditsDecision.Discard;

    /// <summary>
    /// Decides per document name, taking precedence over <see cref="Decision"/> when set. Lets a test
    /// answer Cancel for one Tab of a bulk close and Discard for the rest (INV-072).
    /// </summary>
    public Func<string, UnsavedEditsDecision>? DecisionFor { get; set; }

    /// <summary>The number of times <see cref="Confirm"/> has been called.</summary>
    public int ConfirmCount { get; private set; }

    /// <summary>The document name passed to the most recent <see cref="Confirm"/> call, or <see langword="null"/>.</summary>
    public string? LastDocumentName { get; private set; }

    /// <summary>Every document name asked about, in the order they were asked.</summary>
    public IReadOnlyList<string> DocumentNames => _documentNames;

    /// <inheritdoc />
    public UnsavedEditsDecision Confirm(string documentName)
    {
        ConfirmCount++;
        LastDocumentName = documentName;
        _documentNames.Add(documentName);
        return DecisionFor?.Invoke(documentName) ?? Decision;
    }
}
