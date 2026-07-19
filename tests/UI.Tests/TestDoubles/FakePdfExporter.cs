using System.Text;
using Domain;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Deterministic <see cref="IPdfExporter"/> for tests. It fakes the PDF re-layout with an
/// obviously-not-a-PDF marker keyed to the document's source text — the real MigraDoc adapter is
/// tested in Infrastructure.Tests, so a ViewModel test only needs to know that <em>this document's</em>
/// content was the one exported (INV-033).
/// </summary>
public sealed class FakePdfExporter : IPdfExporter
{
    /// <summary>The Markdown Documents this exporter was asked to export, in order.</summary>
    public List<string> Exported { get; } = [];

    /// <summary>The marker bytes this fake produces for the given source text.</summary>
    public static byte[] BytesFor(string sourceText) => Encoding.UTF8.GetBytes($"PDF:{sourceText}");

    /// <inheritdoc />
    public Task<byte[]> ExportAsync(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Exported.Add(document.Source.Text);
        return Task.FromResult(BytesFor(document.Source.Text));
    }
}
