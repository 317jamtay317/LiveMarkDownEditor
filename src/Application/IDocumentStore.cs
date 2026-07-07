using Domain;

namespace Application;

/// <summary>
/// Port for persisting and retrieving a <see cref="MarkdownDocument"/> to and from its Watched File.
/// The Application layer owns this contract; an adapter in the Infrastructure layer implements it
/// against the file system.
/// </summary>
public interface IDocumentStore
{
    /// <summary>Loads the Markdown Document stored at the given Watched File path.</summary>
    /// <param name="path">The absolute path of the Watched File.</param>
    /// <param name="cancellationToken">Token to cancel the load.</param>
    /// <returns>The Markdown Document read from disk.</returns>
    Task<MarkdownDocument> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Saves the given Markdown Document to the Watched File path.</summary>
    /// <param name="path">The absolute path of the Watched File.</param>
    /// <param name="document">The Markdown Document to persist.</param>
    /// <param name="cancellationToken">Token to cancel the save.</param>
    Task SaveAsync(string path, MarkdownDocument document, CancellationToken cancellationToken = default);
}
