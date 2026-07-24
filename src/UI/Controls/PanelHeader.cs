using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UI.Controls;

/// <summary>
/// A Panel Header: the strip across the top of a Dockable Panel carrying the panel's
/// <see cref="Title"/> beside its Pin Toggle and its Close Button (INV-062). The Pin Toggle runs the
/// <see cref="PinCommand"/> — Unpinning a Docked panel to Auto-Hidden or Pinning an Auto-Hidden one
/// back — and shows an upright pin while <see cref="IsPinned"/>, a sideways one while not. The Close
/// Button runs the <see cref="CloseCommand"/>. Both pass the <see cref="CommandParameter"/> (the
/// panel), and both grey out where the command is unavailable — the Document Pane rule surfaces as a
/// disabled button, never a refused click (INV-063). Presentation-only chrome: the header names and
/// controls a panel's Placement, it never touches any Markdown Document.
/// </summary>
/// <remarks>
/// Authored as a custom Control — a class plus a ResourceDictionary (<c>PanelHeader.xaml</c>) for its
/// look — per the project's Control exception to the zero-code-behind rule, the same pattern as the
/// <see cref="CommandTip"/>. It holds no state of its own: title, pin state, and both commands are
/// bound by the host, so the header is reused unchanged by the Editor Pane, the Source Panel, the
/// Preview Panel, and each Panel Flyout.
/// </remarks>
public sealed class PanelHeader : Control
{
    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(PanelHeader),
        new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="IsPinned"/> dependency property.</summary>
    public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register(
        nameof(IsPinned),
        typeof(bool),
        typeof(PanelHeader),
        new PropertyMetadata(true));

    /// <summary>Identifies the <see cref="PinCommand"/> dependency property.</summary>
    public static readonly DependencyProperty PinCommandProperty = DependencyProperty.Register(
        nameof(PinCommand),
        typeof(ICommand),
        typeof(PanelHeader),
        new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="CloseCommand"/> dependency property.</summary>
    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand),
        typeof(ICommand),
        typeof(PanelHeader),
        new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(PanelHeader),
        new PropertyMetadata(null));

    /// <summary>The panel's title, shown at the header's left (e.g. "SOURCE").</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Whether the panel is pinned — turns the Pin Toggle's glyph upright (pinned) or sideways (unpinned).</summary>
    public bool IsPinned
    {
        get => (bool)GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <summary>The Pin Toggle's command — the Workspace's <c>TogglePinCommand</c> (INV-062).</summary>
    public ICommand? PinCommand
    {
        get => (ICommand?)GetValue(PinCommandProperty);
        set => SetValue(PinCommandProperty, value);
    }

    /// <summary>The Close Button's command — the Workspace's <c>ClosePanelCommand</c> (INV-062).</summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>The parameter both commands receive — the header's <c>DockablePanel</c>.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    static PanelHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PanelHeader),
            new FrameworkPropertyMetadata(typeof(PanelHeader)));
    }
}
