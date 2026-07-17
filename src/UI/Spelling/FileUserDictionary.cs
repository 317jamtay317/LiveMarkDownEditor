using System.IO;

namespace UI.Spelling;

/// <summary>
/// An <see cref="IUserDictionary"/> backed by a text file — one accepted word per line, compared
/// case-insensitively. Accepting a word appends it to the file so it holds across runs (INV-040). A
/// missing or unreadable file is treated as an empty User Dictionary, and a failure to persist an
/// accepted word never disrupts editing.
/// </summary>
public sealed class FileUserDictionary : IUserDictionary
{
    private readonly string _path;
    private readonly HashSet<string> _words;

    /// <summary>Creates a User Dictionary backed by the file at the given path, loading any existing words.</summary>
    /// <param name="path">The absolute path of the user-dictionary text file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or blank.</exception>
    public FileUserDictionary(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _words = Load(path);
    }

    /// <inheritdoc />
    public bool Contains(string word) => !string.IsNullOrWhiteSpace(word) && _words.Contains(word.Trim());

    /// <inheritdoc />
    public void Add(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        var trimmed = word.Trim();
        if (!_words.Add(trimmed))
        {
            return; // already accepted
        }

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_path, trimmed + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Failing to persist an accepted word must not disrupt editing; it still holds this run.
        }
    }

    private static HashSet<string> Load(string path)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0)
                    {
                        words.Add(trimmed);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable file is an empty User Dictionary.
        }

        return words;
    }
}
