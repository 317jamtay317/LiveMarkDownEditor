using System.Windows;
using System.Windows.Controls;

namespace UI.Controls;

/// <summary>
/// An attached behaviour that owns the width of a <b>Panel Column</b> — the Workspace grid column
/// that holds a toggleable panel, such as the Source Panel or the Preview Panel. While its panel is
/// shown the column takes the width the user last dragged its Panel Splitter to, or
/// <see cref="VisibleWidthProperty"/> until it has been dragged; while its panel is hidden the column
/// takes no width at all, so the Visual Document fills the whole Workspace (INV-056).
/// </summary>
/// <remarks>
/// The width cannot be projected from the visibility flag by a converter alone: dragging a
/// <c>GridSplitter</c> writes the column's <see cref="ColumnDefinition.Width"/> directly, and a direct
/// write replaces a one-way binding — so after the first drag the binding is gone and hiding the panel
/// leaves the dragged-open column behind. Owning both the toggle and the remembered drag here keeps
/// the two from fighting over one property. Sizing a column is view-layout logic, so it lives in a
/// behaviour rather than a View's code-behind.
/// </remarks>
public static class PanelColumn
{
    private const double DefaultVisibleWidth = 380d;
    private const double DefaultMinimumWidth = 180d;

    /// <summary>
    /// Identifies the <c>IsVisible</c> attached property — whether the column's panel is shown.
    /// </summary>
    /// <remarks>
    /// It is nullable so that its default is "never set": a column whose panel starts hidden is
    /// collapsed by the first evaluation of the binding, rather than being left at a
    /// <c>ColumnDefinition</c>'s default star width because <c>false</c> matched the default.
    /// </remarks>
    public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.RegisterAttached(
        "IsVisible",
        typeof(bool?),
        typeof(PanelColumn),
        new PropertyMetadata(null, OnIsVisibleChanged));

    /// <summary>
    /// Identifies the <c>VisibleWidth</c> attached property — the width the panel opens at before the
    /// user has dragged its Panel Splitter.
    /// </summary>
    public static readonly DependencyProperty VisibleWidthProperty = DependencyProperty.RegisterAttached(
        "VisibleWidth",
        typeof(double),
        typeof(PanelColumn),
        new PropertyMetadata(DefaultVisibleWidth, OnVisibleWidthChanged));

    /// <summary>
    /// Identifies the <c>MinimumWidth</c> attached property — the narrowest the Panel Splitter may
    /// drag the panel while it is shown, so a shown panel is never crushed to a sliver. It applies
    /// only while the panel is shown: a hidden panel takes no width at all.
    /// </summary>
    public static readonly DependencyProperty MinimumWidthProperty = DependencyProperty.RegisterAttached(
        "MinimumWidth",
        typeof(double),
        typeof(PanelColumn),
        new PropertyMetadata(DefaultMinimumWidth, OnMinimumWidthChanged));

    /// <summary>The width the user last dragged the panel to; unset until it has been dragged.</summary>
    private static readonly DependencyProperty DraggedWidthProperty = DependencyProperty.RegisterAttached(
        "DraggedWidth",
        typeof(double?),
        typeof(PanelColumn),
        new PropertyMetadata(default(double?)));

    /// <summary>Shows or hides the panel in <paramref name="column"/>, sizing the column to match.</summary>
    /// <param name="column">The Panel Column to size.</param>
    /// <param name="value"><see langword="true"/> while the panel is shown; otherwise the column collapses.</param>
    public static void SetIsVisible(DependencyObject column, bool value) =>
        column.SetValue(IsVisibleProperty, value);

    /// <summary>Gets whether the panel in <paramref name="column"/> is shown.</summary>
    /// <param name="column">The Panel Column to query.</param>
    /// <returns><see langword="true"/> while the panel is shown.</returns>
    public static bool GetIsVisible(DependencyObject column) => column.GetValue(IsVisibleProperty) is true;

    /// <summary>Sets the width <paramref name="column"/> opens at before it has been dragged.</summary>
    /// <param name="column">The Panel Column to size.</param>
    /// <param name="value">The width in device-independent pixels.</param>
    public static void SetVisibleWidth(DependencyObject column, double value) =>
        column.SetValue(VisibleWidthProperty, value);

    /// <summary>Gets the width <paramref name="column"/> opens at before it has been dragged.</summary>
    /// <param name="column">The Panel Column to query.</param>
    /// <returns>The width in device-independent pixels.</returns>
    public static double GetVisibleWidth(DependencyObject column) => (double)column.GetValue(VisibleWidthProperty);

    /// <summary>Sets the narrowest the Panel Splitter may drag <paramref name="column"/> while shown.</summary>
    /// <param name="column">The Panel Column to bound.</param>
    /// <param name="value">The minimum width in device-independent pixels.</param>
    public static void SetMinimumWidth(DependencyObject column, double value) =>
        column.SetValue(MinimumWidthProperty, value);

    /// <summary>Gets the narrowest the Panel Splitter may drag <paramref name="column"/> while shown.</summary>
    /// <param name="column">The Panel Column to query.</param>
    /// <returns>The minimum width in device-independent pixels.</returns>
    public static double GetMinimumWidth(DependencyObject column) => (double)column.GetValue(MinimumWidthProperty);

    private static void OnIsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColumnDefinition column)
        {
            Size(column, isShown: e.NewValue is true);
        }
    }

    private static void OnVisibleWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // XAML may set the visible width after the visibility binding has already opened the panel.
        if (d is ColumnDefinition column && GetIsVisible(column) && column.GetValue(DraggedWidthProperty) is null)
        {
            Size(column, isShown: true);
        }
    }

    private static void OnMinimumWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColumnDefinition column && GetIsVisible(column))
        {
            column.MinWidth = GetMinimumWidth(column);
        }
    }

    /// <summary>Sizes the column to its shown width, or to nothing at all when its panel is hidden.</summary>
    private static void Size(ColumnDefinition column, bool isShown)
    {
        // Remember where the user dragged the panel to, so showing it again reopens it there.
        if (!isShown && column.Width.IsAbsolute && column.Width.Value > 0)
        {
            column.SetValue(DraggedWidthProperty, (double?)column.Width.Value);
        }

        // The minimum bounds the splitter, not the toggle — a hidden panel gives up all of its width.
        column.MinWidth = isShown ? GetMinimumWidth(column) : 0;
        column.Width = new GridLength(isShown ? DraggedOrVisibleWidth(column) : 0);
    }

    private static double DraggedOrVisibleWidth(ColumnDefinition column) =>
        column.GetValue(DraggedWidthProperty) as double? ?? GetVisibleWidth(column);
}
