using UI.Spelling;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Scriptable <see cref="ISpellDictionary"/> for tests: the words given to the constructor are the
/// misspelled ones, and <see cref="Suggestions"/> is what it offers for any word.
/// </summary>
public sealed class StubSpellDictionary(params string[] misspelled) : ISpellDictionary
{
    private readonly HashSet<string> _misspelled = new(misspelled, StringComparer.OrdinalIgnoreCase);

    /// <summary>The Spelling Suggestions this stub returns for any word.</summary>
    public IReadOnlyList<string> Suggestions { get; set; } = [];

    /// <inheritdoc />
    public bool IsMisspelled(string word) => _misspelled.Contains(word);

    /// <inheritdoc />
    public IReadOnlyList<string> Suggest(string word) => Suggestions;
}
