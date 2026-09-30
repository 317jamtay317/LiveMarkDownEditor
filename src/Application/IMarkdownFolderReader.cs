using Domain;

namespace Application;

/// <summary>
/// Port for reading the Folder Listing beneath a folder, so the Folder Workspace can be built without
/// the Domain or ViewModels touching the file system. The Application layer owns the contract; an
/// adapter in the Infrastructure layer implements it (INV-042, INV-084). It is asynchronous because
/// reading a large folder tree is I/O that must not block the UI thread.
/// </summary>
public interface IMarkdownFolderReader
{
    /// <summary>
    /// Reads the Folder Listing beneath <paramref name="rootPath"/>: every folder except what lies in an
    /// Excluded Folder, the Markdown Documents, and which of them Git ignores — each as a root-relative,
    /// <c>/</c>-separated path (the input contract of <c>FolderWorkspace.From</c>). Unreadable locations
    /// are omitted rather than raised, and with no Git to ask, nothing is Ignored.
    /// </summary>
    /// <param name="rootPath">The absolute path of the root folder to read.</param>
    /// <param name="cancellationToken">Cancels a long read.</param>
    /// <returns>The Folder Listing beneath the root.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the root folder does not exist.</exception>
    Task<FolderListing> ReadListingAsync(string rootPath, CancellationToken cancellationToken = default);
}
