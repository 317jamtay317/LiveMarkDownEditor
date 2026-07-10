using System.Windows;
using System.Windows.Controls;

namespace UI.Controls;

/// <summary>
/// An attached behaviour that moves keyboard focus to an element whenever it becomes visible, so a
/// bar revealed by a command (such as the Find Bar opened with Ctrl+F) is ready to type into without
/// a separate click. When the element is a <see cref="TextBox"/> its text is also selected.
/// </summary>
/// <remarks>
/// Focusing a specific view element is view-interaction logic, so it lives in a behaviour rather than
/// a View's code-behind.
/// </remarks>
public static class FocusOnVisible
{
    /// <summary>Identifies the <c>Enabled</c> attached property.</summary>
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(FocusOnVisible),
        new PropertyMetadata(false, OnEnabledChanged));

    /// <summary>Enables or disables focus-on-visible for <paramref name="element"/>.</summary>
    /// <param name="element">The element to focus when it becomes visible.</param>
    /// <param name="value"><see langword="true"/> to focus it on each transition to visible.</param>
    public static void SetEnabled(DependencyObject element, bool value) =>
        element.SetValue(EnabledProperty, value);

    /// <summary>Gets whether focus-on-visible is enabled for <paramref name="element"/>.</summary>
    /// <param name="element">The element to query.</param>
    /// <returns><see langword="true"/> when it is focused on becoming visible.</returns>
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.IsVisibleChanged += OnIsVisibleChanged;
        }
        else
        {
            element.IsVisibleChanged -= OnIsVisibleChanged;
        }
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || sender is not FrameworkElement element)
        {
            return;
        }

        // Defer until the element is laid out and focusable; focusing during the visibility change
        // itself is too early.
        element.Dispatcher.BeginInvoke(() =>
        {
            element.Focus();
            if (element is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }
}
