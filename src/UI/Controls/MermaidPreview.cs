using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Serilog;
using UI.Platform;

namespace UI.Controls;

/// <summary>
/// The Preview Panel's rendering surface: a WebView2 that renders the Mermaid Diagram at the caret as
/// a live Diagram Preview (INV-047). It is a custom Control — the only place interaction logic lives
/// outside a ViewModel — hosting a browser that runs the bundled Mermaid library offline.
/// </summary>
/// <remarks>
/// Rendering is view-only: the control reads <see cref="DiagramSource"/> and draws it, and never
/// changes any Markdown Document (INV-047). It follows the app theme through <see cref="IsDark"/>. The
/// browser loads a bundled host page from a virtual host mapped to the app's Mermaid assets, so no
/// network is used.
/// </remarks>
public sealed class MermaidPreview : ContentControl
{
    /// <summary>
    /// Identifies the <see cref="DiagramSource"/> dependency property — the Mermaid Diagram source to
    /// render, or <see langword="null"/> when the caret is not in a Mermaid Diagram.
    /// </summary>
    public static readonly DependencyProperty DiagramSourceProperty = DependencyProperty.Register(
        nameof(DiagramSource),
        typeof(string),
        typeof(MermaidPreview),
        new PropertyMetadata(defaultValue: null, OnRenderInputChanged));

    /// <summary>
    /// Identifies the <see cref="IsDark"/> dependency property — whether the active theme is dark, so
    /// the Diagram Preview matches the editor's light/dark palette.
    /// </summary>
    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.Register(
        nameof(IsDark),
        typeof(bool),
        typeof(MermaidPreview),
        new PropertyMetadata(defaultValue: false, OnRenderInputChanged));

    private readonly WebView2 _webView = new();
    private bool _ready;
    private bool _initialising;

    /// <summary>Creates the Preview Panel's rendering surface.</summary>
    /// <remarks>
    /// The WebView2 host is loaded lazily — only when the panel first becomes visible — so a user who
    /// never opens the Preview Panel never pays to spin up a browser, and the editor is untouched by
    /// the diagram surface until it is actually asked for.
    /// </remarks>
    public MermaidPreview()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _webView.DefaultBackgroundColor = System.Drawing.Color.White;
        Content = _webView;

        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                DeferInitialiseUntilStablyVisible();
            }
        };
    }

    /// <summary>
    /// The Mermaid Diagram source to render, or <see langword="null"/> when the caret is not within a
    /// Mermaid Diagram — in which case the panel shows a hint instead (INV-047).
    /// </summary>
    public string? DiagramSource
    {
        get => (string?)GetValue(DiagramSourceProperty);
        set => SetValue(DiagramSourceProperty, value);
    }

    /// <summary>Whether the active theme is dark, so the Diagram Preview matches the editor palette.</summary>
    public bool IsDark
    {
        get => (bool)GetValue(IsDarkProperty);
        set => SetValue(IsDarkProperty, value);
    }

    private static void OnRenderInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MermaidPreview)d).RenderCurrent();

    // A Preview Panel that is Collapsed at startup still flashes IsVisible=true for a single layout pass
    // before its Visibility binding resolves. Defer to a lower dispatcher priority and re-check: a
    // transient flash is Collapsed again by the time this runs and is skipped, so only a panel the user
    // actually opens spins up the browser — keeping the host genuinely lazy.
    private void DeferInitialiseUntilStablyVisible() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (IsVisible)
            {
                _ = InitialiseAsync();
            }
        }));

    // Loads the bundled Mermaid host once the control is realised: maps a virtual host to the app's
    // Mermaid assets folder (so the page and library load offline) and navigates to the host page.
    private async Task InitialiseAsync()
    {
        if (_ready || _initialising)
        {
            return;
        }

        _initialising = true;
        try
        {
            // The browser works in a profile outside the installation directory, so an installed app
            // renders the Diagram Preview just as a developer's build does (INV-047).
            var environment = await MermaidBrowserHost.EnvironmentAsync().ConfigureAwait(true);
            await _webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

            var core = _webView.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping(
                MermaidBrowserHost.HostName,
                MermaidBrowserHost.AssetsFolder,
                CoreWebView2HostResourceAccessKind.Allow);
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            _webView.NavigationCompleted += (_, _) =>
            {
                _ready = true;
                RenderCurrent();
            };

            core.Navigate(MermaidBrowserHost.HostUrl);
        }
        catch (Exception exception) when (
            exception is WebView2RuntimeNotFoundException or InvalidOperationException or COMException or IOException)
        {
            // WebView2 unavailable (no runtime, or a load failure): the panel stays blank rather than
            // crashing the app. Rendering is view-only, so nothing else is affected (INV-047). It is
            // logged because a blank panel is otherwise indistinguishable from a diagram Mermaid rejects.
            Log.Error(exception, "The Mermaid browser could not be started; the Diagram Preview stays blank");
        }
        finally
        {
            _initialising = false;
        }
    }

    // Renders the current Diagram Source (or clears to the hint when it is null) once the host is
    // ready. Before it is ready the latest inputs are simply read again when NavigationCompleted fires.
    private void RenderCurrent()
    {
        if (!_ready)
        {
            return;
        }

        var source = JsonSerializer.Serialize(DiagramSource ?? string.Empty);
        var dark = IsDark ? "true" : "false";
        _ = _webView.CoreWebView2.ExecuteScriptAsync($"renderDiagram({source}, {dark})");
    }
}
