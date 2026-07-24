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

    [Fact]
    public void AFillColumn_WhenShown_TakesTheRemainingWidth_INV063()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true, editorIsFillColumn: true);

            // The fill column (the primary Document Pane) takes what the pixel-sized panel leaves.
            workspace.Editor.ActualWidth.ShouldBe(WorkspaceWidth - SplitterWidth - VisibleWidth);
        });
    }

    [Fact]
    public void AFillColumn_WhenHidden_TakesNoWidth_AndTheOtherPaneCanFillInstead_INV063()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true, editorIsFillColumn: true);

            // The Editor Pane leaves the layout; the Source Panel becomes the primary pane and fills.
            PanelColumn.SetIsVisible(workspace.Editor, false);
            PanelColumn.SetFill(workspace.Panel, true);
            Layout(workspace.Grid);

            workspace.Editor.ActualWidth.ShouldBe(0);
            workspace.Editor.MinWidth.ShouldBe(0);
            workspace.Panel.ActualWidth.ShouldBe(WorkspaceWidth - SplitterWidth);
        });
    }

    [Fact]
    public void AFillColumn_KeepsItsMinimumWhileShown_INV063()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true, editorIsFillColumn: true);

            workspace.Editor.MinWidth.ShouldBe(EditingMinimumWidth);
        });
    }

    [Fact]
    public void TurningFillOff_ReturnsTheColumnToItsPanelWidth_INV063()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true, editorIsFillColumn: true);

            // The Source Panel fills while the Editor Pane is away, then returns to its panel width
            // the moment the editor is docked back.
            PanelColumn.SetIsVisible(workspace.Editor, false);
            PanelColumn.SetFill(workspace.Panel, true);
            Layout(workspace.Grid);

            PanelColumn.SetFill(workspace.Panel, false);
            PanelColumn.SetIsVisible(workspace.Editor, true);
            Layout(workspace.Grid);

            workspace.Panel.ActualWidth.ShouldBe(VisibleWidth);
            workspace.Editor.ActualWidth.ShouldBe(WorkspaceWidth - SplitterWidth - VisibleWidth);
        });
    }

    [Fact]
    public void TurningFillOn_AfterASplitterDrag_ThenOffAgain_RestoresTheDraggedWidth_INV063()
    {
        StaThread.Run(() =>
        {
            var workspace = BuildWorkspace(isPanelVisible: true, editorIsFillColumn: true);
            Drag(workspace, horizontalChange: -150);
            var dragged = workspace.Panel.ActualWidth;
            dragged.ShouldBeGreaterThan(VisibleWidth);

            PanelColumn.SetIsVisible(workspace.Editor, false);
            PanelColumn.SetFill(workspace.Panel, true);
            Layout(workspace.Grid);

            PanelColumn.SetFill(workspace.Panel, false);
            PanelColumn.SetIsVisible(workspace.Editor, true);
            Layout(workspace.Grid);

            // The width the user dragged the panel to survives its spell as the fill column.
            workspace.Panel.ActualWidth.ShouldBe(dragged);
        });
    }

    /// <summary>
    /// A Workspace of the shape MainWindow uses: a star-sized editing area that never shrinks below its
    /// minimum, a Panel Splitter, and the Panel Column the behaviour drives. With
    /// <paramref name="editorIsFillColumn"/> the editing column is itself a fill Panel Column (the
    /// closeable Editor Pane of INV-063) rather than a plain star column.
    /// </summary>
    private static Workspace BuildWorkspace(bool isPanelVisible, bool editorIsFillColumn = false)
    {
        var grid = new Grid { Width = WorkspaceWidth, Height = 400 };
        var editor = new ColumnDefinition();
        if (editorIsFillColumn)
        {
            PanelColumn.SetMinimumWidth(editor, EditingMinimumWidth);
            PanelColumn.SetFill(editor, true);
            PanelColumn.SetIsVisible(editor, true);
        }
        else
        {
            editor.Width = new GridLength(1, GridUnitType.Star);
            editor.MinWidth = EditingMinimumWidth;
        }

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
