using System.IO;
using Application;
using Domain;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using UI.Diagnostics;

namespace UI;

/// <summary>
/// Application entry point. Builds configuration, initialises Serilog from that
/// configuration, wires global exception handling, and runs the WPF application.
/// </summary>
public static class Program
{
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

            var window = host.Services.GetRequiredService<MainWindow>();
            window.DataContext = host.Services.GetRequiredService<ViewModels.EditorSessionViewModel>();
            application.Run(window);
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
