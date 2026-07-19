using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Domain;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace UI.Platform;

/// <summary>
/// WebView2-backed adapter for the <see cref="IMermaidImageRenderer"/> port: renders a Mermaid
/// Diagram to a PNG for an Export as PDF (INV-050). It drives an off-screen browser running the
/// bundled Mermaid library, sizes it to the rendered diagram, and captures the result.
/// </summary>
/// <remarks>
/// Every failure mode — no WebView2 runtime, a diagram Mermaid rejects, a capture that times out —
/// yields <see langword="null"/> rather than throwing, so the exporter falls back to the diagram's
/// source text and a PDF export is never broken by a diagram (INV-050). Renders are serialised, since
/// a single off-screen browser is reused across the whole export.
/// </remarks>
public sealed class WebView2MermaidImageRenderer : IMermaidImageRenderer
{
    private const string HostName = "mermaid.host";
    private const string HostUrl = "https://mermaid.host/index.html";
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(12);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dispatcher _dispatcher = System.Windows.Application.Current?.Dispatcher
        ?? Dispatcher.CurrentDispatcher;

    private Window? _window;
    private WebView2? _webView;
    private bool _ready;

    /// <inheritdoc />
    public async Task<DiagramImage?> RenderAsync(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // WebView2 is a UI object: do everything on the UI thread.
            return await _dispatcher.InvokeAsync(() => RenderOnUiThreadAsync(source)).Task.Unwrap().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or InvalidOperationException or COMException or IOException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DiagramImage?> RenderOnUiThreadAsync(string source)
    {
        if (!await EnsureReadyAsync().ConfigureAwait(true) || _webView?.CoreWebView2 is not { } core)
        {
            return null;
        }

        var request = JsonSerializer.Serialize(source);
        var json = await core.ExecuteScriptAsync($"renderDiagram({request}, false)").ConfigureAwait(true);
        if (Measure(json) is not { } size)
        {
            return null;
        }

        // Size the off-screen host to the rendered diagram (plus the host page's padding) and let it
        // repaint before capturing, so the whole diagram is in the captured image.
        _window!.Width = size.Width + 40;
        _window.Height = size.Height + 40;
        await Task.Delay(60).ConfigureAwait(true);

        using var stream = new MemoryStream();
        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream).ConfigureAwait(true);
        return new DiagramImage(stream.ToArray(), size.Width, size.Height);
    }

    // Creates the off-screen host once and navigates it to the bundled Mermaid page, returning whether
    // it is ready to render. A navigation that does not complete within the timeout leaves it not ready.
    private async Task<bool> EnsureReadyAsync()
    {
        if (_ready)
        {
            return true;
        }

        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Left = -10000,
            Top = -10000,
            Width = 800,
            Height = 600,
        };
        _webView = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.White };
        _window.Content = _webView;
        _window.Show();

        await _webView.EnsureCoreWebView2Async().ConfigureAwait(true);
        var core = _webView.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping(HostName, AssetsFolder(), CoreWebView2HostResourceAccessKind.Allow);
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;

        var navigated = new TaskCompletionSource();
        void OnNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e) => navigated.TrySetResult();
        _webView.NavigationCompleted += OnNavigated;
        try
        {
            core.Navigate(HostUrl);
            var completed = await Task.WhenAny(navigated.Task, Task.Delay(RenderTimeout)).ConfigureAwait(true);
            _ready = completed == navigated.Task;
        }
        finally
        {
            _webView.NavigationCompleted -= OnNavigated;
        }

        return _ready;
    }

    // Reads the {ok,width,height} the host page returns; null when the diagram did not render.
    private static (int Width, int Height)? Measure(string executeScriptJson)
    {
        // ExecuteScriptAsync returns the JS result as a JSON literal — here a JSON-encoded string.
        var inner = JsonSerializer.Deserialize<string>(executeScriptJson);
        if (inner is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(inner);
        var root = document.RootElement;
        if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            return null;
        }

        return (root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32());
    }

    private static string AssetsFolder() =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Mermaid");
}
