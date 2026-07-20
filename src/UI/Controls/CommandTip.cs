using System.Windows;
using System.Windows.Controls;

namespace UI.Controls;

/// <summary>
/// A Command Tip: the themed tooltip shown on hovering a command control — a Command Bar action, or
/// any of the editor's command buttons. In place of a bare one-line hint it names the action and
/// explains it: the action's <see cref="Heading"/> (the Name its Command Icon carries), a short
/// <see cref="Detail"/> line saying what the action does, and — when the action has one — its
/// <see cref="Gesture"/> (key gesture), shown as a small chip. A Command Tip is presentation-only: it
/// names and explains an action, it never performs one, and it follows the active light/dark theme.
/// </summary>
/// <remarks>
/// Authored as a custom Control — a <see cref="ToolTip"/> subclass plus a ResourceDictionary
/// (<c>CommandTip.xaml</c>) for its themed look — per the project's Control exception to the
/// zero-code-behind rule. WPF's stock ToolTip is a pale, system-drawn popup that ignores the palette;
/// deriving from it lets a Command Tip both carry structured content and paint itself from the active
/// palette (DynamicResource), the same reason <c>Themes/Controls.xaml</c> re-styles the stock TextBox
/// and ComboBox. Set one as an element's tooltip and fill in its three properties — the gesture chip
/// and the detail line collapse when left empty:
/// <code>
/// &lt;Button.ToolTip&gt;
///     &lt;controls:CommandTip Heading="Bold"
///                          Detail="Make the selected text bold, or turn it back to normal."
///                          Gesture="Ctrl+B" /&gt;
/// &lt;/Button.ToolTip&gt;
/// </code>
/// </remarks>
public sealed class CommandTip : ToolTip
{
    /// <summary>Identifies the <see cref="Heading"/> dependency property.</summary>
    public static readonly DependencyProperty HeadingProperty = DependencyProperty.Register(
        nameof(Heading),
        typeof(string),
        typeof(CommandTip),
        new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Detail"/> dependency property.</summary>
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail),
        typeof(string),
        typeof(CommandTip),
        new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Gesture"/> dependency property.</summary>
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture),
        typeof(string),
        typeof(CommandTip),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// The action's Name — what it is (e.g. "Bold"). Shown as the tip's heading, the same Name the
    /// action's Command Icon carries and that a screen reader reads.
    /// </summary>
    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>
    /// A short line saying what the action does, shown beneath the <see cref="Heading"/>. Collapses
    /// when empty.
    /// </summary>
    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>
    /// The action's key gesture (e.g. "Ctrl+B"), shown as a chip beside the <see cref="Heading"/>.
    /// Empty for an action with no gesture, in which case the chip collapses.
    /// </summary>
    public string Gesture
    {
        get => (string)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }
}
