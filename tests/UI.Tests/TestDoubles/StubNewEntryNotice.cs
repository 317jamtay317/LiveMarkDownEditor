using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Recording <see cref="INewEntryNotice"/> for tests: keeps every reason it was asked to explain, and the
/// heading each came with (INV-085, INV-086).
/// </summary>
public sealed class StubNewEntryNotice : INewEntryNotice
{
    private readonly List<string> _reasons = [];
    private readonly List<string> _headings = [];

    /// <summary>Every reason explained to the user, in order.</summary>
    public IReadOnlyList<string> Reasons => _reasons;

    /// <summary>The heading each reason was explained under, in the same order.</summary>
    public IReadOnlyList<string> Headings => _headings;

    /// <inheritdoc />
    public void Explain(string heading, string reason)
    {
        _headings.Add(heading);
        _reasons.Add(reason);
    }
}
