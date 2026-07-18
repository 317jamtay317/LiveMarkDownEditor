using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IWorkspaceStateStore"/> for tests: returns a scripted state from Load and
/// records what SaveAsync was given, so a test can drive Restore and assert what was persisted.
/// </summary>
public sealed class FakeWorkspaceStateStore : IWorkspaceStateStore
{
    /// <summary>The state <see cref="Load"/> returns. Defaults to <see cref="WorkspaceState.Empty"/>.</summary>
    public WorkspaceState StateToLoad { get; set; } = WorkspaceState.Empty;

    /// <summary>The state last passed to <see cref="SaveAsync"/>, or <see langword="null"/> if never saved.</summary>
    public WorkspaceState? SavedState { get; private set; }

    /// <summary>How many times <see cref="SaveAsync"/> was called.</summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc />
    public WorkspaceState Load() => StateToLoad;

    /// <inheritdoc />
    public Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken = default)
    {
        SavedState = state;
        SaveCount++;
        return Task.CompletedTask;
    }
}
