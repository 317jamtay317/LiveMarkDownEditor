using System.Text.Json;
using Application;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IWorkspaceStateStore"/>. Persists the Workspace State as JSON
/// at a per-user path. A missing, corrupt, or unreadable file loads as
/// <see cref="WorkspaceState.Empty"/>, so a first run or a damaged file simply starts clean (INV-037).
/// </summary>
public sealed class JsonWorkspaceStateStore : IWorkspaceStateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    /// <summary>Creates a store that reads and writes the Workspace State at the given file path.</summary>
    /// <param name="path">The absolute path of the JSON state file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or blank.</exception>
    public JsonWorkspaceStateStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <inheritdoc />
    public WorkspaceState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return WorkspaceState.Empty;
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<WorkspaceState>(json, Options) ?? WorkspaceState.Empty;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable state file must never stop the app from starting.
            return WorkspaceState.Empty;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, Options);
        await File.WriteAllTextAsync(_path, json, cancellationToken).ConfigureAwait(false);
    }
}
