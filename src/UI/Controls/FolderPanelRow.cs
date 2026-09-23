using System.Collections.ObjectModel;
using Domain;
using UI.Core;

namespace UI.Controls;

/// <summary>
/// A Folder Panel Row: one <see cref="FolderEntry"/> as the Folder Panel shows it, together with the
/// panel-only <see cref="IsExpanded"/> state. The panel keeps its rows when the Folder Tree is
/// rebuilt and changes only the rows whose entries were added or removed, so every other row keeps its
/// Expanded state, its highlight and its place on screen (INV-044). Expanding or Collapsing a row is
/// view-only and changes no document (INV-043).
/// </summary>
public sealed class FolderPanelRow : ObservableObject
{
    private bool _isExpanded;
    private bool _isRenaming;
    private string _newName = string.Empty;

    /// <summary>Creates the row for a Folder Entry, with a row for each of its children, all Collapsed.</summary>
    /// <param name="entry">The Folder Entry the row shows.</param>
    internal FolderPanelRow(FolderEntry entry)
    {
        Entry = entry;
        foreach (var child in entry.Children)
        {
            Children.Add(new FolderPanelRow(child));
        }
    }

    /// <summary>The Folder Entry this row shows, as the latest rebuild of the Folder Tree has it.</summary>
    public FolderEntry Entry { get; private set; }

    /// <summary>Whether the row is a Folder or a File. It never changes, since it is part of what identifies the row.</summary>
    public FolderEntryKind Kind => Entry.Kind;

    /// <summary>The entry's display name, shown as the row's label.</summary>
    public string Name => Entry.Name;

    /// <summary>The rows nested under this one: a Folder's children, folders before files, each A–Z. Empty for a File.</summary>
    public ObservableCollection<FolderPanelRow> Children { get; } = [];

    /// <summary>
    /// Whether this row's Folder is Expanded, showing its <see cref="Children"/>. Panel-only view state:
    /// it is kept across rebuilds of the Folder Tree (INV-044) and never changes a document (INV-043).
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    /// <summary>
    /// Whether this File row is showing its name editor for Rename File (INV-082), in place of its
    /// name. Panel-only view state: editing a name changes nothing until the New Name is committed.
    /// </summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set => Set(ref _isRenaming, value);
    }

    /// <summary>
    /// The New Name being typed into the row's name editor for Rename File (INV-082), exactly as typed.
    /// It starts as the File's current name each time editing starts.
    /// </summary>
    public string NewName
    {
        get => _newName;
        set => Set(ref _newName, value);
    }

    /// <summary>The row's name, which is what UI Automation and screen readers announce for it.</summary>
    /// <returns>The entry's display name.</returns>
    public override string ToString() => Name;

    /// <summary>
    /// Brings <paramref name="rows"/> in line with a rebuilt level of the Folder Tree. A row whose entry
    /// has gone is removed. A row whose entry is still there is kept and updated in place, together with
    /// the rows beneath it. A new entry gets a new row in its place. A row is identified by its entry's
    /// kind and relative path.
    /// </summary>
    /// <param name="rows">The rows of one level of the panel, updated in place.</param>
    /// <param name="entries">The same level of the rebuilt Folder Tree, in its order.</param>
    internal static void Sync(ObservableCollection<FolderPanelRow> rows, IReadOnlyList<FolderEntry> entries)
    {
        var present = entries.Select(KeyOf).ToHashSet();
        for (var index = rows.Count - 1; index >= 0; index--)
        {
            if (!present.Contains(KeyOf(rows[index].Entry)))
            {
                rows.RemoveAt(index);
            }
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var key = KeyOf(entries[index]);
            var existing = IndexOf(rows, key, from: index);
            if (existing < 0)
            {
                rows.Insert(index, new FolderPanelRow(entries[index]));
                continue;
            }

            // The Folder Tree's order is deterministic (INV-042), so a surviving row is normally already
            // in place. Moving it, rather than re-creating it, keeps its state if it ever is not.
            if (existing != index)
            {
                rows.Move(existing, index);
            }

            rows[index].Update(entries[index]);
        }
    }

    private void Update(FolderEntry entry)
    {
        Entry = entry;
        Sync(Children, entry.Children);
    }

    private static int IndexOf(ObservableCollection<FolderPanelRow> rows, (FolderEntryKind, string) key, int from)
    {
        for (var index = from; index < rows.Count; index++)
        {
            if (KeyOf(rows[index].Entry) == key)
            {
                return index;
            }
        }

        return -1;
    }

    private static (FolderEntryKind Kind, string RelativePath) KeyOf(FolderEntry entry) =>
        (entry.Kind, entry.RelativePath);
}
