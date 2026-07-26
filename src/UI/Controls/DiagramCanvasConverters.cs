using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Domain;
using UI.ViewModels;

namespace UI.Controls;

/// <summary>
/// Converts a <see cref="NodeShape"/> to the <see cref="Geometry"/> a Diagram Node is drawn with on the
/// Diagram Builder's canvas, sized to the node box (<see cref="DiagramNodeViewModel.Width"/> ×
/// <see cref="DiagramNodeViewModel.Height"/>). The live Diagram Preview shows the exact Mermaid
/// rendering; the canvas shape is a recognisable stand-in.
/// </summary>
public sealed class NodeShapeGeometryConverter : IValueConverter
{
    private const double W = DiagramNodeViewModel.Width;
    private const double H = DiagramNodeViewModel.Height;

    /// <summary>The diameter a State Diagram's Terminal is drawn at — a marker, not a box.</summary>
    public const double TerminalDiameter = 44;

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var rect = new Rect(1, 1, W - 2, H - 2);
        Geometry geometry = value switch
        {
            NodeShape.Rounded => new RectangleGeometry(rect, 12, 12),
            NodeShape.Stadium => new RectangleGeometry(rect, H / 2, H / 2),
            NodeShape.Circle => new EllipseGeometry(rect),
            NodeShape.Diamond => Diamond(rect),
            NodeShape.Terminal => new EllipseGeometry(new Rect(
                (W - TerminalDiameter) / 2, (H - TerminalDiameter) / 2, TerminalDiameter, TerminalDiameter)),
            _ => new RectangleGeometry(rect, 2, 2),
        };
        geometry.Freeze();
        return geometry;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static Geometry Diamond(Rect r)
    {
        var figure = new PathFigure { StartPoint = new Point(r.Left + (r.Width / 2), r.Top), IsClosed = true };
        figure.Segments.Add(new LineSegment(new Point(r.Right, r.Top + (r.Height / 2)), true));
        figure.Segments.Add(new LineSegment(new Point(r.Left + (r.Width / 2), r.Bottom), true));
        figure.Segments.Add(new LineSegment(new Point(r.Left, r.Top + (r.Height / 2)), true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}

/// <summary>
/// Builds a Diagram Edge's line geometry from the bound endpoints (<c>X1</c>, <c>Y1</c>, <c>X2</c>,
/// <c>Y2</c>), the <see cref="EdgeKind"/>, and the two endpoints' <see cref="NodeShape"/>s — the shaft
/// plus the markers that end it (<see cref="DiagramEdgeGeometry"/>). The shapes are what hold the line
/// off each node's outline, so a marker is never buried under the node box. With the converter
/// parameter <c>Hollow</c> it builds the hollow markers alone, which a second <c>Path</c> draws over
/// the line filled with the canvas background.
/// </summary>
public sealed class EdgeGeometryConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 5 || values[0] is not double x1 || values[1] is not double y1 ||
            values[2] is not double x2 || values[3] is not double y2 || values[4] is not EdgeKind kind)
        {
            return Geometry.Empty;
        }

        var source = new Point(x1, y1);
        var target = new Point(x2, y2);
        var fromShape = values.Length > 5 && values[5] is NodeShape from ? from : NodeShape.Rectangle;
        var toShape = values.Length > 6 && values[6] is NodeShape to ? to : NodeShape.Rectangle;
        return string.Equals(parameter as string, "Hollow", StringComparison.Ordinal)
            ? DiagramEdgeGeometry.Hollow(source, target, kind, fromShape, toShape)
            : DiagramEdgeGeometry.Solid(source, target, kind, fromShape, toShape);
    }

    /// <inheritdoc />
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts an <see cref="EdgeKind"/> to the stroke thickness its line is drawn with — a Thick edge is heavier.</summary>
public sealed class EdgeThicknessConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EdgeKind.Thick ? 3.0 : 1.6;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>
/// Converts an <see cref="EdgeKind"/> to its stroke dash pattern — a Dotted edge and a Class Diagram's
/// Dependency are dashed, as Mermaid draws them; every other kind is solid.
/// </summary>
public sealed class EdgeDashConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EdgeKind.Dotted or EdgeKind.Dependency ? new DoubleCollection([3, 3]) : new DoubleCollection();

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Averages two bound coordinates — used to place a Diagram Edge's label at the line's midpoint.</summary>
public sealed class MidpointConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length >= 2 && values[0] is double a && values[1] is double b ? (a + b) / 2 : 0d;

    /// <inheritdoc />
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Spells a term the Diagram Builder's pickers show as words — <c>EntityRelationshipDiagram</c> reads
/// "Entity relationship diagram", <c>OneToMany</c> reads "One to many". Presentation only: the terms
/// themselves stay the ubiquitous language's, and nothing converts back.
/// </summary>
public sealed class PascalCaseWordsConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString();
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var words = new StringBuilder(text.Length + 8);
        foreach (var character in text)
        {
            if (char.IsUpper(character) && words.Length > 0)
            {
                words.Append(' ').Append(char.ToLower(character, culture));
            }
            else
            {
                words.Append(character);
            }
        }

        return words.ToString();
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
