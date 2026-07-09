using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UI.Controls;

/// <summary>
/// Converts a Section Heading's level (1–6) into the left indent of its Outline Entry, so the
/// Navigation Panel visually nests subsections under their parents. Presentation-only.
/// </summary>
public sealed class HeadingLevelToIndentConverter : IValueConverter
{
    private const double IndentPerLevel = 14d;

    /// <summary>Converts a heading level to a left-only <see cref="Thickness"/> indent.</summary>
    /// <param name="value">The heading level (1–6); non-integer values are treated as level 1.</param>
    /// <param name="targetType">The binding target type (ignored).</param>
    /// <param name="parameter">Unused converter parameter.</param>
    /// <param name="culture">The culture (ignored).</param>
    /// <returns>A left margin proportional to the heading's depth.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is int headingLevel ? headingLevel : 1;
        return new Thickness((level - 1) * IndentPerLevel, 0, 0, 0);
    }

    /// <summary>Not supported — the indent is a one-way presentation projection.</summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Never returns; always throws.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
