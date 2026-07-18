namespace Application;

/// <summary>
/// Port for enumerating the Markdown Documents beneath a folder, so the Folder Workspace can be built
/// without the Domain or ViewModels touching the file system. The Application layer owns the contract;
/// an adapter in the Infrastructure layer implements it (INV-042). It is asynchronous because reading a
/// large folder tree is I/O that must not block the UI thread.
/// </summary>
public interface IMarkdownFolderReader
{
    /// <summary>
    /// Enumerates the Markdown Documents beneath <paramref name="rootPath"/>, returning each as a
    /// root-relative, <c>/</c>-separated path (the input contract of <c>FolderWorkspace.From</c>).
    /// Non-Markdown files, and unreadable or hidden locations, are omitted rather than raised.
    /// </summary>
    /// <param name="rootPath">The absolute path of the root folder to enumerate.</param>
    /// <param name="cancellationToken">Cancels a long enumeration.</param>
    /// <returns>The root-relative, <c>/</c>-separated Markdown paths beneath the root.</returns>
    Task<IReadOnlyList<string>> EnumerateMarkdownFilesAsync(string rootPath, CancellationToken cancellationToken = default);
}
