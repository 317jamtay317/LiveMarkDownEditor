using System.Windows;
using System.Windows.Media;
using Domain;
using UI.ViewModels;

namespace UI.Controls;

/// <summary>
/// Draws a Diagram Edge on the Diagram Builder's canvas: the shaft between two node centres and the
/// end markers its <see cref="EdgeKind"/> calls for — an arrowhead, a class relationship's triangle or
/// diamond, or an Entity Relationship cardinality's bar or crow's foot. Pure geometry; the live Diagram
/// Preview shows the exact Mermaid rendering, and the canvas shows a recognisable stand-in.
/// </summary>
/// <remarks>
/// Markers come in two layers because some are hollow: <see cref="Solid"/> is stroked and filled with
/// the edge's own brush, and <see cref="Hollow"/> is drawn over it filled with the canvas background,
/// which is what tells composition from aggregation and inheritance from association. A class
/// relationship's marker sits at the <b>From</b> end, matching Mermaid's own reading of
/// <c>Animal &lt;|-- Duck</c>.
/// </remarks>
internal static class DiagramEdgeGeometry
{
    private const double Gap = 5;       // clear air between a node's outline and the edge's marker
    private const double Head = 12;     // how far back from the anchor a triangle or diamond reaches
    private const double HalfWing = 6;  // half the width of a marker
    private const double Bar = 9;       // how far back from the anchor a cardinality bar sits
    private const double Foot = 12;     // how far back from the anchor a crow's foot forks

    /// <summary>The shaft plus the markers drawn in the edge's own brush.</summary>
    /// <param name="source">The centre of the node the edge runs from.</param>
    /// <param name="target">The centre of the node the edge runs to.</param>
    /// <param name="kind">How the edge is drawn.</param>
    /// <param name="fromShape">The shape of the node the edge runs from, which sets how far the line is held off it.</param>
    /// <param name="toShape">The shape of the node the edge runs to.</param>
    /// <returns>The frozen geometry, or <see cref="Geometry.Empty"/> when the ends coincide.</returns>
    public static Geometry Solid(
        Point source, Point target, EdgeKind kind,
        NodeShape fromShape = NodeShape.Rectangle, NodeShape toShape = NodeShape.Rectangle) =>
        Build(source, target, kind, fromShape, toShape, hollow: false);

    /// <summary>The markers drawn hollow — filled with the canvas background so the shaft does not show through.</summary>
    /// <param name="source">The centre of the node the edge runs from.</param>
    /// <param name="target">The centre of the node the edge runs to.</param>
    /// <param name="kind">How the edge is drawn.</param>
    /// <param name="fromShape">The shape of the node the edge runs from.</param>
    /// <param name="toShape">The shape of the node the edge runs to.</param>
    /// <returns>The frozen geometry, or <see cref="Geometry.Empty"/> when the kind has no hollow marker.</returns>
    public static Geometry Hollow(
        Point source, Point target, EdgeKind kind,
        NodeShape fromShape = NodeShape.Rectangle, NodeShape toShape = NodeShape.Rectangle) =>
        Build(source, target, kind, fromShape, toShape, hollow: true);

    private static Geometry Build(
        Point source, Point target, EdgeKind kind, NodeShape fromShape, NodeShape toShape, bool hollow)
    {
        var delta = target - source;
        var length = delta.Length;
        if (length < 1)
        {
            return Geometry.Empty;
        }

        delta /= length;
        var start = source + (delta * Math.Min(Inset(fromShape, delta), length / 2));
        var tip = target - (delta * Math.Min(Inset(toShape, delta), length / 2));

        var geometry = new PathGeometry();
        if (!hollow)
        {
            var shaft = new PathFigure { StartPoint = start, IsFilled = false };
            shaft.Segments.Add(new LineSegment(tip, true));
            geometry.Figures.Add(shaft);
        }

        // A marker's "forward" is the way its point faces: outwards at each end of the shaft.
        AddMarker(geometry, SourceMarker(kind), start, -delta, hollow);
        AddMarker(geometry, TargetMarker(kind), tip, delta, hollow);

        geometry.Freeze();
        return geometry;
    }

    // How far from a node's centre the edge starts: where the line leaves that node's own outline,
    // plus a little air. A fixed inset would bury an arrowhead or a crow's foot under the node box,
    // since the nodes are drawn over the edges.
    private static double Inset(NodeShape shape, Vector direction)
    {
        if (shape == NodeShape.Terminal)
        {
            return (NodeShapeGeometryConverter.TerminalDiameter / 2) + Gap;
        }

        // The ray/box intersection for a unit direction — the smaller of the two axis crossings.
        var byX = Math.Abs(direction.X) < 1e-6
            ? double.PositiveInfinity
            : DiagramNodeViewModel.Width / 2 / Math.Abs(direction.X);
        var byY = Math.Abs(direction.Y) < 1e-6
            ? double.PositiveInfinity
            : DiagramNodeViewModel.Height / 2 / Math.Abs(direction.Y);
        return Math.Min(byX, byY) + Gap;
    }

    // The marker at the end the edge runs from. A class relationship carries its marker here.
    private static Marker SourceMarker(EdgeKind kind) => kind switch
    {
        EdgeKind.Inheritance => Marker.HollowTriangle,
        EdgeKind.Composition => Marker.FilledDiamond,
        EdgeKind.Aggregation => Marker.HollowDiamond,
        EdgeKind.OneToOne or EdgeKind.OneToMany => Marker.Bar,
        EdgeKind.ManyToOne or EdgeKind.ManyToMany => Marker.CrowsFoot,
        _ => Marker.None,
    };

    // The marker at the end the edge runs to — an arrowhead, or a cardinality.
    private static Marker TargetMarker(EdgeKind kind) => kind switch
    {
        EdgeKind.Arrow or EdgeKind.Dotted or EdgeKind.Thick or EdgeKind.Dependency => Marker.FilledTriangle,
        EdgeKind.OneToOne or EdgeKind.ManyToOne => Marker.Bar,
        EdgeKind.OneToMany or EdgeKind.ManyToMany => Marker.CrowsFoot,
        _ => Marker.None,
    };

    private static void AddMarker(PathGeometry geometry, Marker marker, Point anchor, Vector forward, bool hollow)
    {
        var wantsHollow = marker is Marker.HollowTriangle or Marker.HollowDiamond;
        if (marker == Marker.None || wantsHollow != hollow)
        {
            return;
        }

        var perpendicular = new Vector(-forward.Y, forward.X);
        switch (marker)
        {
            case Marker.FilledTriangle or Marker.HollowTriangle:
                geometry.Figures.Add(Triangle(anchor, forward, perpendicular));
                break;
            case Marker.FilledDiamond or Marker.HollowDiamond:
                geometry.Figures.Add(Diamond(anchor, forward, perpendicular));
                break;
            case Marker.Bar:
                geometry.Figures.Add(Stroke(
                    anchor - (forward * Bar) + (perpendicular * HalfWing),
                    anchor - (forward * Bar) - (perpendicular * HalfWing)));
                break;
            case Marker.CrowsFoot:
                var knee = anchor - (forward * Foot);
                geometry.Figures.Add(Stroke(knee, anchor));
                geometry.Figures.Add(Stroke(knee, anchor + (perpendicular * HalfWing)));
                geometry.Figures.Add(Stroke(knee, anchor - (perpendicular * HalfWing)));
                break;
        }
    }

    private static PathFigure Triangle(Point anchor, Vector forward, Vector perpendicular)
    {
        var back = anchor - (forward * Head);
        var figure = new PathFigure { StartPoint = anchor, IsClosed = true };
        figure.Segments.Add(new LineSegment(back + (perpendicular * HalfWing), true));
        figure.Segments.Add(new LineSegment(back - (perpendicular * HalfWing), true));
        return figure;
    }

    private static PathFigure Diamond(Point anchor, Vector forward, Vector perpendicular)
    {
        var waist = anchor - (forward * (Head / 2));
        var figure = new PathFigure { StartPoint = anchor, IsClosed = true };
        figure.Segments.Add(new LineSegment(waist + (perpendicular * HalfWing), true));
        figure.Segments.Add(new LineSegment(anchor - (forward * Head), true));
        figure.Segments.Add(new LineSegment(waist - (perpendicular * HalfWing), true));
        return figure;
    }

    // An open, unfilled figure — a cardinality's marks are strokes, not shapes.
    private static PathFigure Stroke(Point from, Point to)
    {
        var figure = new PathFigure { StartPoint = from, IsFilled = false };
        figure.Segments.Add(new LineSegment(to, true));
        return figure;
    }

    private enum Marker
    {
        None,
        FilledTriangle,
        HollowTriangle,
        FilledDiamond,
        HollowDiamond,
        Bar,
        CrowsFoot,
    }
}
