using Domain;

namespace UI.Core;

/// <summary>
/// Where and how an Export as HTML should be written: the path the user chose, and the Export Shape
/// they chose alongside it in the same save dialog.
/// </summary>
/// <param name="Path">The absolute path to export to.</param>
/// <param name="Shape">The Export Shape to compose — a Standalone Page or an HTML Fragment.</param>
public sealed record HtmlExportTarget(string Path, ExportShape Shape);
