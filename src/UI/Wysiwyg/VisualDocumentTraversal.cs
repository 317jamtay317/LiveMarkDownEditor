using System.Windows;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// Shared structural walks over a Visual Document, used by the Formatting Actions to find where the
/// caret or selection sits (its enclosing element, or its top-level block).
/// </summary>
internal static class VisualDocumentTraversal
{
    /// <summary>
    /// The nearest ancestor element of type <typeparamref name="T"/> containing
    /// <paramref name="position"/>, or <see langword="null"/> when there is none.
    /// </summary>
    /// <typeparam name="T">The element type to look for (e.g. <see cref="TableCell"/>).</typeparam>
    /// <param name="position">The position whose ancestry is walked.</param>
    internal static T? AncestorOf<T>(TextPointer? position) where T : TextElement
    {
        for (DependencyObject? node = position?.Parent; node is TextElement element; node = element.Parent)
        {
            if (element is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// The block that sits directly in the <see cref="FlowDocument"/> and contains
    /// <paramref name="position"/>, or <see langword="null"/> when the position is not inside one.
    /// </summary>
    /// <param name="position">The position whose top-level block is sought.</param>
    internal static Block? TopLevelBlockOf(TextPointer? position)
    {
        for (DependencyObject? node = position?.Parent; node is TextElement element; node = element.Parent)
        {
            if (element is Block block && block.Parent is FlowDocument)
            {
                return block;
            }
        }

        return null;
    }
}
