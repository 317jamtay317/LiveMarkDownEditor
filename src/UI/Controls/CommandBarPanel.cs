using System.Windows;
using System.Windows.Controls;

namespace UI.Controls;

/// <summary>
/// The Command Bar's layout panel: it lays its items out in one horizontal row and, when they would run
/// past the available width, collapses whole groups of actions to their single dropdown — lowest
/// <see cref="CollapseOrderProperty"/> first — so nothing is ever pushed off-screen (INV-054). While the
/// row is wide enough, every group shows its individual Command Icons instead.
/// </summary>
/// <remarks>
/// A collapsible group is a pair of children sharing an <see cref="OverflowGroupProperty"/>: the
/// expanded form (its individual buttons, usually wrapped in a panel) and the collapsed form — a single
/// dropdown — marked <see cref="IsOverflowProperty"/>. A child with no group is always shown. A child
/// docked right (<c>DockPanel.Dock="Right"</c>) is laid out against the right edge, its width reserved
/// before groups are collapsed. Collapsing is presentation-only: the same command runs whether it is
/// reached as an icon or as a dropdown entry, and no Markdown Document is touched (INV-054).
/// </remarks>
public sealed class CommandBarPanel : Panel
{
    /// <summary>Identifies the OverflowGroup attached property — the name pairing a group's two forms.</summary>
    public static readonly DependencyProperty OverflowGroupProperty = DependencyProperty.RegisterAttached(
        "OverflowGroup",
        typeof(string),
        typeof(CommandBarPanel),
        new FrameworkPropertyMetadata(defaultValue: null, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>Identifies the IsOverflow attached property — whether a child is a group's collapsed form.</summary>
    public static readonly DependencyProperty IsOverflowProperty = DependencyProperty.RegisterAttached(
        "IsOverflow",
        typeof(bool),
        typeof(CommandBarPanel),
        new FrameworkPropertyMetadata(defaultValue: false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>Identifies the CollapseOrder attached property — groups collapse in ascending order.</summary>
    public static readonly DependencyProperty CollapseOrderProperty = DependencyProperty.RegisterAttached(
        "CollapseOrder",
        typeof(int),
        typeof(CommandBarPanel),
        new FrameworkPropertyMetadata(defaultValue: 0, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    private HashSet<string> _collapsed = [];

    /// <summary>Gets the <see cref="OverflowGroupProperty"/> — the group a child belongs to, or null.</summary>
    /// <param name="element">The child element.</param>
    /// <returns>The group name, or <see langword="null"/> when the child is always shown.</returns>
    public static string? GetOverflowGroup(DependencyObject element) => (string?)element.GetValue(OverflowGroupProperty);

    /// <summary>Sets the <see cref="OverflowGroupProperty"/> — the group a child belongs to.</summary>
    /// <param name="element">The child element.</param>
    /// <param name="value">The group name.</param>
    public static void SetOverflowGroup(DependencyObject element, string? value) => element.SetValue(OverflowGroupProperty, value);

    /// <summary>Gets the <see cref="IsOverflowProperty"/> — whether the child is the group's collapsed form.</summary>
    /// <param name="element">The child element.</param>
    /// <returns><see langword="true"/> for the collapsed (dropdown) form.</returns>
    public static bool GetIsOverflow(DependencyObject element) => (bool)element.GetValue(IsOverflowProperty);

    /// <summary>Sets the <see cref="IsOverflowProperty"/> — whether the child is the group's collapsed form.</summary>
    /// <param name="element">The child element.</param>
    /// <param name="value"><see langword="true"/> for the collapsed (dropdown) form.</param>
    public static void SetIsOverflow(DependencyObject element, bool value) => element.SetValue(IsOverflowProperty, value);

    /// <summary>Gets the <see cref="CollapseOrderProperty"/> — a group's collapse order (lower collapses first).</summary>
    /// <param name="element">The child element.</param>
    /// <returns>The collapse order.</returns>
    public static int GetCollapseOrder(DependencyObject element) => (int)element.GetValue(CollapseOrderProperty);

    /// <summary>Sets the <see cref="CollapseOrderProperty"/> — a group's collapse order (lower collapses first).</summary>
    /// <param name="element">The child element.</param>
    /// <param name="value">The collapse order.</param>
    public static void SetCollapseOrder(DependencyObject element, int value) => element.SetValue(CollapseOrderProperty, value);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // Every child is measured at its natural width regardless of whether it is currently shown, so
        // the collapse decision always has each form's true width. Hiding is done by opacity in Arrange,
        // not by collapsing layout, so nothing here changes a child's measured size.
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }

        _collapsed = DecideCollapsedGroups(availableSize.Width);

        double left = 0, right = 0, height = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (!IsActive(child))
            {
                continue;
            }

            if (IsRight(child))
            {
                right += child.DesiredSize.Width;
            }
            else
            {
                left += child.DesiredSize.Width;
            }

            height = Math.Max(height, child.DesiredSize.Height);
        }

        var width = left + right;
        if (!double.IsInfinity(availableSize.Width))
        {
            width = Math.Min(width, availableSize.Width);
        }

        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _collapsed = DecideCollapsedGroups(finalSize.Width);

        double x = 0;
        foreach (UIElement child in InternalChildren)
        {
            var active = IsActive(child);
            Show(child, active);
            if (IsRight(child) || !active)
            {
                // Hidden children are parked at zero size so they neither take space nor push siblings;
                // opacity (set in Show) is what keeps them invisible, since a zero slot alone would not.
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            var width = child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }

        double rightEdge = finalSize.Width;
        for (var i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var child = InternalChildren[i];
            if (!IsRight(child) || !IsActive(child))
            {
                continue;
            }

            var width = child.DesiredSize.Width;
            rightEdge -= width;
            child.Arrange(new Rect(rightEdge, 0, width, finalSize.Height));
        }

        return finalSize;
    }

    // Hides an inactive child without disturbing layout: opacity removes it visually and IsHitTestVisible
    // removes it from the mouse, while it stays measurable so a later resize can bring it back.
    private static void Show(UIElement child, bool visible)
    {
        child.Opacity = visible ? 1 : 0;
        child.IsHitTestVisible = visible;
    }

    // Collapses groups by ascending CollapseOrder until the active items fit the width (less the width
    // reserved for right-docked items). With no width constraint every group stays expanded.
    private HashSet<string> DecideCollapsedGroups(double availableWidth)
    {
        var collapsed = new HashSet<string>();
        if (double.IsInfinity(availableWidth))
        {
            return collapsed;
        }

        var groups = new Dictionary<string, (double Primary, double Overflow, int Order)>();
        double fixedWidth = 0, rightWidth = 0;
        foreach (UIElement child in InternalChildren)
        {
            var width = child.DesiredSize.Width;
            if (IsRight(child))
            {
                rightWidth += width;
                continue;
            }

            if (GetOverflowGroup(child) is not { } group)
            {
                fixedWidth += width;
                continue;
            }

            var entry = groups.GetValueOrDefault(group);
            if (GetIsOverflow(child))
            {
                entry.Overflow = width;
            }
            else
            {
                // A group's expanded form can be several children (its individual icons); their widths add.
                entry.Primary += width;
            }

            entry.Order = GetCollapseOrder(child);
            groups[group] = entry;
        }

        var budget = availableWidth - rightWidth;
        var total = fixedWidth + groups.Values.Sum(group => group.Primary);
        foreach (var (name, group) in groups.OrderBy(pair => pair.Value.Order).ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (total <= budget)
            {
                break;
            }

            collapsed.Add(name);
            total += group.Overflow - group.Primary;
        }

        return collapsed;
    }

    // A child is active when it is a fixed item, an expanded form whose group is not collapsed, or a
    // collapsed form whose group is collapsed.
    private bool IsActive(UIElement child)
    {
        if (GetOverflowGroup(child) is not { } group)
        {
            return true;
        }

        return GetIsOverflow(child) == _collapsed.Contains(group);
    }

    private static bool IsRight(UIElement child) => DockPanel.GetDock(child) == Dock.Right;
}
