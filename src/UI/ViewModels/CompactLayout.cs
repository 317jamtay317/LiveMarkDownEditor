namespace UI.ViewModels;

/// <summary>The side panels the user has toggled on, before any width-driven collapse.</summary>
/// <param name="Dock">Whether the Side Dock (Folder / Navigation) is toggled on.</param>
/// <param name="Source">Whether the Source Panel is toggled on.</param>
/// <param name="Preview">Whether the Preview Panel is toggled on.</param>
public readonly record struct PanelIntent(bool Dock, bool Source, bool Preview);

/// <summary>The side panels that stay visible after Compact Layout has been applied (INV-059).</summary>
/// <param name="Dock">Whether the Side Dock is shown.</param>
/// <param name="Source">Whether the Source Panel is shown.</param>
/// <param name="Preview">Whether the Preview Panel is shown.</param>
public readonly record struct PanelVisibility(bool Dock, bool Source, bool Preview);

/// <summary>
/// The pure rule behind Compact Layout: given the width available to the editing row and the side panels
/// the user has toggled on, it decides which panels stay visible. When the row is too narrow to show the
/// Visual Document at its minimum width beside every toggled-on panel, panels are collapsed one at a time
/// — the Preview Panel first, then the Source Panel, then the Side Dock — until the editor keeps its
/// minimum. It has no state and does no I/O, so the same inputs always yield the same layout (INV-059).
/// It is the panel-layout counterpart of <see cref="UI.Controls.CommandBarPanel"/>'s overflow collapse.
/// </summary>
public static class CompactLayout
{
    /// <summary>The minimum width the Visual Document (the editing surface) is always left.</summary>
    public const double EditorMinWidth = 240;

    /// <summary>The width the Side Dock occupies when shown.</summary>
    public const double SideDockWidth = 260;

    /// <summary>The nominal width a right-side Panel Column (Source or Preview) occupies when shown.</summary>
    public const double SidePanelWidth = 420;

    /// <summary>
    /// Resolves which toggled-on panels stay visible at the given width, collapsing the Preview Panel,
    /// then the Source Panel, then the Side Dock until the editor keeps <see cref="EditorMinWidth"/>. A
    /// width of zero or less means the surface has not been measured yet, so every panel is left at its
    /// intent (INV-059).
    /// </summary>
    /// <param name="availableWidth">The width available to the editing row (dock, editor, and side panels together).</param>
    /// <param name="intent">The panels the user has toggled on.</param>
    /// <returns>The panels that remain visible after any width-driven collapse.</returns>
    public static PanelVisibility Resolve(double availableWidth, PanelIntent intent) =>
        Resolve(availableWidth, intent, editorIsDocked: true);

    /// <summary>
    /// Resolves which Docked panels stay visible at the given width, collapsing the Preview Panel, then
    /// the Source Panel, then the Side Dock until the primary Document Pane keeps
    /// <see cref="EditorMinWidth"/>. While the Editor Pane is not Docked the Source Panel is itself the
    /// primary Document Pane: it takes the editor's slot at the editor's minimum and is never
    /// width-collapsed (INV-063). A width of zero or less means the surface has not been measured yet,
    /// so every panel is left at its intent (INV-059).
    /// </summary>
    /// <param name="availableWidth">The width available to the editing row (dock, primary pane, and side panels together).</param>
    /// <param name="intent">The panels that are Docked (INV-062).</param>
    /// <param name="editorIsDocked">Whether the Editor Pane is Docked; when it is not, the Source Panel is the primary pane.</param>
    /// <returns>The panels that remain visible after any width-driven collapse.</returns>
    public static PanelVisibility Resolve(double availableWidth, PanelIntent intent, bool editorIsDocked)
    {
        if (availableWidth <= 0)
        {
            return new PanelVisibility(intent.Dock, intent.Source, intent.Preview);
        }

        var dock = intent.Dock;
        var source = intent.Source;
        var preview = intent.Preview;

        // The primary Document Pane — the editor, or the Source Panel standing in for it — always
        // reserves the editor's minimum. A non-primary Source Panel contributes its fixed panel width.
        double Required() => EditorMinWidth
            + (dock ? SideDockWidth : 0)
            + (editorIsDocked && source ? SidePanelWidth : 0)
            + (preview ? SidePanelWidth : 0);

        // Collapse lowest priority first — Preview, then Source, then the Side Dock — and only as far as
        // the row must shrink for the primary pane to keep its minimum width. The Source Panel is
        // skipped while it is the primary pane: the last Docked Document Pane is never collapsed
        // (INV-063).
        if (preview && Required() > availableWidth)
        {
            preview = false;
        }

        if (editorIsDocked && source && Required() > availableWidth)
        {
            source = false;
        }

        if (dock && Required() > availableWidth)
        {
            dock = false;
        }

        return new PanelVisibility(dock, source, preview);
    }
}
