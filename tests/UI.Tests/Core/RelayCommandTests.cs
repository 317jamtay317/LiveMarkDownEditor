using Shouldly;
using UI.Core;
using Xunit;

namespace UI.Tests.Core;

/// <summary>
/// Tests for <see cref="RelayCommand"/> — the can-execute predicate it delegates to, and the
/// requery its owner raises when the state behind that predicate changes.
/// </summary>
public sealed class RelayCommandTests
{
    [Fact]
    public void CanExecute_WithoutPredicate_IsAlwaysTrue()
    {
        var command = new RelayCommand(() => { });

        command.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void CanExecute_DelegatesToThePredicate()
    {
        var allowed = false;
        var command = new RelayCommand(() => { }, () => allowed);

        command.CanExecute(null).ShouldBeFalse();

        allowed = true;

        command.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void RaiseCanExecuteChanged_NotifiesSubscribers()
    {
        var command = new RelayCommand(() => { });
        var notified = 0;
        command.CanExecuteChanged += (_, _) => notified++;

        command.RaiseCanExecuteChanged();

        notified.ShouldBe(1);
    }

    [Fact]
    public void RaiseCanExecuteChanged_AfterUnsubscribing_DoesNotNotify()
    {
        var command = new RelayCommand(() => { });
        var notified = 0;
        EventHandler handler = (_, _) => notified++;
        command.CanExecuteChanged += handler;
        command.CanExecuteChanged -= handler;

        command.RaiseCanExecuteChanged();

        notified.ShouldBe(0);
    }
}
