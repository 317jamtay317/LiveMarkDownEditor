using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="PanelColumn"/>: a hidden panel's column takes no width — even after the user
/// has dragged its Panel Splitter — and showing the panel again returns it to the width it was
/// dragged to (INV-056).
/// </summary>
public sealed class PanelColumnTests
{
    private const double VisibleWidth = 420d;
    private const double WorkspaceWidth = 1000d;
    private const double SplitterWidth = 5d;
    private const double EditingMinimumWidth = 240d;

    private sealed record Workspace(Grid Grid, ColumnDefinition Editor, ColumnDefinition Panel, GridSplitter Splitter);

    [Fact]
    public void AHiddenPanel_TakesNoWidth_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: false);

            workspace.Panel.ActualWidth.ShouldBe(0);
            workspace.Editor.ActualWidth.ShouldBe(WorkspaceWidth - SplitterWidth);
        });
    }

    [Fact]
    public void AShownPanel_TakesItsVisibleWidth_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true);

            workspace.Panel.ActualWidth.ShouldBe(VisibleWidth);
        });
    }

    [Fact]
    public void Hiding_AfterTheSplitterResizedThePanel_StillTakesNoWidth_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true);
            Drag(workspace, horizontalChange: -150);
            workspace.Panel.ActualWidth.ShouldBeGreaterThan(VisibleWidth);

            PanelColumn.SetIsVisible(workspace.Panel, false);
            Layout(workspace.Grid);

            workspace.Panel.ActualWidth.ShouldBe(0);
            workspace.Editor.ActualWidth.ShouldBe(WorkspaceWidth - SplitterWidth);
        });
    }

    [Fact]
    public void Showing_AfterTheSplitterResizedThePanel_RestoresTheDraggedWidth_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true);
            Drag(workspace, horizontalChange: -150);
            var dragged = workspace.Panel.ActualWidth;

            PanelColumn.SetIsVisible(workspace.Panel, false);
            Layout(workspace.Grid);
            PanelColumn.SetIsVisible(workspace.Panel, true);
            Layout(workspace.Grid);

            workspace.Panel.ActualWidth.ShouldBe(dragged);
        });
    }

    [Fact]
    public void Showing_APanelThatWasNeverResized_UsesItsVisibleWidth_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: false);

            PanelColumn.SetIsVisible(workspace.Panel, true);
            Layout(workspace.Grid);

            workspace.Panel.ActualWidth.ShouldBe(VisibleWidth);
        });
    }

    [Fact]
    public void Toggling_AfterAResize_KeepsTheDraggedWidthAcrossEveryRound_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true);
            Drag(workspace, horizontalChange: -150);
            var dragged = workspace.Panel.ActualWidth;

            for (var round = 0; round < 3; round++)
            {
                PanelColumn.SetIsVisible(workspace.Panel, false);
                Layout(workspace.Grid);
                workspace.Panel.ActualWidth.ShouldBe(0);

                PanelColumn.SetIsVisible(workspace.Panel, true);
                Layout(workspace.Grid);
                workspace.Panel.ActualWidth.ShouldBe(dragged);
            }
        });
    }

    [Fact]
    public void TheSplitter_CannotCrushTheEditingArea_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true);

            // Drag far past the editing area's left edge, as a user flinging the splitter does.
            Drag(workspace, horizontalChange: -WorkspaceWidth);

            workspace.Editor.ActualWidth.ShouldBe(EditingMinimumWidth);
        });
    }

    [Fact]
    public void TheSplitter_CannotCrushAShownPanel_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true);

            Drag(workspace, horizontalChange: WorkspaceWidth);

            workspace.Panel.ActualWidth.ShouldBe(PanelColumn.GetMinimumWidth(workspace.Panel));
        });
    }

    [Fact]
    public void AHiddenPanel_KeepsNoMinimumWidth_SoTheColumnTrulyCollapses_INV056()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true);
            workspace.Panel.MinWidth.ShouldBe(PanelColumn.GetMinimumWidth(workspace.Panel));

            PanelColumn.SetIsVisible(workspace.Panel, false);
            Layout(workspace.Grid);

            workspace.Panel.MinWidth.ShouldBe(0);
            workspace.Panel.ActualWidth.ShouldBe(0);
        });
    }

    /// <summary>
    /// A Workspace of the shape MainWindow uses: a star-sized editing area that never shrinks below its
    /// minimum, a Panel Splitter, and the Panel Column the behaviour drives.
    /// </summary>
    private static Workspace BuildWorkspace(bool isPanelVisible)
    {
        var grid = new Grid { Width = WorkspaceWidth, Height = 400 };
        var editor = new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
            MinWidth = EditingMinimumWidth,
        };
        var splitterColumn = new ColumnDefinition { Width = GridLength.Auto };
        var panel = new ColumnDefinition();
        grid.ColumnDefinitions.Add(editor);
        grid.ColumnDefinitions.Add(splitterColumn);
        grid.ColumnDefinitions.Add(panel);

        var splitter = new GridSplitter
        {
            Width = SplitterWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ResizeDirection = GridResizeDirection.Columns,
        };
        Grid.SetColumn(splitter, 1);
        grid.Children.Add(splitter);

        PanelColumn.SetVisibleWidth(panel, VisibleWidth);
        PanelColumn.SetIsVisible(panel, isPanelVisible);
        Layout(grid);
        return new Workspace(grid, editor, panel, splitter);
    }

    /// <summary>Drags the Panel Splitter the way the user does — a negative change widens the panel.</summary>
    private static void Drag(Workspace workspace, double horizontalChange)
    {
        workspace.Splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        workspace.Splitter.RaiseEvent(new DragDeltaEventArgs(horizontalChange, 0) { RoutedEvent = Thumb.DragDeltaEvent });
        workspace.Splitter.RaiseEvent(new DragCompletedEventArgs(horizontalChange, 0, canceled: false)
        {
            RoutedEvent = Thumb.DragCompletedEvent,
        });
        Layout(workspace.Grid);
    }

    private static void Layout(Grid grid)
    {
        grid.Measure(new Size(WorkspaceWidth, 400));
        grid.Arrange(new Rect(0, 0, WorkspaceWidth, 400));
        grid.UpdateLayout();
    }
}
