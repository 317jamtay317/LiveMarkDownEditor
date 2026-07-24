namespace UI.Core;

/// <summary>
/// The named Print Margins choices — the four fixed presets a word processor offers, plus
/// <see cref="Custom"/> for the user's own values entered through the Custom Margins Prompt (INV-061).
/// </summary>
public enum MarginPreset
{
    /// <summary>One inch on every side. The default.</summary>
    Normal,

    /// <summary>Half an inch on every side.</summary>
    Narrow,

    /// <summary>One inch top and bottom, three-quarters of an inch left and right.</summary>
    Moderate,

    /// <summary>One inch top and bottom, two inches left and right.</summary>
    Wide,

    /// <summary>The user's own four values — any margins that match no named preset.</summary>
    Custom,
}
