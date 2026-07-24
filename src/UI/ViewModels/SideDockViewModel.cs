using System.ComponentModel;
using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Side Dock: the tabbed container along the left edge of the Workspace that hosts the Folder Panel
/// and the Navigation Panel as tabs, so the two navigation panels need not each take a column of their
/// own. It shows a tab for each panel that is open and Docked, presents the single
/// <see cref="SelectedTab"/> at a time, and is itself shown only while at least one tab is (INV-046).
/// An Auto-Hidden panel keeps its toggle on but leaves the strip for the left Auto-Hide Bar
/// (<see cref="SetAutoHidden"/>, pushed by the Workspace — INV-062). It owns the Navigation Panel's
/// open state and coordinates with the <see cref="FolderWorkspaceViewModel"/>, which owns the Folder
/// Panel's — docking a panel and selecting a tab are presentation-only and change no document. It is
/// composed as a child of the <see cref="WorkspaceViewModel"/>, alongside the Appearance, Export, and
/// Folder ViewModels.
/// </summary>
public sealed class SideDockViewModel : ObservableObject, IDisposable
{
    private readonly FolderWorkspaceViewModel _folder;
    private bool _isNavigationPanelOpen;
    private bool _isFolderAutoHidden;
    private bool _isNavigationAutoHidden;
    private bool _isWidthCollapsed;
    private SideDockTab _selectedTab = SideDockTab.Folder;

    /// <summary>Creates the Side Dock over the Folder Workspace whose Folder Panel it docks.</summary>
    /// <param name="folder">The Folder Workspace shell; the Side Dock observes its Folder Panel visibility.</param>
    public SideDockViewModel(FolderWorkspaceViewModel folder)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        _folder.PropertyChanged += OnFolderPropertyChanged;

        ToggleNavigationPanelCommand = new RelayCommand(() => SetNavigationOpen(!_isNavigationPanelOpen));
        SelectFolderTabCommand = new RelayCommand(() => Select(SideDockTab.Folder));
        SelectNavigationTabCommand = new RelayCommand(() => Select(SideDockTab.Navigation));
    }

    /// <summary>
    /// Whether the Folder Panel's tab is shown in the strip — true while the Folder Panel is toggled
    /// on (owned by the <see cref="FolderWorkspaceViewModel"/>) and not Auto-Hidden (INV-046, INV-062).
    /// </summary>
    public bool IsFolderTabVisible => _folder.IsFolderPanelVisible && !_isFolderAutoHidden;

    /// <summary>
    /// Whether the Navigation Panel is open — toggled on, wherever it stands (Docked or Auto-Hidden).
    /// Hidden until the user toggles it on; toggling it is presentation-only (INV-012/INV-046).
    /// </summary>
    public bool IsNavigationPanelOpen => _isNavigationPanelOpen;

    /// <summary>
    /// Whether the Navigation Panel's tab is shown in the strip — true while the panel is open and
    /// not Auto-Hidden (INV-046, INV-062).
    /// </summary>
    public bool IsNavigationTabVisible => _isNavigationPanelOpen && !_isNavigationAutoHidden;

    /// <summary>
    /// Whether at least one of the Side Dock's tabs is shown in the strip — the dock's intent, before
    /// any width-driven collapse (INV-059). <see cref="IsVisible"/> combines it with the width-collapse.
    /// </summary>
    public bool HasVisibleTab => IsFolderTabVisible || IsNavigationTabVisible;

    /// <summary>
    /// Whether the Side Dock is shown — true while at least one of its tabs is shown
    /// (<see cref="HasVisibleTab"/>) and the Workspace has not collapsed it for want of width
    /// (INV-046, INV-059).
    /// </summary>
    public bool IsVisible => HasVisibleTab && !_isWidthCollapsed;

    /// <summary>The tab whose panel is currently presented. Only meaningful while <see cref="IsVisible"/>.</summary>
    public SideDockTab SelectedTab
    {
        get => _selectedTab;
        private set => Set(ref _selectedTab, value);
    }

    /// <summary>Shows the Navigation Panel if hidden, or hides it if shown (selecting it when shown).</summary>
    public ICommand ToggleNavigationPanelCommand { get; }

    /// <summary>Selects the Folder Panel's tab, when it is shown.</summary>
    public ICommand SelectFolderTabCommand { get; }

    /// <summary>Selects the Navigation Panel's tab, when it is shown.</summary>
    public ICommand SelectNavigationTabCommand { get; }

    /// <summary>
    /// Closes the Navigation Panel — the Panel Header's Close Button path, reached from the
    /// Workspace's <c>ClosePanelCommand</c> wherever the panel stands (INV-062). Its Command Bar
    /// toggle reopens it.
    /// </summary>
    public void CloseNavigationPanel() => SetNavigationOpen(false);

    /// <summary>
    /// Takes a tab out of the strip while its panel is Auto-Hidden, or returns it when the panel is
    /// Pinned back — shown and Selected again (INV-062). The panel's own toggle is untouched: an
    /// Auto-Hidden panel is open, merely off the layout. The Workspace drives this from the Panel
    /// Chrome state.
    /// </summary>
    /// <param name="tab">The tab whose panel is Auto-Hidden or re-Pinned.</param>
    /// <param name="value"><see langword="true"/> while the tab's panel is Auto-Hidden.</param>
    public void SetAutoHidden(SideDockTab tab, bool value)
    {
        var stripBefore = IsTabVisible(tab);
        if (tab == SideDockTab.Folder)
        {
            if (_isFolderAutoHidden == value)
            {
                return;
            }

            _isFolderAutoHidden = value;
        }
        else
        {
            if (_isNavigationAutoHidden == value)
            {
                return;
            }

            _isNavigationAutoHidden = value;
        }

        RefreshStrip(tab, stripBefore);
    }

    /// <summary>
    /// Collapses or restores the Side Dock for width: while collapsed the dock is hidden even though its
    /// tabs stay toggled on, so a narrow Workspace never hides the editor behind it and widening restores
    /// the dock exactly as it was (INV-059). The Workspace drives this from the measured width.
    /// </summary>
    /// <param name="value"><see langword="true"/> to collapse the dock for width; <see langword="false"/> to restore it.</param>
    public void SetWidthCollapsed(bool value)
    {
        if (_isWidthCollapsed != value)
        {
            _isWidthCollapsed = value;
            Raise(nameof(IsVisible));
        }
    }

    /// <summary>Unsubscribes from the Folder Workspace's change notifications.</summary>
    public void Dispose() => _folder.PropertyChanged -= OnFolderPropertyChanged;

    private void SetNavigationOpen(bool value)
    {
        if (_isNavigationPanelOpen == value)
        {
            return;
        }

        var stripBefore = IsNavigationTabVisible;
        _isNavigationPanelOpen = value;
        Raise(nameof(IsNavigationPanelOpen));
        RefreshStrip(SideDockTab.Navigation, stripBefore);
    }

    private void Select(SideDockTab tab)
    {
        // The tab headers only offer a shown tab, but guard anyway so selection can never point at a
        // hidden panel.
        if (IsTabVisible(tab))
        {
            SelectedTab = tab;
        }
    }

    private void OnFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FolderWorkspaceViewModel.IsFolderPanelVisible))
        {
            Raise(nameof(IsFolderTabVisible));
            OnTabVisibilityChanged(SideDockTab.Folder, IsFolderTabVisible);
        }
    }

    /// <summary>Re-raises a tab's strip visibility and reconciles the selection when it changed.</summary>
    private void RefreshStrip(SideDockTab tab, bool stripBefore)
    {
        Raise(tab == SideDockTab.Folder ? nameof(IsFolderTabVisible) : nameof(IsNavigationTabVisible));
        var stripNow = IsTabVisible(tab);
        if (stripNow != stripBefore)
        {
            OnTabVisibilityChanged(tab, stripNow);
        }
    }

    /// <summary>
    /// Keeps the Selected tab and the dock's visibility in step when a tab is shown or hidden: a tab
    /// shown becomes the Selected one; hiding the Selected tab falls back to the other if it is still
    /// shown, and otherwise leaves the dock hidden (INV-046).
    /// </summary>
    private void OnTabVisibilityChanged(SideDockTab tab, bool nowVisible)
    {
        if (nowVisible)
        {
            SelectedTab = tab;
        }
        else if (SelectedTab == tab)
        {
            var other = tab == SideDockTab.Folder ? SideDockTab.Navigation : SideDockTab.Folder;
            if (IsTabVisible(other))
            {
                SelectedTab = other;
            }
        }

        Raise(nameof(HasVisibleTab));
        Raise(nameof(IsVisible));
    }

    private bool IsTabVisible(SideDockTab tab) =>
        tab == SideDockTab.Folder ? IsFolderTabVisible : IsNavigationTabVisible;
}
