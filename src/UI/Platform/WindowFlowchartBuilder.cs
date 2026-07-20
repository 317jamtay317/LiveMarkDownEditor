using System.Windows;
using UI.Core;
using UI.ViewModels;
using UI.Views;

namespace UI.Platform;

/// <summary>
/// Realises the Flowchart Builder as a modal <see cref="FlowchartBuilderWindow"/> over the active
/// window. It is the WPF adapter behind <see cref="IFlowchartBuilder"/>: keeping the window behind the
/// port is what lets Open Flowchart Builder (INV-053) be tested headlessly, exactly as
/// <see cref="WindowLinkPrompt"/> does for Insert Link (INV-030). It follows the app theme so the
/// builder's live Diagram Preview matches the editor.
/// </summary>
/// <param name="appearance">The visual-theme ViewModel, read for the current light/dark theme.</param>
public sealed class WindowFlowchartBuilder(AppearanceViewModel appearance) : IFlowchartBuilder
{
    private readonly AppearanceViewModel _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));

    /// <inheritdoc />
    public string? Build(string? existingSource)
    {
        var viewModel = new FlowchartBuilderViewModel(existingSource, _appearance.IsDarkTheme);
        var window = new FlowchartBuilderWindow
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
