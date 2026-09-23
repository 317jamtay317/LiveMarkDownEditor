using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Domain;
using Shouldly;
using UI.Controls;
using UI.Core;

namespace UI.Tests.Controls;

/// <summary>
/// The shared harness for the <see cref="FolderPanel"/> tests: builds a virtualized panel over a Folder
/// Tree exactly as FolderPanel.xaml sets it up, finds its rows the way the user reaches them, and presses
/// keys on it. Used by <see cref="FolderPanelTests"/> and <see cref="FolderPanelRenameTests"/>.
/// </summary>
internal static class FolderPanelHarness
{
    /// <summary>The root every test Folder Workspace opens.</summary>
    internal const string Root = @"C:
otes";

    // The tree the panel lists, as "Kind path" lines in order — from its rows or from Folder Entries.
    internal static List<string> Listed(System.Collections.IEnumerable items)
    {
        var lines = new List<string>();
        foreach (var item in items)
        {
            var (entry, children) = item switch
            {
                FolderPanelRow row => (row.Entry, (System.Collections.IEnumerable)row.Children),
                FolderEntry folderEntry => (folderEntry, folderEntry.Children),
                _ => throw new InvalidOperationException($"Unexpected item {item}."),
            };
            lines.Add($"{entry.Kind} {entry.RelativePath}");
            lines.AddRange(Listed(children));
        }

        return lines;
    }

    internal static string NameOf(object item) => item switch
    {
        FolderPanelRow row => row.Name,
        FolderEntry entry => entry.Name,
        _ => throw new InvalidOperationException($"Unexpected item {item}."),
    };

    internal static ScrollViewer ScrollViewerOf(DependencyObject element)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }

            try
            {
                return ScrollViewerOf(child);
            }
            catch (InvalidOperationException)
            {
                // Not under this child; keep looking.
            }
        }

        throw new InvalidOperationException("No ScrollViewer in the panel's template.");
    }

    internal static void Rebuild(FolderPanel panel, params string[] relativePaths)
    {
        panel.Workspace = FolderWorkspace.From(Root, relativePaths);
        Layout(panel);
    }

    /// <summary>Finds a row as the panel left it, without Expanding anything on the way.</summary>
    internal static TreeViewItem ExistingRow(FolderPanel panel, params string[] names)
    {
        ItemsControl parent = panel;
        TreeViewItem? row = null;

        foreach (var name in names)
        {
            var entry = parent.Items.Cast<object>().Single(item => NameOf(item) == name);
            row = (TreeViewItem?)parent.ItemContainerGenerator.ContainerFromItem(entry)
                  ?? throw new InvalidOperationException($"No row was generated for '{name}' — is its Folder Collapsed?");
            parent = row;
        }

        return row ?? throw new ArgumentException("At least one name is required.", nameof(names));
    }

    internal static FolderPanel BuildDeletablePanel(out List<FolderEntry> deleted, params string[] relativePaths)
    {
        var panel = BuildPanel(out _, relativePaths);
        var recorded = new List<FolderEntry>();
        deleted = recorded;
        panel.DeleteCommand = new RecordingCommand(recorded);
        return panel;
    }

    internal static void PressKey(FolderPanel panel, Key key)
    {
        // A key event needs a PresentationSource to come from; a bare HwndSource stands in for the
        // window the panel would sit in.
        using var source = new HwndSource(new HwndSourceParameters("FolderPanelTests") { Width = 1, Height = 1 });

        panel.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
        });
    }

    internal static FolderPanel BuildPanel(out List<FolderEntry> activated, params string[] relativePaths)
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

        // Virtualize and recycle rows exactly as FolderPanel.xaml does. A virtualized row passes
        // through WPF's item-value storage, which clears a plain IsExpanded set while the row is
        // prepared, so a test without it passes where the real panel fails (INV-044).
        VirtualizingPanel.SetIsVirtualizing(panel, true);
        VirtualizingPanel.SetVirtualizationMode(panel, VirtualizationMode.Recycling);

        // A control built in code never enters a window here, so nudge it through initialization to
        // pick up its theme style — without a Template there is no row to click.
        panel.BeginInit();
        panel.EndInit();

        Layout(panel);
        return panel;
    }

    internal static HierarchicalDataTemplate RowTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(FolderEntry.Name)));

        // The name editor Rename File types into, bound as FolderPanel.xaml binds it (INV-082).
        var editor = new FrameworkElementFactory(typeof(TextBox));
        editor.SetBinding(TextBox.TextProperty, new Binding(nameof(FolderPanelRow.NewName))
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

        var header = new FrameworkElementFactory(typeof(StackPanel));
        header.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        header.AppendChild(text);
        header.AppendChild(editor);

        return new HierarchicalDataTemplate(typeof(FolderEntry))
        {
            ItemsSource = new Binding(nameof(FolderEntry.Children)),
            VisualTree = header,
        };
    }

    internal static FolderPanel BuildRenamablePanel(out List<RenameFileRequest> renamed, params string[] relativePaths) =>
        BuildRenamablePanel(out renamed, out _, relativePaths);

    internal static FolderPanel BuildRenamablePanel(
        out List<RenameFileRequest> renamed, out List<FolderEntry> activated, params string[] relativePaths)
    {
        var panel = BuildPanel(out activated, relativePaths);
        var recorded = new List<RenameFileRequest>();
        renamed = recorded;
        panel.RenameCommand = new RecordingRenames(recorded);
        return panel;
    }

    /// <summary>Selects the File row at the given path and presses F2 on it, as the user starts Rename File.</summary>
    internal static TreeViewItem StartEditing(FolderPanel panel, params string[] names)
    {
        var row = Row(panel, names);
        row.IsSelected = true;
        PressKey(panel, Key.F2);
        RowOf(row).IsRenaming.ShouldBeTrue();
        Layout(panel);
        return row;
    }

    internal static FolderPanelRow RowOf(TreeViewItem row) => (FolderPanelRow)row.DataContext;

    internal static TextBox NameEditorOf(TreeViewItem row) =>
        NameEditorOrNull(row) ?? throw new InvalidOperationException("The row has no name editor.");

    internal static TextBox? NameEditorOrNull(DependencyObject element)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is TreeViewItem)
            {
                continue;
            }

            if (child is TextBox editor)
            {
                return editor;
            }

            if (NameEditorOrNull(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    internal static void LoseFocus(TextBox editor, IInputElement? newFocus) =>
        editor.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, editor, newFocus)
        {
            RoutedEvent = Keyboard.LostKeyboardFocusEvent,
        });

    internal static TreeViewItem Row(FolderPanel panel, params string[] names)
    {
        ItemsControl parent = panel;
        TreeViewItem? row = null;

        foreach (var name in names)
        {
            var entry = parent.Items.Cast<object>().Single(item => NameOf(item) == name);
            row = (TreeViewItem?)parent.ItemContainerGenerator.ContainerFromItem(entry)
                  ?? throw new InvalidOperationException($"No row was generated for '{name}'.");

            // Children are realized only under an Expanded row, exactly as the user reaches them.
            row.IsExpanded = true;
            Layout(panel);
            parent = row;
        }

        return row ?? throw new ArgumentException("At least one name is required.", nameof(names));
    }

    internal static DependencyObject HeaderTextOf(TreeViewItem row) =>
        HeaderTextOrNull(row) ?? throw new InvalidOperationException("The row has no header text.");

    internal static TextBlock? HeaderTextOrNull(DependencyObject element)
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

    internal static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(300, 600));
        element.Arrange(new Rect(0, 0, 300, 600));
        element.UpdateLayout();

        // Container generation is queued at Background priority; drain the queue so the rows exist.
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    internal sealed class RecordingCommand(List<FolderEntry> activated) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => activated.Add((FolderEntry)parameter!);
    }

    internal sealed class RecordingRenames(List<RenameFileRequest> renamed) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => renamed.Add((RenameFileRequest)parameter!);
    }
}
