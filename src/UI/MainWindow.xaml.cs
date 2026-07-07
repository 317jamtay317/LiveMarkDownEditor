using System.Windows;

namespace UI;

/// <summary>
/// The application's main window. Per the project's MVVM rules the code-behind contains only the
/// designer-generated <see cref="InitializeComponent"/> call; all state and behaviour live in the
/// bound <see cref="ViewModels.EditorSessionViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Initialises the window's XAML-defined content.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
