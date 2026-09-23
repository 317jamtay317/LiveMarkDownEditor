using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Recording <see cref="IRenameFileNotice"/> for tests: keeps every reason it was asked to explain
/// (INV-082).
/// </summary>
public sealed class StubRenameFileNotice : IRenameFileNotice
{
    private readonly List<string> _reasons = [];

    /// <summary>Every reason explained to the user, in order.</summary>
    public IReadOnlyList<string> Reasons => _reasons;

    /// <inheritdoc />
    public void Explain(string reason) => _reasons.Add(reason);
}
