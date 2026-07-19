using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Domain;
using UI.ViewModels;

namespace UI.Controls;

/// <summary>
/// Converts a <see cref="NodeShape"/> to the <see cref="Geometry"/> a Diagram Node is drawn with on the
/// Flowchart Builder's canvas, sized to the node box (<see cref="FlowchartNodeViewModel.Width"/> ×
/// <see cref="FlowchartNodeViewModel.Height"/>). The live Diagram Preview shows the exact Mermaid
/// rendering; the canvas shape is a recognisable stand-in.
/// </summary>
public sealed class NodeShapeGeometryConverter : IValueConverter
{
    private const double W = FlowchartNodeViewModel.Width;
    private const double H = FlowchartNodeViewModel.Height;

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
/// Builds a Diagram Edge's line geometry — the shaft between the two node centres plus an arrowhead at
/// the target end — from the bound endpoints (<c>X1</c>, <c>Y1</c>, <c>X2</c>, <c>Y2</c>) and the
/// <see cref="EdgeKind"/>. An Open edge draws no arrowhead. The shaft is stroked and the arrowhead
/// filled by giving the drawing <c>Path</c> the same Stroke and Fill brush.
/// </summary>
public sealed class EdgeGeometryConverter : IMultiValueConverter
{
    private const double Inset = 30; // pull the ends out of the node boxes
    private const double Head = 12;
    private const double HalfWing = 6;

    /// <inheritdoc />
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 5 || values[0] is not double x1 || values[1] is not double y1 ||
            values[2] is not double x2 || values[3] is not double y2)
        {
            return Geometry.Empty;
        }

        var source = new Point(x1, y1);
        var target = new Point(x2, y2);
        var delta = target - source;
        var length = delta.Length;
        if (length < 1)
        {
            return Geometry.Empty;
        }

        delta /= length;
        var start = source + (delta * Math.Min(Inset, length / 2));
        var tip = target - (delta * Math.Min(Inset, length / 2));

        var geometry = new PathGeometry();
        var shaft = new PathFigure { StartPoint = start };
        shaft.Segments.Add(new LineSegment(tip, true));
        geometry.Figures.Add(shaft);

        if (values[4] is not EdgeKind.Open)
        {
            var perpendicular = new Vector(-delta.Y, delta.X);
            var basePoint = tip - (delta * Head);
            var head = new PathFigure { StartPoint = tip, IsClosed = true };
            head.Segments.Add(new LineSegment(basePoint + (perpendicular * HalfWing), true));
            head.Segments.Add(new LineSegment(basePoint - (perpendicular * HalfWing), true));
            geometry.Figures.Add(head);
        }

        geometry.Freeze();
        return geometry;
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

/// <summary>Converts an <see cref="EdgeKind"/> to its stroke dash pattern — a Dotted edge is dashed, others solid.</summary>
public sealed class EdgeDashConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EdgeKind.Dotted ? new DoubleCollection([3, 3]) : new DoubleCollection();

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
