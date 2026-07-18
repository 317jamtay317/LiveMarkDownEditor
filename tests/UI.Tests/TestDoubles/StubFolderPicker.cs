using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Scriptable <see cref="IFolderPicker"/> for tests: returns a pre-set folder path
/// (<see langword="null"/> simulates the user cancelling).
/// </summary>
public sealed class StubFolderPicker : IFolderPicker
{
    /// <summary>The path returned by <see cref="PickFolder"/>.</summary>
    public string? FolderResult { get; set; }

    /// <inheritdoc />
    public string? PickFolder() => FolderResult;
}
