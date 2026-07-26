using System.IO;
using Microsoft.Web.WebView2.Core;

namespace UI.Platform;

/// <summary>
/// The one place the browser behind every Mermaid Diagram is configured: the virtual host its pages
/// are served from, the bundled assets that host maps to, and the profile folder the browser works in
/// (INV-047). Both render surfaces — the inline picture in the Visual Document and the Diagram Preview
/// in the Preview Panel — share it, so neither can drift from the other.
/// </summary>
/// <remarks>
/// The profile and the environment itself are the app's, not Mermaid's: they come from
/// <see cref="BrowserEnvironment"/>, which a Video Player runs in too (INV-069). The assets are read
/// from beside the executable; nothing is written there.
/// </remarks>
public static class MermaidBrowserHost
{
    /// <summary>The virtual host name the bundled Mermaid assets are served from.</summary>
    public const string HostName = "mermaid.host";

    /// <summary>The URL of the bundled Mermaid host page, served from <see cref="HostName"/>.</summary>
    public const string HostUrl = "https://mermaid.host/index.html";

    /// <summary>
    /// The folder holding the bundled Mermaid host page and library, beside the executable. Read-only:
    /// the browser is given read access to it and never writes there.
    /// </summary>
    public static string AssetsFolder { get; } =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Mermaid");

    /// <summary>
    /// The folder the browser keeps its own profile in, which every embedded browser in the app shares
    /// (INV-047).
    /// </summary>
    public static string UserDataFolder => BrowserEnvironment.UserDataFolder;

    /// <summary>
    /// Gets the browser environment every Mermaid render surface runs in, creating it on first use. It
    /// is the app's one shared environment — a Video Player runs in it too (INV-069).
    /// </summary>
    /// <returns>The shared browser environment.</returns>
    public static Task<CoreWebView2Environment> EnvironmentAsync() => BrowserEnvironment.GetAsync();
}
