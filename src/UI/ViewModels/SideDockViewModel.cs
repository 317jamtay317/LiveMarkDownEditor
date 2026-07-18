using System.ComponentModel;
using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Side Dock: the tabbed container along the left edge of the Workspace that hosts the Folder Panel
/// and the Navigation Panel as tabs, so the two navigation panels need not each take a column of their
/// own. It shows a tab for each panel toggled on, presents the single <see cref="SelectedTab"/> at a
/// time, and is itself shown only while at least one of its panels is toggled on (INV-046). It owns the
/// Navigation Panel's tab visibility and coordinates with the <see cref="FolderWorkspaceViewModel"/>,
/// which owns the Folder Panel's — docking a panel and selecting a tab are presentation-only and change
/// no document. It is composed as a child of the <see cref="WorkspaceViewModel"/>, alongside the
/// Appearance, Export, and Folder ViewModels.
/// </summary>
public sealed class SideDockViewModel : ObservableObject, IDisposable
{
    private readonly FolderWorkspaceViewModel _folder;
    private bool _isNavigationTabVisible;
    private SideDockTab _selectedTab = SideDockTab.Folder;

    /// <summary>Creates the Side Dock over the Folder Workspace whose Folder Panel it docks.</summary>
    /// <param name="folder">The Folder Workspace shell; the Side Dock observes its Folder Panel visibility.</param>
    public SideDockViewModel(FolderWorkspaceViewModel folder)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        _folder.PropertyChanged += OnFolderPropertyChanged;

        ToggleNavigationPanelCommand = new RelayCommand(ToggleNavigationPanel);
        SelectFolderTabCommand = new RelayCommand(() => Select(SideDockTab.Folder));
        SelectNavigationTabCommand = new RelayCommand(() => Select(SideDockTab.Navigation));
    }

    /// <summary>
    /// Whether the Folder Panel's tab is shown — true exactly while a Folder Workspace's Folder Panel
    /// is toggled on. Owned by the <see cref="FolderWorkspaceViewModel"/>; the Side Dock mirrors it.
    /// </summary>
    public bool IsFolderTabVisible => _folder.IsFolderPanelVisible;

    /// <summary>
    /// Whether the Navigation Panel's tab is shown. Hidden until the user toggles it on; toggling it is
    /// presentation-only (INV-012/INV-046).
    /// </summary>
    public bool IsNavigationTabVisible
    {
        get => _isNavigationTabVisible;
        private set
        {
            if (Set(ref _isNavigationTabVisible, value))
            {
                OnTabVisibilityChanged(SideDockTab.Navigation, value);
            }
        }
    }

    /// <summary>Whether the Side Dock is shown at all — true exactly while at least one of its tabs is shown.</summary>
    public bool IsVisible => IsFolderTabVisible || IsNavigationTabVisible;

    /// <summary>The tab whose panel is currently presented. Only meaningful while <see cref="IsVisible"/>.</summary>
    public SideDockTab SelectedTab
    {
        get => _selectedTab;
        private set => Set(ref _selectedTab, value);
    }

    /// <summary>Shows the Navigation Panel's tab if hidden, or hides it if shown (selecting it when shown).</summary>
    public ICommand ToggleNavigationPanelCommand { get; }

    /// <summary>Selects the Folder Panel's tab, when it is shown.</summary>
    public ICommand SelectFolderTabCommand { get; }

    /// <summary>Selects the Navigation Panel's tab, when it is shown.</summary>
    public ICommand SelectNavigationTabCommand { get; }

    /// <summary>Unsubscribes from the Folder Workspace's change notifications.</summary>
    public void Dispose() => _folder.PropertyChanged -= OnFolderPropertyChanged;

    private void ToggleNavigationPanel() => IsNavigationTabVisible = !IsNavigationTabVisible;

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
            OnTabVisibilityChanged(SideDockTab.Folder, _folder.IsFolderPanelVisible);
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

        Raise(nameof(IsVisible));
    }

    private bool IsTabVisible(SideDockTab tab) =>
        tab == SideDockTab.Folder ? IsFolderTabVisible : IsNavigationTabVisible;
}
