using System.Windows;

namespace UI.Controls;

/// <summary>
/// Closes a dialog <see cref="Window"/> from its ViewModel by binding the window's
/// <see cref="Window.DialogResult"/> to a bound value. A dialog's result can only be set in code,
/// which would otherwise be the one reason for a View to have code-behind; this attached property
/// keeps that rule intact.
/// </summary>
public static class DialogCloser
{
    /// <summary>
    /// Identifies the DialogResult attached property. Set it on a <see cref="Window"/> and bind it
    /// to the ViewModel's outcome: assigning a non-null value closes the dialog with that result.
    /// </summary>
    public static readonly DependencyProperty DialogResultProperty = DependencyProperty.RegisterAttached(
        "DialogResult",
        typeof(bool?),
        typeof(DialogCloser),
        new PropertyMetadata(defaultValue: null, OnDialogResultChanged));

    /// <summary>Sets the dialog result that closes <paramref name="target"/>.</summary>
    /// <param name="target">The dialog window.</param>
    /// <param name="value">The result to close with, or <see langword="null"/> to leave it open.</param>
    public static void SetDialogResult(DependencyObject target, bool? value) =>
        target.SetValue(DialogResultProperty, value);

    /// <summary>Gets the dialog result bound to <paramref name="target"/>.</summary>
    /// <param name="target">The dialog window.</param>
    /// <returns>The bound result, or <see langword="null"/> while the dialog is unanswered.</returns>
    public static bool? GetDialogResult(DependencyObject target) =>
        (bool?)target.GetValue(DialogResultProperty);

    private static void OnDialogResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Only a shown, still-open dialog can take a DialogResult — setting it otherwise throws.
        if (d is Window window && e.NewValue is bool result && window.IsLoaded)
        {
            window.DialogResult = result;
        }
    }
}
