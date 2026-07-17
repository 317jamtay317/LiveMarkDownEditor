using System.IO;
using System.Windows;
using Application;
using Domain;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using UI.Core;
using UI.Diagnostics;
using UI.Platform;
using UI.ViewModels;

namespace UI;

/// <summary>
/// Application entry point. Builds configuration, initialises Serilog from that
/// configuration, wires global exception handling, and runs the WPF application.
/// </summary>
public static class Program
{
    // The application-wide Single Instance name (INV-020) shared by the mutex and the pipe.
    private const string InstanceName = "LiveMarkDownEditor";

    /// <summary>
    /// Composes configuration and logging, then runs the application. Serilog is
    /// configured entirely from the <c>Serilog</c> section of the loaded
    /// configuration (see <c>appsettings.json</c> and its environment overrides).
    /// </summary>
    /// <param name="args">Command-line arguments passed to the host builder.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        // Pin the working directory to the executable's own folder so every relative path — the
        // configuration files, the Serilog log sink, anything app-local — resolves identically
        // whether the app is launched by double-click, from the repo root, or from its output
        // folder. Without this, launching from a read-only directory (e.g. C:\) silently loses the
        // logs and, previously, crashed before the logger was even built.
        var baseDirectory = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(baseDirectory);

        // Single Instance (INV-020): the first launch holds the instance and serves every later
        // one; a later launch forwards its Startup Document to the holder and exits immediately.
        var startupDocument = StartupArguments.DocumentPath(args);
        using var instance = SingleInstanceGuard.TryAcquire(InstanceName);
        if (instance is null)
        {
            SingleInstanceGuard.ForwardDocumentPath(InstanceName, startupDocument ?? string.Empty);
            return;
        }

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = baseDirectory,
        });

        builder.Configuration.SetBasePath(baseDirectory);
        builder.Configuration.AddJsonFile("appsettings.json", optional: false);
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("appsettings.dev.json", optional: true);
            builder.Configuration.AddUserSecrets(typeof(Program).Assembly);
        }

        if (builder.Environment.IsProduction())
        {
            builder.Configuration.AddJsonFile("appsettings.prod.json", optional: true);
        }

        builder.Configuration.AddEnvironmentVariables();

        builder.Services.AddDomain();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure();
        builder.Services.AddUi();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting {Application}", "LiveMarkDownEditor");

            builder.Services.AddSerilog();

            var host = builder.Build();

            var application = new App();
            application.InitializeComponent();

            RegisterGlobalExceptionHandling(application, host.Services);

            // Install the initial palette before the window renders so DynamicResource brushes resolve.
            host.Services.GetRequiredService<Core.IThemeService>().Apply(Core.AppTheme.Light);

            var window = host.Services.GetRequiredService<MainWindow>();
            var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
            window.DataContext = workspace;

            // Mirror the Recent Files to the Windows Jump List whenever they change (INV-037).
            var jumpList = host.Services.GetRequiredService<IJumpList>();
            workspace.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(WorkspaceViewModel.RecentFiles))
                {
                    jumpList.ShowRecentFiles(workspace.RecentFiles);
                }
            };

            // A later launch forwards its Startup Document here; open it on the UI thread and bring
            // the one editor window to the front (INV-020).
            instance.Listen(path => application.Dispatcher.InvokeAsync(
                () => OpenForwardedDocument(window, workspace, path)));

            // Restore the previous session's Tabs and Recent Files, then open any Startup Document on
            // top of them (INV-037, INV-020). Queued so it runs once the dispatcher starts pumping.
            application.Dispatcher.InvokeAsync(async () =>
            {
                await workspace.RestoreAsync();
                if (startupDocument is not null)
                {
                    OpenDocument(workspace, startupDocument);
                }
            });

            application.Run(window);

            // Capture the final open Tabs and Recent Files as the app closes (INV-037).
            PersistWorkspace(workspace);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "LiveMarkDownEditor terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Handles a Startup Document forwarded by a later launch: open it (or activate its existing
    // Tab, INV-009) and surface the one editor window. An empty path is just "activate yourself".
    private static void OpenForwardedDocument(MainWindow window, WorkspaceViewModel workspace, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            OpenDocument(workspace, path);
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    // Opens a Startup Document into the Workspace, tolerating a path that no longer exists.
    private static async void OpenDocument(WorkspaceViewModel workspace, string path)
    {
        try
        {
            if (File.Exists(path))
            {
                await workspace.OpenPathAsync(path);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to open the Startup Document {Path}", path);
        }
    }

    // Persists the Workspace as the app closes, best-effort — a failure to save state must not crash
    // shutdown.
    private static void PersistWorkspace(WorkspaceViewModel workspace)
    {
        try
        {
            workspace.PersistStateAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to persist the Workspace on exit");
        }
    }

    /// <summary>
    /// Routes every process-wide unhandled-exception channel through the
    /// <see cref="IGlobalExceptionHandler"/> so failures are logged and surfaced to the user
    /// rather than terminating the process silently.
    /// </summary>
    /// <param name="application">The running WPF application whose dispatcher errors are handled.</param>
    /// <param name="services">The service provider used to resolve the exception handler.</param>
    private static void RegisterGlobalExceptionHandling(App application, IServiceProvider services)
    {
        var handler = services.GetRequiredService<IGlobalExceptionHandler>();

        // Exceptions on non-UI threads (background threads, finalizers). These are fatal for the
        // process, but we still log and notify before the runtime tears the process down.
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            handler.Handle(
                eventArgs.ExceptionObject as Exception
                    ?? new InvalidOperationException("A non-CLS-compliant exception was thrown."),
                "AppDomain");

        // Exceptions from faulted Tasks whose result was never observed.
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            handler.Handle(eventArgs.Exception, "TaskScheduler");
            eventArgs.SetObserved();
        };

        // Exceptions on the UI thread. Marking them handled keeps the application alive.
        application.DispatcherUnhandledException += (_, eventArgs) =>
        {
            handler.Handle(eventArgs.Exception, "Dispatcher");
            eventArgs.Handled = true;
        };
    }
}
