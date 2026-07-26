using System.Windows;
using UI.Core;
using UI.ViewModels;
using UI.Views;

namespace UI.Platform;

/// <summary>
/// Realises the Diagram Builder as a modal <see cref="DiagramBuilderWindow"/> over the active
/// window. It is the WPF adapter behind <see cref="IDiagramBuilder"/>: keeping the window behind the
/// port is what lets Open Diagram Builder (INV-053) be tested headlessly, exactly as
/// <see cref="WindowLinkPrompt"/> does for Insert Link (INV-030). It follows the app theme so the
/// builder's live Diagram Preview matches the editor.
/// </summary>
/// <param name="appearance">The visual-theme ViewModel, read for the current light/dark theme.</param>
public sealed class WindowDiagramBuilder(AppearanceViewModel appearance) : IDiagramBuilder
{
    private readonly AppearanceViewModel _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));

    /// <inheritdoc />
    public string? Build(string? existingSource)
    {
        var viewModel = new DiagramBuilderViewModel(existingSource, _appearance.IsDarkTheme);
        var window = new DiagramBuilderWindow
        {
            DataContext = viewModel,
            // Fully qualified: "Application" alone binds to the Application layer's namespace.
            Owner = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(candidate => candidate.IsActive),
        };

        window.ShowDialog();

        // A cancelled builder yields null, and makes no edit (INV-053).
        return viewModel.Result;
    }
}
