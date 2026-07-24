using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Workspace's Page View and Page Setup surface: the Document Sheet toggle (INV-058) and the one
/// editor-wide Page Setup — orientation and Print Margins — persisted across runs and obeyed by the
/// Sheet, the Print Preview, and the printout alike (INV-061).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private bool _isPageViewEnabled = true;
    private PageSetup _pageSetup = PageSetup.Default;

    /// <summary>
    /// Whether Page View is on — the Visual Document laid out on a fixed-width Document Sheet floating
    /// on a canvas, confining every element (tables included) to one page width. On by default.
    /// Presentation-only: toggling it never changes any Markdown Document (INV-058).
    /// </summary>
    public bool IsPageViewEnabled
    {
        get => _isPageViewEnabled;
        private set => Set(ref _isPageViewEnabled, value);
    }

    /// <summary>
    /// The one editor-wide Page Setup — the Page Orientation together with the Print Margins — obeyed
    /// by the Document Sheet, the Print Preview, and the printout alike. Restored from its store at
    /// construction and persisted on every change. Presentation-and-output only: changing it never
    /// changes any Markdown Document (INV-061).
    /// </summary>
    public PageSetup PageSetup
    {
        get => _pageSetup;
        private set
        {
            if (Set(ref _pageSetup, value))
            {
                Raise(nameof(MarginPreset));
            }
        }
    }

    /// <summary>
    /// The Margin Preset the current Print Margins stand for — a named preset, or Custom when they
    /// match none. Derived from <see cref="PageSetup"/>, so the menu's check state can never disagree
    /// with the margins (INV-061).
    /// </summary>
    public MarginPreset MarginPreset => PrintMargins.PresetOf(_pageSetup.Margins);

    /// <summary>Turns Page View on if it is off, or off if it is on (INV-058).</summary>
    public ICommand TogglePageViewCommand { get; }

    /// <summary>Turns the Page to the given Page Orientation, keeping the margins (INV-061). Parameter: the orientation.</summary>
    public ICommand SetPageOrientationCommand { get; }

    /// <summary>Sets the Print Margins to a named Margin Preset's (INV-061). Parameter: the preset; Custom is ignored.</summary>
    public ICommand SetMarginPresetCommand { get; }

    /// <summary>Asks for custom Print Margins through the Custom Margins Prompt; dismissing it changes nothing (INV-061).</summary>
    public ICommand EditCustomMarginsCommand { get; }

    private void TogglePageView() => IsPageViewEnabled = !IsPageViewEnabled;

    private Task SetPageOrientationAsync(PageOrientation orientation) =>
        ApplyPageSetupAsync(new PageSetup(orientation, PageSetup.Margins));

    private Task SetMarginPresetAsync(MarginPreset preset) =>
        preset == MarginPreset.Custom
            ? Task.CompletedTask // Custom has no fixed margins; EditCustomMarginsCommand is the way there.
            : ApplyPageSetupAsync(new PageSetup(PageSetup.Orientation, PrintMargins.For(preset)));

    private Task EditCustomMarginsAsync()
    {
        var answer = _customMarginsPrompt.Ask(PageSetup.Margins);
        return answer is null
            ? Task.CompletedTask // Dismissing the prompt changes nothing (INV-061).
            : ApplyPageSetupAsync(new PageSetup(PageSetup.Orientation, answer));
    }

    // Every Page Setup change lands here: apply it to the one editor-wide setup and persist it, so the
    // next run opens exactly as this one looks (INV-061).
    private async Task ApplyPageSetupAsync(PageSetup setup)
    {
        if (setup == PageSetup)
        {
            return;
        }

        PageSetup = setup;
        await _pageSetupStore.SaveAsync(setup).ConfigureAwait(true);
    }
}
