using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Scriptable <see cref="IFilePicker"/> for tests: returns pre-set paths for open/save prompts
/// (<see langword="null"/> simulates the user cancelling).
/// </summary>
public sealed class StubFilePicker : IFilePicker
{
    /// <summary>The path returned by <see cref="PickOpen"/>.</summary>
    public string? OpenResult { get; set; }

    /// <summary>The path returned by <see cref="PickSave"/>.</summary>
    public string? SaveResult { get; set; }

    /// <inheritdoc />
    public string? PickOpen() => OpenResult;

    /// <inheritdoc />
    public string? PickSave(string? suggestedFileName) => SaveResult;
}
