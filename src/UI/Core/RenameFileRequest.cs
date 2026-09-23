using Domain;

namespace UI.Core;

/// <summary>
/// A Rename File the user has committed in the Folder Panel: the File, and the New Name typed for it
/// (INV-082). It carries the two together as the one parameter a command takes; checking the New Name
/// is the Folder Workspace's job, not this record's.
/// </summary>
/// <param name="File">The File to rename, as the Folder Tree held it when the user started editing.</param>
/// <param name="NewName">The New Name exactly as typed, before it is tidied.</param>
public sealed record RenameFileRequest(FolderEntry File, string NewName);
