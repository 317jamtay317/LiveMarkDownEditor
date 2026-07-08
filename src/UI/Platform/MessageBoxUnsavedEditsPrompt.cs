using System.Windows;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// WPF adapter for <see cref="IUnsavedEditsPrompt"/> that asks the user, with a modal message box,
/// whether to save, discard, or cancel closing a document that has unsaved edits.
/// </summary>
public sealed class MessageBoxUnsavedEditsPrompt : IUnsavedEditsPrompt
{
    /// <inheritdoc />
    public UnsavedEditsDecision Confirm(string documentName)
    {
        var result = MessageBox.Show(
            $"Save changes to “{documentName}” before closing?",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => UnsavedEditsDecision.Save,
            MessageBoxResult.No => UnsavedEditsDecision.Discard,
            _ => UnsavedEditsDecision.Cancel,
        };
    }
}
