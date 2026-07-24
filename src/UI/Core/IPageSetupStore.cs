namespace UI.Core;

/// <summary>
/// Port for persisting and restoring the one editor-wide Page Setup across runs (INV-061). The UI
/// owns the contract; an adapter implements it (a JSON file in per-user application data).
/// </summary>
public interface IPageSetupStore
{
    /// <summary>
    /// Loads the last-persisted Page Setup. A first run, or an unreadable or invalid file, loads as
    /// <see cref="PageSetup.Default"/> — restoring must never stop the app from starting.
    /// </summary>
    /// <returns>The persisted Page Setup, or <see cref="PageSetup.Default"/>.</returns>
    PageSetup Load();

    /// <summary>Persists the given Page Setup, replacing any previously saved one.</summary>
    /// <param name="setup">The Page Setup to persist.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    Task SaveAsync(PageSetup setup, CancellationToken cancellationToken = default);
}
