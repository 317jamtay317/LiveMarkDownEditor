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
        /// exporter, and other outward-facing adapters.
        /// </summary>
        public void AddInfrastructure()
        {
            services.AddSingleton<IMarkdownRenderer, MarkdigMarkdownRenderer>();
            services.AddSingleton<IPdfExporter, MigraDocPdfExporter>();
            services.AddSingleton<IDocumentStore, FileDocumentStore>();
            services.AddSingleton<IHtmlExportStore, FileHtmlExportStore>();
            services.AddSingleton<IPdfExportStore, FilePdfExportStore>();
            services.AddSingleton<IWorkspaceStateStore>(_ => new JsonWorkspaceStateStore(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LiveMarkDownEditor",
                    "workspace.json")));

            // Transient: each Editor Session (Tab) owns its own watcher so several Tabs can watch
            // different Watched Files at once.
            services.AddTransient<IDocumentWatcher, FileSystemDocumentWatcher>();
        }
    }
}
