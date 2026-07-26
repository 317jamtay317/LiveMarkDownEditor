namespace UI.Views;

/// <summary>
/// The Diagram Builder dialog: a drag-and-drop canvas of Diagram Nodes and Edges beside a live
/// Diagram Preview. All of its state and behaviour lives in
/// <see cref="ViewModels.DiagramBuilderViewModel"/>; it closes itself through the
/// <see cref="Controls.DialogCloser"/> attached property, so this View keeps no code-behind (INV-053).
/// </summary>
public partial class DiagramBuilderWindow
{
    /// <summary>Creates the Diagram Builder dialog.</summary>
    public DiagramBuilderWindow() => InitializeComponent();
}
