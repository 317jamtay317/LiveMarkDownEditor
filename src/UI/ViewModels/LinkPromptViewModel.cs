using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Link Prompt's state and behaviour: the text and URL boxes, and the decision to accept or
/// dismiss. It is the one place a URL is typed, because the Visual Document never shows raw
/// <c>[text](url)</c> syntax. Accepting is only possible with a URL — a Link is nothing without a
/// destination (INV-030).
/// </summary>
public sealed class LinkPromptViewModel : INotifyPropertyChanged
{
    /// <summary>Creates the Link Prompt's state for a Link or an Image.</summary>
    /// <param name="forImage">Whether an Image is being inserted rather than a Link.</param>
    /// <param name="proposedText">The selected text, offered as the Link's text or Image's alt text.</param>
    public LinkPromptViewModel(bool forImage, string proposedText)
    {
        Title = forImage ? "Insert image" : "Insert link";
        TextLabel = forImage ? "Alt text" : "Text";
        UrlLabel = forImage ? "Image URL" : "Link URL";
        _text = proposedText ?? string.Empty;

        _acceptCommand = new RelayCommand(() => DialogResult = true, () => CanAccept);
        CancelCommand = new RelayCommand(() => DialogResult = false);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The Link Prompt's window title — "Insert link" or "Insert image".</summary>
    public string Title { get; }

    /// <summary>The label beside the text box — "Text" for a Link, "Alt text" for an Image.</summary>
    public string TextLabel { get; }

    /// <summary>The label beside the URL box.</summary>
    public string UrlLabel { get; }

    /// <summary>The Link's text, or the Image's alt text. May be left empty.</summary>
    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    /// <summary>The Link's destination URL, or the Image's source URL. Required (INV-030).</summary>
    public string Url
    {
        get => _url;
        set
        {
            if (!Set(ref _url, value))
            {
                return;
            }

            // RelayCommand does not delegate to CommandManager.RequerySuggested, so Accept would
            // stay disabled until the user's next mouse move unless it is requeried here.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAccept)));
            _acceptCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Whether the Link Prompt can be accepted: a URL has been given. A Link with no destination
    /// could never be repaired from the Visual Document, which shows a Link by its text alone.
    /// </summary>
    public bool CanAccept => !string.IsNullOrWhiteSpace(Url);

    /// <summary>
    /// The Link Prompt's outcome: <see langword="true"/> once accepted, <see langword="false"/> once
    /// dismissed, and <see langword="null"/> while it is still being answered. The window closes
    /// itself when this is set.
    /// </summary>
    public bool? DialogResult
    {
        get => _dialogResult;
        private set => Set(ref _dialogResult, value);
    }

    /// <summary>Accepts the Link Prompt. Available only once a URL has been given (INV-030).</summary>
    public ICommand AcceptCommand => _acceptCommand;

    /// <summary>Dismisses the Link Prompt, which makes no edit (INV-030).</summary>
    public ICommand CancelCommand { get; }

    /// <summary>The answer the user gave, or <see langword="null"/> if the Link Prompt was dismissed.</summary>
    public LinkDetails? Answer => DialogResult is true ? new LinkDetails(Text, Url) : null;

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
    private string _text;
    private string _url = string.Empty;
    private bool? _dialogResult;
}
