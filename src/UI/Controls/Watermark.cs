using System.Windows;

namespace UI.Controls;

/// <summary>
/// An attached property carrying the Watermark of a text box: the hint shown in place of its text
/// while it is empty, so an empty box still says what it is for. The Find Bar uses it to label its
/// query box ("Find") and its Replacement box ("Replace"), neither of which draws a border.
/// </summary>
/// <remarks>
/// This only carries the text. Drawing it is the business of a control template that binds to this
/// property and reveals it while the box's <c>Text</c> is empty — see the <c>FindBarTextBox</c> style
/// in <c>Themes/Controls.xaml</c>. A Watermark is presentation-only: it is never part of the box's
/// text, so it can never reach the Markdown Document.
/// </remarks>
public static class Watermark
{
    /// <summary>Identifies the <c>Text</c> attached property.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(Watermark),
        new PropertyMetadata(string.Empty));

    /// <summary>Sets the Watermark shown while <paramref name="element"/> is empty.</summary>
    /// <param name="element">The text box to hint.</param>
    /// <param name="value">The hint text.</param>
    public static void SetText(DependencyObject element, string value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(TextProperty, value);
    }

    /// <summary>Gets the Watermark shown while <paramref name="element"/> is empty.</summary>
    /// <param name="element">The text box to read.</param>
    /// <returns>The hint text; empty when none is set.</returns>
    public static string GetText(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string)element.GetValue(TextProperty);
    }
}
