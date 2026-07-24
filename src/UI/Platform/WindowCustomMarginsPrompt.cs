using System.Windows;
using UI.Core;
using UI.ViewModels;
using UI.Views;

namespace UI.Platform;

/// <summary>
/// Realises the Custom Margins Prompt as a modal <see cref="CustomMarginsWindow"/> over the active
/// window. It is the WPF adapter behind <see cref="ICustomMarginsPrompt"/>: keeping the window behind
/// the port is what lets the margin rules (INV-061) be tested headlessly, exactly as
/// <see cref="WindowLinkPrompt"/> does for Insert Link (INV-030).
/// </summary>
public sealed class WindowCustomMarginsPrompt : ICustomMarginsPrompt
{
    /// <inheritdoc />
    public PrintMargins? Ask(PrintMargins current)
    {
        var viewModel = new CustomMarginsViewModel(current);
        var window = new CustomMarginsWindow
        {
            DataContext = viewModel,
            // Fully qualified: "Application" alone binds to the Application layer's namespace.
            Owner = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(candidate => candidate.IsActive),
        };

        window.ShowDialog();

        // A dismissed prompt answers null, and changes nothing (INV-061).
        return viewModel.Answer;
    }
}
