using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IPageSetupStore"/>: seedable with a stored Page Setup and recording every
/// save, so tests can assert what the Workspace persisted (INV-061).
/// </summary>
public sealed class FakePageSetupStore : IPageSetupStore
{
    /// <summary>The Page Setup the store holds — seed it to simulate a previous run's persisted setup.</summary>
    public PageSetup Stored { get; set; } = PageSetup.Default;

    /// <summary>Every Page Setup saved, in order.</summary>
    public List<PageSetup> Saved { get; } = [];

    /// <inheritdoc />
    public PageSetup Load() => Stored;

    /// <inheritdoc />
    public Task SaveAsync(PageSetup setup, CancellationToken cancellationToken = default)
    {
        Saved.Add(setup);
        Stored = setup;
        return Task.CompletedTask;
    }
}
