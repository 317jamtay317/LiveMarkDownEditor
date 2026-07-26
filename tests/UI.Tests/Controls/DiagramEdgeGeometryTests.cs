using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Domain;
using Shouldly;
using UI.Controls;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="DiagramEdgeGeometry"/> — how the Diagram Builder's canvas draws each
/// <see cref="EdgeKind"/>. Every kind in every Diagram Kind's Edge Set gets an end marker the reader
/// can tell apart, and the hollow ones (Inheritance, Aggregation) are drawn on their own layer so they
/// are not filled solid (INV-070).
/// </summary>
public sealed class DiagramEdgeGeometryTests
{
    private static readonly Point From = new(0, 0);
    private static readonly Point To = new(300, 0);

    private static int Figures(Geometry geometry) => geometry is PathGeometry path ? path.Figures.Count : 0;

    [Fact]
    public void AnArrow_IsAShaftAndOneSolidHead()
    {
        Figures(DiagramEdgeGeometry.Solid(From, To, EdgeKind.Arrow)).ShouldBe(2);
        Figures(DiagramEdgeGeometry.Hollow(From, To, EdgeKind.Arrow)).ShouldBe(0);
    }

    [Fact]
    public void AnOpenEdge_IsAShaftAndNothingElse()
    {
        Figures(DiagramEdgeGeometry.Solid(From, To, EdgeKind.Open)).ShouldBe(1);
        Figures(DiagramEdgeGeometry.Hollow(From, To, EdgeKind.Open)).ShouldBe(0);
    }

    [Theory]
    [InlineData(EdgeKind.Inheritance)]
    [InlineData(EdgeKind.Aggregation)]
    public void AHollowMarker_IsDrawnOnTheHollowLayerOnly(EdgeKind kind)
    {
        Figures(DiagramEdgeGeometry.Solid(From, To, kind)).ShouldBe(1); // the shaft alone
        Figures(DiagramEdgeGeometry.Hollow(From, To, kind)).ShouldBe(1);
    }

    [Fact]
    public void ACompositionDiamond_IsFilled_SoItReadsApartFromAggregation()
    {
        Figures(DiagramEdgeGeometry.Solid(From, To, EdgeKind.Composition)).ShouldBe(2);
        Figures(DiagramEdgeGeometry.Hollow(From, To, EdgeKind.Composition)).ShouldBe(0);
    }

    [Fact]
    public void ADependency_IsAnArrow_AndTheDashComesFromTheDashConverter()
    {
        Figures(DiagramEdgeGeometry.Solid(From, To, EdgeKind.Dependency)).ShouldBe(2);
        new EdgeDashConverter().Convert(EdgeKind.Dependency, typeof(DoubleCollection), null, CultureInfo.InvariantCulture)
            .ShouldBeOfType<DoubleCollection>().ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(EdgeKind.OneToOne, 3)]   // shaft + a bar at each end
    [InlineData(EdgeKind.OneToMany, 5)]  // shaft + a bar + a three-pronged crow's foot
    [InlineData(EdgeKind.ManyToOne, 5)]
    [InlineData(EdgeKind.ManyToMany, 7)] // shaft + two crow's feet
    public void ACardinality_MarksBothEnds(EdgeKind kind, int expectedFigures)
    {
        Figures(DiagramEdgeGeometry.Solid(From, To, kind)).ShouldBe(expectedFigures);
        Figures(DiagramEdgeGeometry.Hollow(From, To, kind)).ShouldBe(0);
    }

    [Fact]
    public void EveryEdgeKind_DrawsSomething()
    {
        foreach (var kind in Enum.GetValues<EdgeKind>())
        {
            var figures = Figures(DiagramEdgeGeometry.Solid(From, To, kind)) +
                Figures(DiagramEdgeGeometry.Hollow(From, To, kind));
            figures.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void TwoNodesAtTheSamePoint_DrawNothing()
    {
        DiagramEdgeGeometry.Solid(From, From, EdgeKind.Arrow).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public void AClassRelationshipsMarker_SitsAtTheFromEnd_AsMermaidReadsIt()
    {
        var hollow = (PathGeometry)DiagramEdgeGeometry.Hollow(From, To, EdgeKind.Inheritance);

        // The triangle's apex is near the From node, not the To node — `Animal <|-- Duck`.
        hollow.Figures[0].StartPoint.X.ShouldBeLessThan((From.X + To.X) / 2);
    }

    [Fact]
    public void AnEdge_StartsClearOfTheNodeBox_SoItsMarkerIsNotBuriedUnderIt()
    {
        // Nodes are drawn over edges: an end held off by less than the box's half-width would hide
        // the marker completely.
        var solid = (PathGeometry)DiagramEdgeGeometry.Solid(From, To, EdgeKind.OneToMany);

        solid.Figures[0].StartPoint.X.ShouldBeGreaterThan(DiagramNodeViewModel.Width / 2);
    }

    [Fact]
    public void ATerminal_HoldsTheEdgeOffByItsOwnRadius_NotTheNodeBoxsHalfWidth()
    {
        var fromBox = (PathGeometry)DiagramEdgeGeometry.Solid(From, To, EdgeKind.Arrow, NodeShape.Rectangle);
        var fromTerminal = (PathGeometry)DiagramEdgeGeometry.Solid(From, To, EdgeKind.Arrow, NodeShape.Terminal);

        // A Terminal is drawn as a circle narrower than the node box, so its edge starts sooner.
        fromTerminal.Figures[0].StartPoint.X.ShouldBeLessThan(fromBox.Figures[0].StartPoint.X);
    }
}
