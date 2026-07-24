using System.Windows;

namespace UI.Controls;

/// <summary>
/// An attached behaviour that reports a <see cref="FrameworkElement"/>'s live width into a bindable
/// attached property, so a ViewModel can drive its responsive layout off the measured width without any
/// code-behind. Set <see cref="ObserveProperty"/> to start watching, and bind
/// <see cref="ObservedWidthProperty"/> <c>OneWayToSource</c> to the ViewModel property that should track
/// the width. It is the width feed behind Compact Layout (INV-059), the panel-layout counterpart of the
/// Command Bar's own width-driven overflow collapse (INV-054).
/// </summary>
public static class SizeObserver
{
    /// <summary>Identifies the Observe attached property — whether the element's width is being watched.</summary>
    public static readonly DependencyProperty ObserveProperty = DependencyProperty.RegisterAttached(
        "Observe",
        typeof(bool),
        typeof(SizeObserver),
        new PropertyMetadata(defaultValue: false, OnObserveChanged));

    /// <summary>Identifies the ObservedWidth attached property — the element's most recently reported width.</summary>
    public static readonly DependencyProperty ObservedWidthProperty = DependencyProperty.RegisterAttached(
        "ObservedWidth",
        typeof(double),
        typeof(SizeObserver),
        new PropertyMetadata(defaultValue: 0d));

    /// <summary>Gets whether the element's width is being watched.</summary>
    /// <param name="element">The element the behaviour is attached to.</param>
    /// <returns><see langword="true"/> while the element's width is being reported.</returns>
    public static bool GetObserve(DependencyObject element) => (bool)element.GetValue(ObserveProperty);

    /// <summary>Sets whether the element's width is being watched.</summary>
    /// <param name="element">The element to watch.</param>
    /// <param name="value"><see langword="true"/> to start reporting the element's width.</param>
    public static void SetObserve(DependencyObject element, bool value) => element.SetValue(ObserveProperty, value);

    /// <summary>Gets the element's most recently reported width.</summary>
    /// <param name="element">The element the behaviour is attached to.</param>
    /// <returns>The element's last reported width.</returns>
    public static double GetObservedWidth(DependencyObject element) => (double)element.GetValue(ObservedWidthProperty);

    /// <summary>Sets the reported width. Used by the behaviour; bind it <c>OneWayToSource</c> to read the width.</summary>
    /// <param name="element">The element the behaviour is attached to.</param>
    /// <param name="value">The width to report.</param>
    public static void SetObservedWidth(DependencyObject element, double value) => element.SetValue(ObservedWidthProperty, value);

    private static void OnObserveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.SizeChanged += OnSizeChanged;
            // Seed the current width so a ViewModel bound before the first layout still starts in step.
            SetObservedWidth(element, element.ActualWidth);
        }
        else
        {
            element.SizeChanged -= OnSizeChanged;
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        SetObservedWidth((DependencyObject)sender, e.NewSize.Width);
}
