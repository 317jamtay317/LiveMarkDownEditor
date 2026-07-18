namespace UI.Spelling;

/// <summary>
/// An <see cref="ISpellDictionary"/> that consults the User Dictionary before an inner speller: a word
/// the user has accepted is never a Misspelling, whatever the speller thinks (INV-040). Spelling
/// Suggestions come from the inner speller unchanged.
/// </summary>
/// <param name="inner">The underlying speller (the operating system's).</param>
/// <param name="userDictionary">The User Dictionary of accepted words.</param>
public sealed class UserAwareSpellDictionary(ISpellDictionary inner, IUserDictionary userDictionary) : ISpellDictionary
{
    private readonly ISpellDictionary _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IUserDictionary _userDictionary =
        userDictionary ?? throw new ArgumentNullException(nameof(userDictionary));

    /// <inheritdoc />
    public bool IsMisspelled(string word) => !_userDictionary.Contains(word) && _inner.IsMisspelled(word);

    /// <inheritdoc />
    public IReadOnlyList<string> Suggest(string word) => _inner.Suggest(word);
}
