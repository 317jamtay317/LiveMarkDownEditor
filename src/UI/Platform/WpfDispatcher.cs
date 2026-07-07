using System.Windows;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// <see cref="IUiDispatcher"/> implementation that marshals work onto the WPF application's UI
/// thread via its <see cref="System.Windows.Threading.Dispatcher"/>.
/// </summary>
public sealed class WpfDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
