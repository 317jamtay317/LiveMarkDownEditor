using System.IO;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace UI.Platform;

/// <summary>
/// The one place the browser behind every Mermaid Diagram is configured: the virtual host its pages
/// are served from, the bundled assets that host maps to, and the profile folder the browser works in
/// (INV-047). Both render surfaces — the inline picture in the Visual Document and the Diagram Preview
/// in the Preview Panel — share it, so neither can drift from the other.
/// </summary>
/// <remarks>
/// The profile deliberately lives under the user's local application data rather than beside the
/// executable, which is where WebView2 would otherwise put it. An installed app sits in a directory
/// the user may only read, so a profile defaulted next to the executable cannot be written and the
/// browser never starts — every diagram would fall back to its source text on every machine but a
/// developer's (INV-047). The assets are still read from beside the executable; nothing is written
/// there.
/// </remarks>
public static class MermaidBrowserHost
{
    /// <summary>The virtual host name the bundled Mermaid assets are served from.</summary>
    public const string HostName = "mermaid.host";

    /// <summary>The URL of the bundled Mermaid host page, served from <see cref="HostName"/>.</summary>
    public const string HostUrl = "https://mermaid.host/index.html";

    // The profile folder is named for the app rather than the executable so it is recognisable in the
    // user's profile, and is shared by every render surface in the process.
    private const string ProfileFolderName = "LiveMarkDownEditor";
    private const string BrowserFolderName = "WebView2";

    private static Task<CoreWebView2Environment>? _environment;

    /// <summary>
    /// The folder holding the bundled Mermaid host page and library, beside the executable. Read-only:
    /// the browser is given read access to it and never writes there.
    /// </summary>
    public static string AssetsFolder { get; } =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Mermaid");

    /// <summary>
    /// The folder the browser keeps its own profile in — caches, session state and its lock file —
    /// under the user's local application data, so it stays writable however the app is installed
    /// (INV-047).
    /// </summary>
    public static string UserDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProfileFolderName,
        BrowserFolderName);

    /// <summary>
    /// Gets the browser environment every Mermaid render surface runs in, creating it on first use.
    /// </summary>
    /// <returns>The shared browser environment.</returns>
    /// <remarks>
    /// The result is cached for the life of the process — including a failed attempt. A browser that
    /// cannot be created (no WebView2 runtime installed, or a profile folder that cannot be written)
    /// will not become creatable later in the same run, and diagrams are re-rendered often enough that
    /// retrying each time would be pure waste. Callers await this on the UI thread, so no lock is
    /// needed to guard the cache.
    /// </remarks>
    public static Task<CoreWebView2Environment> EnvironmentAsync() => _environment ??= CreateAsync();

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        Directory.CreateDirectory(UserDataFolder);
        Log.Debug("Creating the Mermaid browser environment in {UserDataFolder}", UserDataFolder);
        return await CoreWebView2Environment
            .CreateAsync(browserExecutableFolder: null, userDataFolder: UserDataFolder)
            .ConfigureAwait(true);
    }
}
