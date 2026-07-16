using System.Windows;
using UI.Core;
using UI.ViewModels;
using UI.Views;

namespace UI.Platform;

/// <summary>
/// Realises the Link Prompt as a modal <see cref="LinkPromptWindow"/> over the active window. It is
/// the WPF adapter behind <see cref="ILinkPrompt"/>: keeping it behind the port is what lets Insert
/// Link and Insert Image (INV-030) be tested headlessly.
/// </summary>
public sealed class WindowLinkPrompt : ILinkPrompt
{
    /// <inheritdoc />
    public LinkDetails? AskForLink(string proposedText) => Ask(forImage: false, proposedText);

    /// <inheritdoc />
    public LinkDetails? AskForImage(string proposedAlt) => Ask(forImage: true, proposedAlt);

    private static LinkDetails? Ask(bool forImage, string proposedText)
    {
        var viewModel = new LinkPromptViewModel(forImage, proposedText);
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
