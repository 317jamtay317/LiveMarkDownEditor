using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace UI.Controls;

/// <summary>
/// An attached behaviour that moves keyboard focus to an element each time a ViewModel asks for it: bind
/// <c>Request</c> to a property the ViewModel sets to a new object whenever the element should take focus.
/// It is how a new File opens ready to type into, as VS Code's editor does (INV-086, INV-087).
/// </summary>
/// <remarks>
/// Focusing a specific view element is view-interaction logic, so it lives in a behaviour rather than a
/// View's code-behind — the same reasoning as <see cref="FocusOnVisible"/>.
/// </remarks>
public static class FocusOnRequest
{
    /// <summary>Identifies the <c>Request</c> attached property.</summary>
    public static readonly DependencyProperty RequestProperty = DependencyProperty.RegisterAttached(
        "Request",
        typeof(object),
        typeof(FocusOnRequest),
        new PropertyMetadata(null, OnRequestChanged));

    /// <summary>Asks for <paramref name="element"/> to take focus; each new object is a new request.</summary>
    /// <param name="element">The element to focus.</param>
    /// <param name="value">A new object to ask again, or <see langword="null"/> for no request.</param>
    public static void SetRequest(DependencyObject element, object? value) => element.SetValue(RequestProperty, value);

    /// <summary>Gets the last request made of <paramref name="element"/>.</summary>
    /// <param name="element">The element to query.</param>
    /// <returns>The last request, or <see langword="null"/> when there is none.</returns>
    public static object? GetRequest(DependencyObject element) => element.GetValue(RequestProperty);

    private static void OnRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is null || d is not UIElement element)
        {
            return;
        }

        // Once everything the request came with has settled: the new Tab laid out and shown, and the Folder
        // Panel done revealing its row (both at Loaded priority). Asking any sooner lets them take focus
        // back.
        element.Dispatcher.BeginInvoke(() =>
        {
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(element), element);
            element.Focus();
        }, DispatcherPriority.ContextIdle);
    }
}
