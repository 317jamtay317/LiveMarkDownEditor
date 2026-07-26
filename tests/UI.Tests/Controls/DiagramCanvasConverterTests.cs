using System.Globalization;
using System.Windows.Media;
using Domain;
using Shouldly;
using UI.Controls;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Diagram Builder canvas's converters — the shape each <see cref="NodeShape"/> is drawn
/// as, and how a term is spelled in the builder's pickers.
/// </summary>
public sealed class DiagramCanvasConverterTests
{
    private static object Shape(NodeShape shape) =>
        new NodeShapeGeometryConverter().Convert(shape, typeof(Geometry), null, CultureInfo.InvariantCulture);

    [Fact]
    public void ATerminal_IsDrawnAsACircularMarker_NotABox()
    {
        var geometry = Shape(NodeShape.Terminal).ShouldBeOfType<EllipseGeometry>();

        geometry.Bounds.Width.ShouldBe(NodeShapeGeometryConverter.TerminalDiameter);
        geometry.Bounds.Height.ShouldBe(NodeShapeGeometryConverter.TerminalDiameter);
    }

    [Fact]
    public void EveryNodeShape_HasAGeometry()
    {
        foreach (var shape in Enum.GetValues<NodeShape>())
        {
            ((Geometry)Shape(shape)).IsEmpty().ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(DiagramKind.Flowchart, "Flowchart")]
    [InlineData(DiagramKind.StateDiagram, "State diagram")]
    [InlineData(DiagramKind.ClassDiagram, "Class diagram")]
    [InlineData(DiagramKind.EntityRelationshipDiagram, "Entity relationship diagram")]
    [InlineData(EdgeKind.OneToMany, "One to many")]
    [InlineData(EdgeKind.Arrow, "Arrow")]
    [InlineData(FlowDirection.LeftRight, "Left right")]
    [InlineData(NodeShape.Terminal, "Terminal")]
    public void APickersTerm_IsSpelledAsWords(object term, string expected) =>
        new PascalCaseWordsConverter()
            .Convert(term, typeof(string), null, CultureInfo.InvariantCulture)
            .ShouldBe(expected);
}
