using Application;
using Domain;
using Infrastructure.Markdown;
using Infrastructure.Pdf;
using Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>Registers the Infrastructure layer's adapters with the host container.</summary>
public static class InfrastructureRegistry
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Infrastructure layer: the Markdig-backed Markdown renderer, the MigraDoc PDF
        /// exporter, the Folder Workspace reader and watcher, and other outward-facing adapters.
        /// </summary>
        public void AddInfrastructure()
        {
            services.AddSingleton<ISyntaxHighlighter, ColorCodeSyntaxHighlighter>();

            // The renderer colors each Code Block through the tokenizer, so an exported page shows
            // the Syntax Highlighting the editor does (INV-064). Composed explicitly rather than by
            // convention, because the tokenizer is an optional constructor argument.
            services.AddSingleton<IMarkdownRenderer>(provider =>
                new MarkdigMarkdownRenderer(provider.GetRequiredService<ISyntaxHighlighter>()));
            services.AddSingleton<IPdfExporter>(provider => new MigraDocPdfExporter(
                provider.GetRequiredService<IMermaidImageRenderer>(),
                provider.GetRequiredService<ISyntaxHighlighter>()));
            services.AddSingleton<IDocumentStore, FileDocumentStore>();
            services.AddSingleton<IHtmlExportStore, FileHtmlExportStore>();
            services.AddSingleton<IPdfExportStore, FilePdfExportStore>();
            services.AddSingleton<IMarkdownFolderReader, FileSystemMarkdownFolderReader>();
            services.AddSingleton<IWorkspaceStateStore>(_ => new JsonWorkspaceStateStore(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LiveMarkDownEditor",
                    "workspace.json")));

            // Transient: each Editor Session (Tab) owns its own watcher so several Tabs can watch
            // different Watched Files at once.
            services.AddTransient<IDocumentWatcher, FileSystemDocumentWatcher>();

            // Transient: a Folder Workspace owns its own recursive watcher for the open root.
            services.AddTransient<IFolderWatcher, FileSystemFolderWatcher>();
        }
    }
}
