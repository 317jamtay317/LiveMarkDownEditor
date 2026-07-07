using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="IUiDispatcher"/> that runs posted work inline (synchronously), so
/// External Change handling completes deterministically within a test.
/// </summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action) => action();
}
