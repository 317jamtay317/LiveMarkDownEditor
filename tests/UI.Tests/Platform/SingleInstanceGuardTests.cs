using Shouldly;
using UI.Platform;
using Xunit;

namespace UI.Tests.Platform;

/// <summary>
/// Tests for <see cref="SingleInstanceGuard"/> — the Single Instance rule (INV-020): the first
/// holder acquires the named instance and listens for forwarded Startup Document paths; a later
/// acquire fails, and its forwarded path reaches the holder.
/// </summary>
public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_WhenFirst_AcquiresTheInstance_INV020()
    {
        using var guard = SingleInstanceGuard.TryAcquire(UniqueName());

        guard.ShouldNotBeNull();
    }

    [Fact]
    public void TryAcquire_WhenAlreadyHeld_ReturnsNull_INV020()
    {
        var name = UniqueName();
        using var first = SingleInstanceGuard.TryAcquire(name);

        using var second = SingleInstanceGuard.TryAcquire(name);

        first.ShouldNotBeNull();
        second.ShouldBeNull();
    }

    [Fact]
    public void TryAcquire_AfterTheHolderReleases_AcquiresAgain_INV020()
    {
        var name = UniqueName();
        var first = SingleInstanceGuard.TryAcquire(name);
        first!.Dispose();

        using var second = SingleInstanceGuard.TryAcquire(name);

        second.ShouldNotBeNull();
    }

    [Fact]
    public async Task ForwardDocumentPath_DeliversThePathToTheHolder_INV020()
    {
        var name = UniqueName();
        using var holder = SingleInstanceGuard.TryAcquire(name);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        holder!.Listen(path => received.TrySetResult(path));

        var forwarded = SingleInstanceGuard.ForwardDocumentPath(name, @"C:\docs\note.md");

        forwarded.ShouldBeTrue();
        var path = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        path.ShouldBe(@"C:\docs\note.md");
    }

    [Fact]
    public void ForwardDocumentPath_WithNoHolder_ReturnsFalse_INV020()
    {
        SingleInstanceGuard.ForwardDocumentPath(UniqueName(), @"C:\docs\note.md", timeoutMilliseconds: 200)
            .ShouldBeFalse();
    }

    // Each test isolates itself behind its own instance name so parallel runs never collide.
    private static string UniqueName() => "LiveMarkDownEditor.Tests." + Guid.NewGuid().ToString("N");
}
