namespace UI.Core;

/// <summary>
/// Abstraction over the platform's folder-picker dialog, so ViewModels can request a Folder Workspace
/// root without depending on WPF dialog types (keeping them unit-testable). The file counterpart is
/// <see cref="IFilePicker"/>.
/// </summary>
public interface IFolderPicker
{
    /// <summary>Prompts the user to choose a folder to open as a Folder Workspace.</summary>
    /// <returns>The chosen folder's absolute path, or <see langword="null"/> if the user cancelled.</returns>
    string? PickFolder();
}
