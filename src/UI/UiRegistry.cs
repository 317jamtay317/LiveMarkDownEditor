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
        /// picker, the Editor Session ViewModel, and the application's windows.
        /// </summary>
        public void AddUi()
        {
            services.AddSingleton<ISnackbarService, SnackbarService>();
            services.AddSingleton<IGlobalExceptionHandler, GlobalExceptionHandler>();
            services.AddSingleton<IFilePicker, Win32FilePicker>();
            services.AddSingleton<IUiDispatcher, WpfDispatcher>();
            services.AddSingleton<EditorSessionViewModel>();
            services.AddSingleton<MainWindow>();
        }
    }
}
