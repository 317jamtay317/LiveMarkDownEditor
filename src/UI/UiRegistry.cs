using Microsoft.Extensions.DependencyInjection;
using UI.Core;
using UI.Diagnostics;
using UI.Notifications;
using UI.Platform;
using UI.ViewModels;

namespace UI;

/// <summary>Registers the UI layer's services with the host container.</summary>
public static class UiRegistry
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the UI layer: the snackbar service, the global exception handler, the file
        /// picker, the unsaved-edits prompt, the theme service, the Editor Session factory, the
        /// Workspace, appearance and export ViewModels, and the application's windows.
        /// </summary>
        public void AddUi()
        {
            services.AddSingleton<ISnackbarService, SnackbarService>();
            services.AddSingleton<IGlobalExceptionHandler, GlobalExceptionHandler>();
            services.AddSingleton<IFilePicker, Win32FilePicker>();
            services.AddSingleton<IUiDispatcher, WpfDispatcher>();
            services.AddSingleton<IUnsavedEditsPrompt, MessageBoxUnsavedEditsPrompt>();
            services.AddSingleton<ILinkPrompt, WindowLinkPrompt>();
            services.AddSingleton<IThemeService, WpfThemeService>();
            services.AddSingleton<IMarkdownRoundTrip, Wysiwyg.FlowDocumentRoundTrip>();

            // A fresh Editor Session (with its own Watched File watcher) is minted per Tab.
            services.AddTransient<EditorSessionViewModel>();
            services.AddSingleton<EditorSessionFactory>(
                provider => () => provider.GetRequiredService<EditorSessionViewModel>());

            services.AddSingleton<AppearanceViewModel>();
            services.AddSingleton<ExportViewModel>();
            services.AddSingleton<WorkspaceViewModel>();
            services.AddSingleton<MainWindow>();
        }
    }
}
