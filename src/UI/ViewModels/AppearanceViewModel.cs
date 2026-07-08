using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// Exposes the application's visual theme to the UI: whether the dark theme is active, and a command
/// to toggle between light and dark. Backed by the <see cref="IThemeService"/>. Appearance is UI
/// chrome, not part of the editing domain, so it lives outside the ubiquitous language.
/// </summary>
public sealed class AppearanceViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    /// <summary>Creates the appearance ViewModel over the given theme service.</summary>
    /// <param name="themeService">The service that applies and toggles the visual theme.</param>
    public AppearanceViewModel(IThemeService themeService)
    {
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _themeService.ThemeChanged += OnThemeChanged;
        ToggleThemeCommand = new RelayCommand(_themeService.Toggle);
    }

    /// <summary>Whether the dark theme is currently applied.</summary>
    public bool IsDarkTheme => _themeService.Current == AppTheme.Dark;

    /// <summary>Toggles between the light and dark themes.</summary>
    public ICommand ToggleThemeCommand { get; }

    private void OnThemeChanged(object? sender, EventArgs e) => Raise(nameof(IsDarkTheme));
}
