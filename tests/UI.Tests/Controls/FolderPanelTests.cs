using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Domain;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="FolderPanel"/>: double-clicking a File in the Folder Tree activates it at any
/// depth — a File nested under a Folder opens exactly as one at the root does — while a Folder is left
/// to its native Expand/Collapse (INV-043).
/// </summary>
public sealed class FolderPanelTests
{
    private const string Root = @"C:\notes";

    [Fact]
    public void DoubleClicking_AFileAtTheRoot_ActivatesThatFile_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "top.md", "Nested/deep.md");
            var row = Row(panel, "top.md");

            panel.ActivateAt(HeaderTextOf(row));

            activated.Select(entry => entry.RelativePath).ShouldBe(["top.md"]);
        });
    }

    [Fact]
    public void DoubleClicking_AFileInsideAFolder_ActivatesThatFile_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "top.md", "Nested/deep.md");
            var row = Row(panel, "Nested", "deep.md");

            panel.ActivateAt(HeaderTextOf(row));

            activated.Select(entry => entry.RelativePath).ShouldBe(["Nested/deep.md"]);
        });
    }

    [Fact]
    public void DoubleClicking_AFileNestedTwoFoldersDeep_ActivatesThatFile_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "Outer/Inner/buried.md");
            var row = Row(panel, "Outer", "Inner", "buried.md");

            panel.ActivateAt(HeaderTextOf(row));

            activated.Select(entry => entry.RelativePath).ShouldBe(["Outer/Inner/buried.md"]);
        });
    }

    [Fact]
    public void DoubleClicking_AFolder_ActivatesNothing_INV043()
    {
        StaThread.Run(() =>
        {
            var panel = BuildPanel(out var activated, "Nested/deep.md");
            var row = Row(panel, "Nested");

            panel.ActivateAt(HeaderTextOf(row));

            activated.ShouldBeEmpty();
        });
    }

    private static FolderPanel BuildPanel(out List<FolderEntry> activated, params string[] relativePaths)
    {
        var recorded = new List<FolderEntry>();
        activated = recorded;

        var panel = new FolderPanel
        {
            // The real look comes from FolderPanel.xaml; this stands in for it so the tree nests and
            // each row has an inner visual for the click to land on, as a real double-click does.
            ItemTemplate = RowTemplate(),
            ActivateCommand = new RecordingCommand(recorded),
            Workspace = FolderWorkspace.From(Root, relativePaths),
        };

        // A control built in code never enters a window here, so nudge it through initialization to
        // pick up its theme style — without a Template there is no row to click.
        panel.BeginInit();
        panel.EndInit();

        Layout(panel);
        return panel;
    }

    private static HierarchicalDataTemplate RowTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(FolderEntry.Name)));

        return new HierarchicalDataTemplate(typeof(FolderEntry))
        {
            ItemsSource = new Binding(nameof(FolderEntry.Children)),
            VisualTree = text,
        };
    }

    private static TreeViewItem Row(FolderPanel panel, params string[] names)
    {
        ItemsControl parent = panel;
        TreeViewItem? row = null;

        foreach (var name in names)
        {
            var entry = parent.Items.OfType<FolderEntry>().Single(item => item.Name == name);
            row = (TreeViewItem?)parent.ItemContainerGenerator.ContainerFromItem(entry)
                  ?? throw new InvalidOperationException($"No row was generated for '{name}'.");

            // Children are realized only under an Expanded row, exactly as the user reaches them.
            row.IsExpanded = true;
            Layout(panel);
            parent = row;
        }

        return row ?? throw new ArgumentException("At least one name is required.", nameof(names));
    }

    private static DependencyObject HeaderTextOf(TreeViewItem row) =>
        HeaderTextOrNull(row) ?? throw new InvalidOperationException("The row has no header text.");

    private static TextBlock? HeaderTextOrNull(DependencyObject element)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);

            // A nested row is another entry's header, not this one's.
            if (child is TreeViewItem)
            {
                continue;
            }

            if (child is TextBlock text)
            {
                return text;
            }

            if (HeaderTextOrNull(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(300, 600));
        element.Arrange(new Rect(0, 0, 300, 600));
        element.UpdateLayout();

        // Container generation is queued at Background priority; drain the queue so the rows exist.
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    private sealed class RecordingCommand(List<FolderEntry> activated) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => activated.Add((FolderEntry)parameter!);
    }
}
