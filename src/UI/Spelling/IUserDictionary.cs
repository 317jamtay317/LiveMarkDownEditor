namespace UI.Spelling;

/// <summary>
/// The User Dictionary — the words the user has accepted through Add to Dictionary, which the
/// Dictionary treats as correctly spelled. Accepting a word persists it, so it holds across runs
/// (INV-040).
/// </summary>
public interface IUserDictionary
{
    /// <summary>Whether <paramref name="word"/> has been accepted into the User Dictionary.</summary>
    /// <param name="word">A single word (no surrounding punctuation).</param>
    /// <returns><see langword="true"/> if the word has been accepted; otherwise <see langword="false"/>.</returns>
    bool Contains(string word);

    /// <summary>Accepts <paramref name="word"/> into the User Dictionary, persisting it.</summary>
    /// <param name="word">The word to accept.</param>
    void Add(string word);
}
