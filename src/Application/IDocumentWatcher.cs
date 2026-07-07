namespace Application;

/// <summary>
/// Port for monitoring a Watched File for External Change. The Application layer owns this contract;
/// an adapter in the Infrastructure layer implements it against the file system.
/// </summary>
public interface IDocumentWatcher
{
    /// <summary>Raised when the Watched File changes on disk outside the Editor Session.</summary>
    event EventHandler<ExternalChange>? Changed;

    /// <summary>Begins watching the file at <paramref name="path"/>, replacing any prior watch.</summary>
    /// <param name="path">The absolute path of the Watched File.</param>
    void Watch(string path);

    /// <summary>Stops watching the current Watched File, if any.</summary>
    void StopWatching();
}
