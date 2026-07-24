namespace UI.Views;

/// <summary>
/// The Print Preview window: the document's printed pages on a scrolling canvas, with Print beside
/// them. All of its state and behaviour lives in <see cref="ViewModels.PrintPreviewViewModel"/>, so
/// this View keeps no code-behind (INV-061).
/// </summary>
public partial class PrintPreviewWindow
{
    /// <summary>Creates the Print Preview window.</summary>
    public PrintPreviewWindow() => InitializeComponent();
}
