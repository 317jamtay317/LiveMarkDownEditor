using Domain;

namespace UI.Core;

/// <summary>
/// A New File or New Folder the user has committed in the Folder Panel: the Selected Folder Entry when it
/// began, which names the Save Folder it goes in, and the Entry Name typed for it (INV-085, INV-086). It
/// carries the two together as the one parameter a command takes; checking the Entry Name is the Folder
/// Workspace's job, not this record's.
/// </summary>
/// <param name="Selected">The Selected Folder Entry when the action began, or <see langword="null"/> for none.</param>
/// <param name="EntryName">The Entry Name exactly as typed, before it is tidied.</param>
public sealed record NewEntryRequest(FolderEntry? Selected, string EntryName);
