using System.IO;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace UI.Platform;

/// <summary>
/// The one browser environment every embedded browser in the app shares: the Mermaid render surfaces
/// (INV-047) and the Video Players (INV-069). One environment is not merely tidy — two of them cannot
/// share a profile folder, so a second would have to keep its own.
/// </summary>
/// <remarks>
/// The profile deliberately lives under the user's local application data rather than beside the
/// executable, which is where WebView2 would otherwise put it. An installed app sits in a directory the
/// user may only read, so a profile defaulted next to the executable cannot be written and the browser
/// never starts — every diagram would fall back to its source text, and every Video to its alt text, on
/// every machine but a developer's.
/// </remarks>
public static class BrowserEnvironment
{
    // Named for the app rather than the executable so it is recognisable in the user's profile.
    private const string ProfileFolderName = "LiveMarkDownEditor";
    private const string BrowserFolderName = "WebView2";

    // A Video Player plays because the reader asked it to (INV-069) — but the asking happens in the
    // editor, outside the page, so Chromium sees no gesture of its own and would refuse to start an
    // unmuted video. The gesture is real; only its location is unusual.
    private const string BrowserArguments = "--autoplay-policy=no-user-gesture-required";

    private static Task<CoreWebView2Environment>? _environment;

    /// <summary>
    /// The folder the browser keeps its own profile in — caches, session state and its lock file — under
    /// the user's local application data, so it stays writable however the app is installed.
    /// </summary>
    public static string UserDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProfileFolderName,
        BrowserFolderName);

    /// <summary>Gets the shared browser environment, creating it on first use.</summary>
    /// <returns>The shared browser environment.</returns>
    /// <remarks>
    /// The result is cached for the life of the process — including a failed attempt. A browser that
    /// cannot be created (no WebView2 runtime installed, or a profile folder that cannot be written) will
    /// not become creatable later in the same run, and retrying on every diagram and every Video would be
    /// pure waste. Callers await this on the UI thread, so no lock is needed to guard the cache.
    /// </remarks>
    public static Task<CoreWebView2Environment> GetAsync() => _environment ??= CreateAsync();

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        Directory.CreateDirectory(UserDataFolder);
        Log.Debug("Creating the shared browser environment in {UserDataFolder}", UserDataFolder);
        return await CoreWebView2Environment
            .CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: UserDataFolder,
                options: new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = BrowserArguments })
            .ConfigureAwait(true);
    }
}
