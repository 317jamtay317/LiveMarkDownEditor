using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a List: each List Item indented one step, behind the marker its kind carries — a bullet
/// for an Unordered List, the next number for an Ordered List, a checkbox for a Task List.
/// </summary>
/// <remarks>
/// A MigraDoc list style cannot show a Task Marker, so every marker is written as text at the head
/// of the item's first paragraph. Any further block of the item is written beneath it at the same
/// indent, so a paragraph or a nested List inside an item stays with the item it belongs to.
/// </remarks>
internal sealed class ListWriter : IBlockWriter
{
    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is not ListBlock list)
        {
            return;
        }

        var number = ParseStart(list.OrderedStart);
        foreach (var itemBlock in list)
        {
            var item = (ListItemBlock)itemBlock;
            var task = FindTask(item);
            var marker = task is not null
                ? (task.Checked ? "[x] " : "[ ] ")
                : list.IsOrdered ? $"{number}. " : "• ";

            WriteItem(item, marker, context.Indented());
            number++;
        }
    }

    private static void WriteItem(ListItemBlock item, string marker, BlockContext context)
    {
        var wroteMarker = false;
        foreach (var child in item)
        {
            if (child is ParagraphBlock paragraph && !wroteMarker)
            {
                var written = context.NewParagraph();
                written.AddText(marker);
                InlineWriter.Write(paragraph.Inline, written);
                wroteMarker = true;
                continue;
            }

            context.Write(child);
        }
    }

    // The Task Marker a List Item leads with, if it is a Task List Item at all.
    private static TaskList? FindTask(ListItemBlock item) =>
        item.Count > 0 && item[0] is ParagraphBlock { Inline.FirstChild: TaskList task } ? task : null;

    private static int ParseStart(string? orderedStart) =>
        int.TryParse(orderedStart, out var start) ? start : 1;
}
