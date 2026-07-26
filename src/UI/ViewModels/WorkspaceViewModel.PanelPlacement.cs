using System.ComponentModel;

namespace UI.ViewModels;

/// <summary>
/// How a Dockable Panel comes to stand where it does: the toggle, close, pin, and flyout operations
/// the Panel Chrome commands run, the writes back to each panel's owner, and the recomputation that
/// re-resolves every Placement — pins reset on reopen (INV-062), Compact Layout re-resolved over the
/// Docked panels (INV-059, INV-063), and the Panel Layout saved when a Placement actually changed
/// (INV-067).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>Opens a Workspace-owned panel Docked, or closes it — the View Menu toggles' path (INV-062).</summary>
    private void TogglePanel(DockablePanel panel)
    {
        if (ChromeState.Of(panel).IsOpen)
        {
            ClosePanel(panel);
        }
        else
        {
            SetOpen(panel, true);
            SetPinned(panel, true);
            RecomputePanels();
        }
    }

    /// <summary>Closes a panel wherever it stands, resetting its pin so reopening docks it (INV-062, INV-063).</summary>
    private void ClosePanel(DockablePanel panel)
    {
        if (!PanelChrome.CanClose(ChromeState, panel))
        {
            return;
        }

        SetOpen(panel, false);
        SetPinned(panel, true);
        RecomputePanels();
    }

    /// <summary>The Pin Toggle: Docked → Auto-Hidden, Auto-Hidden → Docked (INV-062, INV-063).</summary>
    private void TogglePin(DockablePanel panel)
    {
        var chrome = ChromeState;
        if (PanelChrome.CanUnpin(chrome, panel))
        {
            SetPinned(panel, false);
        }
        else if (PanelChrome.CanPin(chrome, panel))
        {
            SetPinned(panel, true);
        }
        else
        {
            return;
        }

        RecomputePanels();
    }

    /// <summary>Opens an Auto-Hidden panel's Panel Flyout, or dismisses the one already open (INV-062).</summary>
    private void ToggleFlyout(DockablePanel panel)
    {
        if (_flyoutPanel == panel)
        {
            SetFlyout(null);
        }
        else if (PanelChrome.PlacementOf(ChromeState, panel) == PanelPlacement.AutoHidden)
        {
            SetFlyout(panel);
        }
    }

    private void SetFlyout(DockablePanel? panel)
    {
        if (_flyoutPanel != panel)
        {
            _flyoutPanel = panel;
            RaiseFlyoutProperties();
        }
    }

    /// <summary>Writes a panel's open state back to its owner — the Workspace's own flags, the Folder shell, or the Side Dock.</summary>
    private void SetOpen(DockablePanel panel, bool value)
    {
        switch (panel)
        {
            case DockablePanel.EditorPane:
                _isEditorPaneOpen = value;
                break;
            case DockablePanel.SourcePanel:
                _isSourcePanelRequested = value;
                break;
            case DockablePanel.PreviewPanel:
                _isPreviewPanelRequested = value;
                break;
            // The Folder and Navigation panels own their open state; the Workspace closes them from a
            // Panel Header's Close Button (INV-062) and re-opens them when a persisted Panel Layout
            // says the last run left them open (INV-067). Their Command Bar toggles are their own.
            case DockablePanel.FolderPanel:
                if (value)
                {
                    Folder.ShowFolderPanel();
                }
                else
                {
                    Folder.CloseFolderPanel();
                }

                break;
            default:
                if (value)
                {
                    SideDock.OpenNavigationPanel();
                }
                else
                {
                    SideDock.CloseNavigationPanel();
                }

                break;
        }
    }

    private void SetPinned(DockablePanel panel, bool value)
    {
        switch (panel)
        {
            case DockablePanel.EditorPane:
                _isEditorPanePinned = value;
                break;
            case DockablePanel.SourcePanel:
                _isSourcePanelPinned = value;
                break;
            case DockablePanel.PreviewPanel:
                _isPreviewPanelPinned = value;
                break;
            case DockablePanel.FolderPanel:
                _isFolderPanelPinned = value;
                break;
            default:
                _isNavigationPanelPinned = value;
                break;
        }
    }

    // Resolves the whole Panel Chrome: pins reset on reopen (INV-062), the Side Dock told which tabs
    // are Auto-Hidden, Compact Layout re-resolved over the Docked panels (INV-059/063), and a flyout
    // whose panel stopped being Auto-Hidden dismissed. Converges: nested notifications re-enter as
    // no-ops through the guard.
    private void RecomputePanels()
    {
        if (_isRecomputingPanels)
        {
            return;
        }

        _isRecomputingPanels = true;
        var chromeChanged = false;
        try
        {
            // Restoring already put every panel exactly where the layout says; a reopen reset would
            // undo it, pinning back the panels the last run left Auto-Hidden (INV-067).
            if (!_isRestoringPanels)
            {
                ResetPinsOnReopen();
            }

            var chrome = ChromeState;
            chromeChanged = chrome != _lastChrome;
            _lastChrome = chrome;

            SideDock.SetAutoHidden(
                SideDockTab.Folder,
                PanelChrome.PlacementOf(chrome, DockablePanel.FolderPanel) == PanelPlacement.AutoHidden);
            SideDock.SetAutoHidden(
                SideDockTab.Navigation,
                PanelChrome.PlacementOf(chrome, DockablePanel.NavigationPanel) == PanelPlacement.AutoHidden);

            var intent = new PanelIntent(
                SideDock.HasVisibleTab,
                PanelChrome.IsDocked(chrome, DockablePanel.SourcePanel),
                PanelChrome.IsDocked(chrome, DockablePanel.PreviewPanel));
            _resolved = CompactLayout.Resolve(
                _workspaceWidth, intent, PanelChrome.IsDocked(chrome, DockablePanel.EditorPane));
            SideDock.SetWidthCollapsed(intent.Dock && !_resolved.Dock);

            // A flyout only ever shows an Auto-Hidden panel; a placement change takes it with it.
            if (_flyoutPanel is DockablePanel flyout &&
                PanelChrome.PlacementOf(chrome, flyout) != PanelPlacement.AutoHidden)
            {
                _flyoutPanel = null;
            }
        }
        finally
        {
            _isRecomputingPanels = false;
        }

        RaisePanelChrome();

        // Save the layout when a Placement actually changed — not on every recomputation, which a
        // window resize drives on each measured width (INV-059, INV-067).
        if (chromeChanged && _isPanelChromeReady && !_isRestoringPanels)
        {
            PersistPanelLayout();
        }
    }

    // A panel reopened by any toggle comes back Docked, never straight to Auto-Hidden (INV-062): a
    // closed-to-open transition clears a pin left over from before the panel was closed.
    private void ResetPinsOnReopen()
    {
        foreach (var panel in PanelChrome.All)
        {
            var now = ChromeState.Of(panel);
            if (now.IsOpen && !now.IsPinned && !_lastChrome.Of(panel).IsOpen)
            {
                SetPinned(panel, true);
            }
        }
    }

    private void OnSideDockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SideDockViewModel.HasVisibleTab)
            or nameof(SideDockViewModel.IsNavigationPanelOpen)
            or nameof(SideDockViewModel.SelectedTab))
        {
            RecomputePanels();
        }
    }

    private void OnFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FolderWorkspaceViewModel.IsFolderPanelVisible))
        {
            RecomputePanels();
        }
    }
}
