using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UI.Controls;

/// <summary>
/// Converts a visibility flag into a <see cref="GridLength"/> for a grid column, collapsing the
/// column to zero width when the flag is <see langword="false"/>. Used to give the Source Panel a
/// fixed, splitter-resizable width when shown and no footprint when hidden. Presentation-only.
/// </summary>
public sealed class BooleanToGridLengthConverter : IValueConverter
{
    private const double DefaultVisibleWidth = 380d;

    /// <summary>Converts a boolean to a column <see cref="GridLength"/>.</summary>
    /// <param name="value">Whether the column is shown; non-boolean values are treated as hidden.</param>
    /// <param name="targetType">The binding target type (ignored).</param>
    /// <param name="parameter">Optional visible width in pixels; defaults to 380 when absent or unparsable.</param>
    /// <param name="culture">The culture used to parse <paramref name="parameter"/>.</param>
    /// <returns>The visible width as a pixel <see cref="GridLength"/> when shown; otherwise zero.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true)
        {
            return new GridLength(0);
        }

        var width = parameter is string text && double.TryParse(text, NumberStyles.Float, culture, out var parsed)
            ? parsed
            : DefaultVisibleWidth;
        return new GridLength(width);
    }

    /// <summary>Not supported — the column width is a one-way presentation projection.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns; always throws.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
