using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Scriptable <see cref="IDeleteFilePrompt"/> for tests: answers with a pre-set <see cref="Answer"/>
/// and records which Files it was asked about (INV-081).
/// </summary>
public sealed class StubDeleteFilePrompt : IDeleteFilePrompt
{
    private readonly List<string> _fileNames = [];

    /// <summary>The answer <see cref="Confirm"/> gives: <see langword="true"/> for Yes, <see langword="false"/> for No.</summary>
    public bool Answer { get; set; } = true;

    /// <summary>Every File name asked about, in the order they were asked.</summary>
    public IReadOnlyList<string> FileNames => _fileNames;

    /// <inheritdoc />
    public bool Confirm(string fileName)
    {
        _fileNames.Add(fileName);
        return Answer;
    }
}
