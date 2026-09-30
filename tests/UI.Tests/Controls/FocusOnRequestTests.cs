using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the <see cref="FocusOnRequest"/> attached behavior: each new request moves focus to the
/// element — so a new File opens ready to type into (INV-086, INV-087) — and taking a request back does
/// nothing. Asserted on logical focus within the element's focus scope, which needs no window on screen.
/// </summary>
public sealed class FocusOnRequestTests
{
    [Fact]
    public void ARequest_FocusesTheElement_INV086()
    {
        StaThread.Run(() =>
        {
            var (scope, editor, other) = Build();
            FocusManager.SetFocusedElement(scope, other);

            FocusOnRequest.SetRequest(editor, new object());
            Drain();

            FocusManager.GetFocusedElement(scope).ShouldBeSameAs(editor);
        });
    }

    [Fact]
    public void EachNewRequest_FocusesItAgain_INV086()
    {
        StaThread.Run(() =>
        {
            var (scope, editor, other) = Build();
            FocusOnRequest.SetRequest(editor, new object());
            Drain();
            FocusManager.SetFocusedElement(scope, other);

            FocusOnRequest.SetRequest(editor, new object());
            Drain();

            FocusManager.GetFocusedElement(scope).ShouldBeSameAs(editor);
        });
    }

    [Fact]
    public void NoRequest_LeavesFocusWhereItIs_INV086()
    {
        StaThread.Run(() =>
        {
            var (scope, editor, other) = Build();
            FocusOnRequest.SetRequest(editor, new object());
            Drain();
            FocusManager.SetFocusedElement(scope, other);

            FocusOnRequest.SetRequest(editor, null);
            Drain();

            FocusManager.GetFocusedElement(scope).ShouldBeSameAs(other);
        });
    }

    private static (StackPanel Scope, TextBox Editor, TextBox Other) Build()
    {
        var editor = new TextBox();
        var other = new TextBox();
        var scope = new StackPanel { Children = { editor, other } };
        FocusManager.SetIsFocusScope(scope, true);
        return (scope, editor, other);
    }

    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
