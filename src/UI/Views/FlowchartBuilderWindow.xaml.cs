namespace UI.Views;

/// <summary>
/// The Flowchart Builder dialog: a drag-and-drop canvas of Diagram Nodes and Edges beside a live
/// Diagram Preview. All of its state and behaviour lives in
/// <see cref="ViewModels.FlowchartBuilderViewModel"/>; it closes itself through the
/// <see cref="Controls.DialogCloser"/> attached property, so this View keeps no code-behind (INV-053).
/// </summary>
public partial class FlowchartBuilderWindow
{
    /// <summary>Creates the Flowchart Builder dialog.</summary>
    public FlowchartBuilderWindow() => InitializeComponent();
}
