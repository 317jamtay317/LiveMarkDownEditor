using System.Runtime.InteropServices;

namespace UI.Spelling;

/// <summary>
/// An <see cref="ISpellDictionary"/> backed by the Windows Spell Checking API
/// (<c>ISpellChecker</c>). Uses the operating system's installed dictionaries, so no word list is
/// shipped. If the API is unavailable (an older OS, or the requested language is not installed),
/// every word is reported as correctly spelled — spell check simply shows nothing rather than failing.
/// </summary>
/// <remarks>
/// Results are cached per word, so repeated checks of the same identifier across re-scans do not make
/// repeated cross-process COM calls.
/// </remarks>
public sealed class WindowsSpellDictionary : ISpellDictionary
{
    // A speller can return many corrections; the OS orders them best-first, so a small cap keeps the
    // most relevant ones without pulling the whole list across the COM boundary.
    private const int MaximumSuggestions = 16;

    private readonly ISpellChecker? _checker;
    private readonly Dictionary<string, bool> _cache = new(StringComparer.Ordinal);

    /// <summary>Creates a dictionary for the given language, degrading gracefully if unavailable.</summary>
    /// <param name="languageTag">The BCP-47 language tag to check against (default US English).</param>
    public WindowsSpellDictionary(string languageTag = "en-US")
    {
        try
        {
            var factoryType = Type.GetTypeFromCLSID(new Guid("7AB36653-1796-484B-BDFA-E74F1DB7C1DC"));
            if (factoryType is null || Activator.CreateInstance(factoryType) is not ISpellCheckerFactory factory)
            {
                return;
            }

            if (factory.IsSupported(languageTag, out var supported) == 0 && supported != 0
                && factory.CreateSpellChecker(languageTag, out var checker) == 0)
            {
                _checker = checker;
            }
        }
        catch (COMException)
        {
            _checker = null;
        }
    }

    /// <inheritdoc />
    public bool IsMisspelled(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (_checker is null)
        {
            return false;
        }

        if (_cache.TryGetValue(word, out var cached))
        {
            return cached;
        }

        var result = CheckWithOs(word);
        _cache[word] = result;
        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Suggest(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (_checker is null || word.Length == 0)
        {
            return [];
        }

        try
        {
            // Suggest returns S_OK (0) with an enumerator of corrections for a misspelled word; any
            // other result (e.g. S_FALSE for a correctly-spelled word) means nothing to offer.
            if (_checker.Suggest(word, out var enumerator) != 0 || enumerator is null)
            {
                return [];
            }

            var suggestions = new List<string>();
            // Next yields S_OK (0) while it fetched the requested one item; the cap guards against a
            // speller that returns an unexpectedly long list.
            while (suggestions.Count < MaximumSuggestions
                && enumerator.Next(1, out var item, out var fetched) == 0 && fetched == 1)
            {
                var suggestion = Marshal.PtrToStringUni(item);
                Marshal.FreeCoTaskMem(item);
                if (!string.IsNullOrEmpty(suggestion))
                {
                    suggestions.Add(suggestion);
                }
            }

            return suggestions;
        }
        catch (COMException)
        {
            return [];
        }
    }

    private bool CheckWithOs(string word)
    {
        try
        {
            // Check reports an error for each unrecognised span; feeding a single word, any error at
            // all means that word is misspelled. Next returns S_OK (0) while errors remain.
            return _checker!.Check(word, out var errors) == 0
                && errors is not null
                && errors.Next(out var error) == 0
                && error is not null;
        }
        catch (COMException)
        {
            return false;
        }
    }

    [ComImport, Guid("8E018A9D-2415-4677-BF08-794EA61F94BB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellCheckerFactory
    {
        [PreserveSig] int _GetSupportedLanguages(out IntPtr value);

        [PreserveSig] int IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag, out int value);

        [PreserveSig] int CreateSpellChecker(
            [MarshalAs(UnmanagedType.LPWStr)] string languageTag,
            [MarshalAs(UnmanagedType.Interface)] out ISpellChecker value);
    }

    [ComImport, Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellChecker
    {
        [PreserveSig] int _GetLanguageTag(out IntPtr value);

        [PreserveSig] int Check(
            [MarshalAs(UnmanagedType.LPWStr)] string text,
            [MarshalAs(UnmanagedType.Interface)] out IEnumSpellingError value);

        [PreserveSig] int Suggest(
            [MarshalAs(UnmanagedType.LPWStr)] string word,
            [MarshalAs(UnmanagedType.Interface)] out IEnumString value);
    }

    [ComImport, Guid("00000101-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumString
    {
        // rgelt is a caller-owned CoTaskMem string that must be freed with Marshal.FreeCoTaskMem.
        [PreserveSig] int Next(int celt, out IntPtr rgelt, out int fetched);
    }

    [ComImport, Guid("803E3BD4-2828-4410-8290-418D1D73C762"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumSpellingError
    {
        [PreserveSig] int Next([MarshalAs(UnmanagedType.Interface)] out object? value);
    }
}
