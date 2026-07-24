using System.Globalization;
using System.Windows.Data;

namespace UI.Controls;

/// <summary>
/// One-way converter answering whether the bound value names the converter parameter — the comparison
/// a menu entry's check state binds through when the state is one choice of an enum (the Page Setup
/// menu's orientation and Margin Preset checks, INV-061).
/// </summary>
public sealed class EqualityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
