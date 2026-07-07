namespace Application;

/// <summary>How an External Change to the Watched File should be resolved against the Editor Session.</summary>
public enum ExternalChangeResolution
{
    /// <summary>Apply the on-disk contents to the session (the session had no unsaved edits).</summary>
    ReloadFromDisk,

    /// <summary>Surface a Conflict for the user to resolve (the session had unsaved edits).</summary>
    RaiseConflict,
}

/// <summary>
/// Decides how an External Change should be reconciled with the Editor Session, enforcing INV-006
/// and INV-007: a clean session reloads live, while a session with unsaved edits raises a Conflict
/// rather than silently discarding either side.
/// </summary>
public static class ExternalChangeReconciler
{
    /// <summary>Decides the resolution for an External Change given the session's edit state.</summary>
    /// <param name="hasUnsavedEdits">Whether the Editor Session holds edits not yet persisted.</param>
    /// <returns>
    /// <see cref="ExternalChangeResolution.RaiseConflict"/> when there are unsaved edits (INV-006);
    /// otherwise <see cref="ExternalChangeResolution.ReloadFromDisk"/> (INV-007).
    /// </returns>
    public static ExternalChangeResolution Reconcile(bool hasUnsavedEdits) =>
        hasUnsavedEdits
            ? ExternalChangeResolution.RaiseConflict
            : ExternalChangeResolution.ReloadFromDisk;
}
