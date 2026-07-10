using System.Collections;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// WPF adapter for <see cref="IThemeService"/>. Applies a theme by recolouring a single live palette
/// <see cref="ResourceDictionary"/> merged into the application's resources. The control styles in
/// <c>Themes/Controls.xaml</c> reference the palette's brushes via <c>DynamicResource</c>, so
/// recolouring those brushes repaints the whole UI live. The window's non-client area (title bar) is
/// not covered by WPF styling, so it is themed here via the DWM immersive-dark-mode attribute.
/// </summary>
/// <remarks>
/// The palette brushes are recoloured <em>in place</em> rather than by swapping a merged palette
/// dictionary. Swapping a dictionary raises an application-wide resource invalidation that re-resolves
/// every <c>DynamicResource</c> and re-formats the editor's whole Visual Document — a stall that grows
/// with document size. Mutating each live <see cref="SolidColorBrush"/>'s
/// <see cref="SolidColorBrush.Color"/> instead leaves the brush instances (and the references to them)
/// untouched, so WPF only repaints the elements that use each brush. Theme switching stays instant
/// regardless of document size.
/// <para>
/// A resource brush placed in any <see cref="ResourceDictionary"/> (root or merged) is frozen by WPF
/// so it can be shared, which would make recolouring throw. Each live brush therefore has its
/// <see cref="SolidColorBrush.Color"/> data-bound to a mutable <see cref="PaletteColour"/> holder:
/// a brush with a bound property reports <c>CanFreeze == false</c>, so the dictionary leaves it
/// mutable. Re-theming sets each holder's colour, which the binding pushes into the live brush.
/// </para>
/// </remarks>
public sealed class WpfThemeService : IThemeService
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private static readonly Uri LightPalette = new("/UI;component/Themes/Palette.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkPalette = new("/UI;component/Themes/Palette.Dark.xaml", UriKind.Relative);

    // The colour holders backing each live brush, keyed as in the palette dictionaries. Setting a
    // holder's Colour flows through its binding to recolour the live brush — the whole theming
    // mechanism, with no dictionary swap.
    private Dictionary<object, PaletteColour>? _holders;

    // The live brushes themselves, kept so their (unbound) Opacity can be updated per theme.
    private Dictionary<object, SolidColorBrush>? _liveBrushes;

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

        // Load the target palette's colours. This dictionary is only a source of values — the brushes
        // actually shown are the live ones, recoloured to match.
        var source = new ResourceDictionary
        {
            Source = theme == AppTheme.Dark ? DarkPalette : LightPalette,
        };

        if (_holders is null)
        {
            InstallLivePalette(application, source);
        }
        else
        {
            Recolour(source);
        }

        Current = theme;
        ApplyTitleBar(application, theme);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    // First application: build a live brush per palette brush whose Colour is bound to a mutable holder,
    // and place it in the application resources where the control styles' DynamicResource lookups
    // resolve it. The binding keeps each brush unfreezable, so it stays mutable for later recolouring.
    private void InstallLivePalette(System.Windows.Application application, ResourceDictionary source)
    {
        _holders = [];
        _liveBrushes = [];
        foreach (DictionaryEntry entry in source)
        {
            if (entry.Value is SolidColorBrush brush)
            {
                var holder = new PaletteColour { Colour = brush.Color };
                var live = new SolidColorBrush { Opacity = brush.Opacity };
                BindingOperations.SetBinding(
                    live,
                    SolidColorBrush.ColorProperty,
                    new Binding(nameof(PaletteColour.Colour)) { Source = holder });

                application.Resources[entry.Key] = live;
                _holders[entry.Key] = holder;
                _liveBrushes[entry.Key] = live;
            }
            else
            {
                application.Resources[entry.Key] = entry.Value;
            }
        }
    }

    // Subsequent applications: push each target colour into its holder (the binding recolours the live
    // brush) and update the brush's Opacity. The brush instances — and the DynamicResource references
    // to them — are unchanged, so WPF repaints without a dictionary swap and its tree-wide invalidation.
    private void Recolour(ResourceDictionary source)
    {
        foreach (DictionaryEntry entry in source)
        {
            if (entry.Value is not SolidColorBrush next || !_holders!.TryGetValue(entry.Key, out var holder))
            {
                continue;
            }

            holder.Colour = next.Color;
            _liveBrushes![entry.Key].Opacity = next.Opacity;
        }
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

    // A mutable colour a live brush binds to. Binding the brush's Color to this holder makes the brush
    // unfreezable, so the resource dictionary leaves it mutable; setting Colour recolours the brush.
    private sealed class PaletteColour : DependencyObject
    {
        public static readonly DependencyProperty ColourProperty = DependencyProperty.Register(
            nameof(Colour),
            typeof(Color),
            typeof(PaletteColour),
            new PropertyMetadata(Colors.Transparent));

        public Color Colour
        {
            get => (Color)GetValue(ColourProperty);
            set => SetValue(ColourProperty, value);
        }
    }
}
