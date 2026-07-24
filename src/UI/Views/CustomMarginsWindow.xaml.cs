namespace UI.Views;

/// <summary>
/// The Custom Margins Prompt dialog: the four Print Margins in inches. All of its state and behaviour
/// lives in <see cref="ViewModels.CustomMarginsViewModel"/>; it closes itself through the
/// <see cref="Controls.DialogCloser"/> attached property, so this View keeps no code-behind (INV-061).
/// </summary>
public partial class CustomMarginsWindow
{
    /// <summary>Creates the Custom Margins Prompt dialog.</summary>
    public CustomMarginsWindow() => InitializeComponent();
}
