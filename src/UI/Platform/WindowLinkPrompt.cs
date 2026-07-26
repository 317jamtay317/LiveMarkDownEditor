using System.Windows;
using UI.Core;
using UI.ViewModels;
using UI.Views;

namespace UI.Platform;

/// <summary>
/// Realises the Link Prompt as a modal <see cref="LinkPromptWindow"/> over the active window. It is
/// the WPF adapter behind <see cref="ILinkPrompt"/>: keeping it behind the port is what lets Insert
/// Link, Insert Image, and Insert Video (INV-030/069) be tested headlessly.
/// </summary>
public sealed class WindowLinkPrompt : ILinkPrompt
{
    /// <inheritdoc />
    public LinkDetails? AskForLink(string proposedText) => Ask(LinkPromptKind.Link, proposedText);

    /// <inheritdoc />
    public LinkDetails? AskForImage(string proposedAlt) => Ask(LinkPromptKind.Image, proposedAlt);

    /// <inheritdoc />
    public LinkDetails? AskForVideo(string proposedAlt) => Ask(LinkPromptKind.Video, proposedAlt);

    private static LinkDetails? Ask(LinkPromptKind kind, string proposedText)
    {
        var viewModel = new LinkPromptViewModel(kind, proposedText);
        var window = new LinkPromptWindow
        {
            DataContext = viewModel,
            // Fully qualified: "Application" alone binds to the Application layer's namespace.
            Owner = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(candidate => candidate.IsActive),
        };

        window.ShowDialog();

        // A dismissed Link Prompt answers null, and makes no edit (INV-030).
        return viewModel.Answer;
    }
}
