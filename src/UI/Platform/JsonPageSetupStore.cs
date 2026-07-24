using System.IO;
using System.Text.Json;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// File-system adapter for <see cref="IPageSetupStore"/>. Persists the editor-wide Page Setup as JSON
/// at a per-user path. A missing, corrupt, or invalid file loads as <see cref="PageSetup.Default"/>,
/// so a first run or a damaged file simply starts at Portrait with Normal margins (INV-061).
/// </summary>
public sealed class JsonPageSetupStore : IPageSetupStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    /// <summary>Creates a store that reads and writes the Page Setup at the given file path.</summary>
    /// <param name="path">The absolute path of the JSON page-setup file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or blank.</exception>
    public JsonPageSetupStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <inheritdoc />
    public PageSetup Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return PageSetup.Default;
            }

            var stored = JsonSerializer.Deserialize<StoredPageSetup>(File.ReadAllText(_path), Options);
            if (stored is null)
            {
                return PageSetup.Default;
            }

            // Reconstructed through the guarded value objects, so a file whose values no Page Setup
            // may hold (a negative margin, an unknown orientation) falls back rather than crashing.
            return new PageSetup(
                Enum.Parse<PageOrientation>(stored.Orientation ?? string.Empty),
                new PrintMargins(stored.Left, stored.Top, stored.Right, stored.Bottom));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            return PageSetup.Default;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(PageSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stored = new StoredPageSetup(
            setup.Orientation.ToString(),
            setup.Margins.Left,
            setup.Margins.Top,
            setup.Margins.Right,
            setup.Margins.Bottom);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(stored, Options), cancellationToken)
            .ConfigureAwait(false);
    }

    // The persisted shape: primitive values only, so the guarded value objects stay the one place a
    // Page Setup can be judged valid.
    private sealed record StoredPageSetup(string? Orientation, double Left, double Top, double Right, double Bottom);
}
