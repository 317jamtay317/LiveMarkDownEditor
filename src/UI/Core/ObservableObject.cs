using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UI.Core;

/// <summary>
/// Base class for ViewModels providing <see cref="INotifyPropertyChanged"/> with a change-detecting
/// <see cref="Set{T}"/> helper.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> when it differs, raising
    /// <see cref="PropertyChanged"/> for the calling property.
    /// </summary>
    /// <typeparam name="T">The property's type.</typeparam>
    /// <param name="field">The backing field, passed by reference.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The property name; supplied automatically by the compiler.</param>
    /// <returns><see langword="true"/> if the value changed; otherwise <see langword="false"/>.</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(propertyName);
        return true;
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for the given property.</summary>
    /// <param name="propertyName">The property name; supplied automatically by the compiler.</param>
    protected void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
