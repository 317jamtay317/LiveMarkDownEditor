using Application;
using Domain;
using Infrastructure.Markdown;
using Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>Registers the Infrastructure layer's adapters with the host container.</summary>
public static class InfrastructureRegistry
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Infrastructure layer: the Markdig-backed Markdown renderer and other
        /// outward-facing adapters.
        /// </summary>
        public void AddInfrastructure()
        {
            services.AddSingleton<IMarkdownRenderer, MarkdigMarkdownRenderer>();
            services.AddSingleton<IDocumentStore, FileDocumentStore>();

            // Transient: each Editor Session (Tab) owns its own watcher so several Tabs can watch
            // different Watched Files at once.
            services.AddTransient<IDocumentWatcher, FileSystemDocumentWatcher>();
        }
    }
}
