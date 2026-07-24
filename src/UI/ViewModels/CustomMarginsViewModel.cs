using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Custom Margins Prompt's state and behaviour: the four margins entered in inches — the unit a
/// person thinks about a page in — seeded with the current Print Margins, and the decision to accept
/// or dismiss. Accepting is only possible while every value is one a <see cref="PrintMargins"/> may
/// hold, so an invalid Page Setup cannot be asked for; dismissing answers nothing and changes nothing
/// (INV-061, the INV-030 discipline).
/// </summary>
public sealed class CustomMarginsViewModel : INotifyPropertyChanged
{
    // One inch, in device-independent units.
    private const double Inch = 96d;

    /// <summary>Creates the prompt's state, seeded with the current Print Margins.</summary>
    /// <param name="current">The margins the prompt opens showing, in device-independent units.</param>
    public CustomMarginsViewModel(PrintMargins current)
    {
        ArgumentNullException.ThrowIfNull(current);

        _left = InInches(current.Left);
        _top = InInches(current.Top);
        _right = InInches(current.Right);
        _bottom = InInches(current.Bottom);

        _acceptCommand = new RelayCommand(() => DialogResult = true, () => CanAccept);
        CancelCommand = new RelayCommand(() => DialogResult = false);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The left margin, in inches, as typed.</summary>
    public string Left
    {
        get => _left;
        set => SetMargin(ref _left, value);
    }

    /// <summary>The top margin, in inches, as typed.</summary>
    public string Top
    {
        get => _top;
        set => SetMargin(ref _top, value);
    }

    /// <summary>The right margin, in inches, as typed.</summary>
    public string Right
    {
        get => _right;
        set => SetMargin(ref _right, value);
    }

    /// <summary>The bottom margin, in inches, as typed.</summary>
    public string Bottom
    {
        get => _bottom;
        set => SetMargin(ref _bottom, value);
    }

    /// <summary>
    /// Whether the prompt can be accepted: every value parses as a margin, and the four together are
    /// margins a Print Margins may hold — non-negative, and leaving writable area on the Page (INV-061).
    /// </summary>
    public bool CanAccept => TryParseMargins() is not null;

    /// <summary>
    /// The prompt's outcome: <see langword="true"/> once accepted, <see langword="false"/> once
    /// dismissed, <see langword="null"/> while still being answered. The window closes itself when
    /// this is set.
    /// </summary>
    public bool? DialogResult
    {
        get => _dialogResult;
        private set => Set(ref _dialogResult, value);
    }

    /// <summary>Accepts the margins. Available only while they are valid (INV-061).</summary>
    public ICommand AcceptCommand => _acceptCommand;

    /// <summary>Dismisses the prompt, which changes nothing (INV-061).</summary>
    public ICommand CancelCommand { get; }

    /// <summary>The margins the user chose, or <see langword="null"/> if the prompt was dismissed.</summary>
    public PrintMargins? Answer => DialogResult is true ? TryParseMargins() : null;

    private static string InInches(double units) =>
        (units / Inch).ToString("0.##", CultureInfo.CurrentCulture);

    // The typed values as Print Margins, or null while any of them is not one — parsing and the value
    // object's own guards are the single judgement of validity.
    private PrintMargins? TryParseMargins()
    {
        if (!TryParseInches(_left, out var left) ||
            !TryParseInches(_top, out var top) ||
            !TryParseInches(_right, out var right) ||
            !TryParseInches(_bottom, out var bottom))
        {
            return null;
        }

        try
        {
            return new PrintMargins(left, top, right, bottom);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool TryParseInches(string text, out double units)
    {
        var parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var inches);
        units = inches * Inch;
        return parsed;
    }

    private void SetMargin(ref string field, string value, [CallerMemberName] string? property = null)
    {
        if (!Set(ref field, value, property))
        {
            return;
        }

        // RelayCommand does not delegate to CommandManager.RequerySuggested, so Accept would stay
        // disabled until the user's next mouse move unless it is requeried here.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAccept)));
        _acceptCommand.RaiseCanExecuteChanged();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        return true;
    }

    private readonly RelayCommand _acceptCommand;
    private string _left;
    private string _top;
    private string _right;
    private string _bottom;
    private bool? _dialogResult;
}
