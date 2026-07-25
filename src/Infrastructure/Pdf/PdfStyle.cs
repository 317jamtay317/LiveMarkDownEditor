namespace Infrastructure.Pdf;

/// <summary>
/// The measurements and font families every Block Writer shares when it lays a Markdown block out
/// on the page.
/// </summary>
/// <remarks>
/// Only the font families the built-in Windows resolver maps are named here, so rendering never
/// fails to resolve a font. A value only one writer needs stays with that writer.
/// </remarks>
internal static class PdfStyle
{
    /// <summary>The family prose, headings and table cells are set in.</summary>
    public const string BodyFont = "Arial";

    /// <summary>The family a Code Block and a Code Span are set in.</summary>
    public const string CodeFont = "Courier New";

    /// <summary>The width, in centimetres, the page has room for between its margins.</summary>
    public const double UsableWidthCm = 16.0;

    /// <summary>The distance, in centimetres, one level of nesting indents a block by.</summary>
    public const double IndentStepCm = 0.6;
}
