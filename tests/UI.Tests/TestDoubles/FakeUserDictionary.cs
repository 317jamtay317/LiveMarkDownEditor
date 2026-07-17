using UI.Spelling;

namespace UI.Tests.TestDoubles;

/// <summary>In-memory <see cref="IUserDictionary"/> for tests, backed by a case-insensitive set.</summary>
public sealed class FakeUserDictionary : IUserDictionary
{
    private readonly HashSet<string> _words;

    /// <summary>Creates a User Dictionary already holding the given accepted words.</summary>
    public FakeUserDictionary(params string[] words) => _words = new(words, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool Contains(string word) => _words.Contains(word);

    /// <inheritdoc />
    public void Add(string word) => _words.Add(word);
}
