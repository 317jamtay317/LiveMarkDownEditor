namespace UI.Views;

/// <summary>
/// The Link Prompt dialog: asks for a Link's text and destination URL, or an Image's alt text and
/// source URL. All of its state and behaviour lives in <see cref="ViewModels.LinkPromptViewModel"/>;
/// it closes itself through the <see cref="Controls.DialogCloser"/> attached property, so this View
/// keeps no code-behind.
/// </summary>
public partial class LinkPromptWindow
{
    /// <summary>Creates the Link Prompt dialog.</summary>
    public LinkPromptWindow() => InitializeComponent();
}
