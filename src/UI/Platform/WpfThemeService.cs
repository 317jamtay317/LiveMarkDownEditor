using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// WPF adapter for <see cref="IThemeService"/>. Applies a theme by swapping the active palette
/// <see cref="ResourceDictionary"/> in the application's merged dictionaries. The control styles in
/// <c>Themes/Controls.xaml</c> reference the palette's brushes via <c>DynamicResource</c>, so
/// swapping the palette recolours the whole UI live. The window's non-client area (title bar) is not
/// covered by WPF styling, so it is themed here via the DWM immersive-dark-mode attribute.
/// </summary>
public sealed class WpfThemeService : IThemeService
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private static readonly Uri LightPalette = new("/UI;component/Themes/Palette.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkPalette = new("/UI;component/Themes/Palette.Dark.xaml", UriKind.Relative);

    private ResourceDictionary? _currentPalette;

    /// <inheritdoc />
    public AppTheme Current { get; private set; } = AppTheme.Light;

    /// <inheritdoc />
    public event EventHandler? ThemeChanged;

    /// <inheritdoc />
    public void Apply(AppTheme theme)
    {
        // Fully qualified: the UI project references the "Application" project, so the bare name
        // "Application" binds to that namespace rather than System.Windows.Application.
        var application = System.Windows.Application.Current;
        if (application is null)
        {
            return;
        }

        var next = new ResourceDictionary
        {
            Source = theme == AppTheme.Dark ? DarkPalette : LightPalette,
        };

        var dictionaries = application.Resources.MergedDictionaries;
        if (_currentPalette is not null)
        {
            dictionaries.Remove(_currentPalette);
        }

        // Insert the palette first so the control styles' DynamicResource lookups resolve its brushes.
        dictionaries.Insert(0, next);
        _currentPalette = next;

        Current = theme;
        ApplyTitleBar(application, theme);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Toggle() => Apply(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Darkens or lightens each open window's title bar to match the theme.</summary>
    private static void ApplyTitleBar(System.Windows.Application application, AppTheme theme)
    {
        var useDark = theme == AppTheme.Dark ? 1 : 0;
        foreach (Window window in application.Windows)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
            }
        }
    }
}
