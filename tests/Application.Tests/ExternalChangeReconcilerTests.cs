using Application;
using Shouldly;
using Xunit;

namespace Application.Tests;

/// <summary>
/// Tests for <see cref="ExternalChangeReconciler"/>, covering INV-006 (unsaved edits raise a
/// Conflict) and INV-007 (a clean session reloads live).
/// </summary>
public sealed class ExternalChangeReconcilerTests
{
    [Fact]
    public void Reconcile_WithUnsavedEdits_RaisesConflict_INV006()
    {
        ExternalChangeReconciler.Reconcile(hasUnsavedEdits: true)
            .ShouldBe(ExternalChangeResolution.RaiseConflict);
    }

    [Fact]
    public void Reconcile_WithNoUnsavedEdits_ReloadsFromDisk_INV007()
    {
        ExternalChangeReconciler.Reconcile(hasUnsavedEdits: false)
            .ShouldBe(ExternalChangeResolution.ReloadFromDisk);
    }
}
