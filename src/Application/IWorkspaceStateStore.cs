namespace Application;

/// <summary>
/// Port for persisting and restoring the <see cref="WorkspaceState"/> across runs. The Application
/// layer owns the contract; an Infrastructure adapter implements it (a JSON file in per-user
/// application data).
/// </summary>
public interface IWorkspaceStateStore
{
    /// <summary>
    /// Loads the last-persisted Workspace State. A first run, or an unreadable file, loads as
    /// <see cref="WorkspaceState.Empty"/> — restoring must never stop the app from starting.
    /// </summary>
    /// <returns>The persisted Workspace State, or <see cref="WorkspaceState.Empty"/>.</returns>
    WorkspaceState Load();

    /// <summary>Persists the given Workspace State, replacing any previously saved one.</summary>
    /// <param name="state">The Workspace State to persist.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken = default);
}
