using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace UI.Wysiwyg;

/// <summary>
/// The one shared definition of a Task Marker in the Visual Document, and the Toggle Task Marker
/// edit. A Task Marker is a <see cref="Run"/> carrying a <see cref="TaskMarkerRole"/> and the glyph
/// the user sees; the Projector composes one through <see cref="CreateMarker"/> and the List
/// Formatting Actions through the same method, so Capture treats a user-marked List Item and a
/// loaded one uniformly (INV-018).
/// </summary>
/// <remarks>
/// The marker owns the single space that separates its checkbox from the List Item's text, so the
/// glyph and the text are never run together and the separator is never doubled. Capture re-emits
/// that space from the marker itself (<c>"[ ] "</c>), which is why the Projector strips the leading
/// space the Markdown source carries on the following text.
/// </remarks>
internal static class TaskMarkerEditing
{
    /// <summary>The glyph shown for an unchecked Task Marker, with its separating space.</summary>
    internal const string UncheckedGlyph = "☐ ";

    /// <summary>The glyph shown for a checked Task Marker, with its separating space.</summary>
    internal const string CheckedGlyph = "☑ ";

    /// <summary>Composes a Task Marker, exactly as the Projector does.</summary>
    /// <param name="isChecked">Whether the task is checked.</param>
    /// <returns>The marker run, tagged with its <see cref="TaskMarkerRole"/>.</returns>
    internal static Run CreateMarker(bool isChecked) =>
        new(isChecked ? CheckedGlyph : UncheckedGlyph)
        {
            Tag = new TaskMarkerRole(isChecked),
            FontFamily = CheckboxFont,
        };

    /// <summary>
    /// The Task Marker at <paramref name="position"/>, or <see langword="null"/> when the position is
    /// not on one.
    /// </summary>
    /// <param name="position">The position to classify (typically where the user clicked).</param>
    /// <remarks>
    /// The position must sit <em>within</em> the marker's glyph, not merely at its edge: a pointer at
    /// the seam between the marker and the List Item's text reports the marker as its parent too, and
    /// treating that as a hit would flip the checkbox when the user clicked the text beside it.
    /// Requiring glyph text ahead of the position excludes the seam.
    /// </remarks>
    internal static Run? MarkerAt(TextPointer? position) =>
        position?.Parent is Run { Tag: TaskMarkerRole } marker
        && position.GetTextInRun(LogicalDirection.Forward).Length > 0
            ? marker
            : null;

    /// <summary>The Task Marker carried by <paramref name="paragraph"/>, or <see langword="null"/>.</summary>
    /// <param name="paragraph">The List Item paragraph to search.</param>
    internal static Run? MarkerOf(Paragraph? paragraph) =>
        paragraph?.Inlines.OfType<Run>().FirstOrDefault(run => run.Tag is TaskMarkerRole);

    /// <summary>
    /// The Toggle Task Marker edit: flips the Task Marker at <paramref name="position"/> between
    /// unchecked and checked, changing nothing else (INV-024). A no-op when the position is not on a
    /// Task Marker, so a click elsewhere places the caret as usual.
    /// </summary>
    /// <param name="editor">The editor holding the Task Marker.</param>
    /// <param name="position">The position clicked.</param>
    /// <returns><see langword="true"/> when a Task Marker was toggled.</returns>
    internal static bool Toggle(RichTextBox editor, TextPointer? position)
    {
        if (MarkerAt(position) is not { Tag: TaskMarkerRole role } marker)
        {
            return false;
        }

        editor.BeginChange();
        try
        {
            SetChecked(marker, !role.Checked);
        }
        finally
        {
            editor.EndChange();
        }

        return true;
    }

    /// <summary>
    /// Sets a Task Marker's checked state, updating its role and its glyph together so the text the
    /// user sees and the text Capture emits can never disagree (INV-024).
    /// </summary>
    /// <param name="marker">The Task Marker run.</param>
    /// <param name="isChecked">Whether the task is checked.</param>
    internal static void SetChecked(Run marker, bool isChecked)
    {
        marker.Tag = new TaskMarkerRole(isChecked);
        marker.Text = isChecked ? CheckedGlyph : UncheckedGlyph;
    }

    // Neither glyph exists in the UI font, so without an explicit family Windows resolves each one
    // through font fallback independently — and lands on two different faces. The checkbox then
    // visibly changes size and weight as it is ticked. Naming one family that has both keeps the
    // box identical in either state. This is presentation only: Capture reads the role, not the font.
    private static readonly FontFamily CheckboxFont = new("Segoe UI Symbol");
}
