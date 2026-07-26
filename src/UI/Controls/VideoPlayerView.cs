using System.Globalization;
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
/// The Video Player: how a Video is shown in the Visual Document (INV-069). Until the reader asks for it
/// the player is a still plate carrying a play mark; asked to play, it becomes a real video surface with
/// the usual controls — a Play Toggle, a Scrubber, elapsed and total time. Playing, pausing, and seeking
/// are presentation: none of them changes the Markdown Document.
/// </summary>
/// <remarks>
/// Authored as a custom Control — a class plus a ResourceDictionary (<c>VideoPlayerView.xaml</c>) for its
/// look — per the project's Control exception to the zero-code-behind rule.
/// <para>
/// The video surface is the app's embedded browser showing a <c>&lt;video&gt;</c> element, not a WPF
/// <c>MediaElement</c>. That is a decision forced by evidence: <c>MediaElement</c> plays through the
/// legacy Windows Media Player pipeline, which on current Windows fails to decode ordinary H.264 MP4s
/// (<c>0xC00D109B</c>, and a natural video size of 0×0) — the format nearly every video a user has is in.
/// The browser decodes them with its own codecs, and the app already carries it for Mermaid Diagrams
/// (INV-047), so this costs no new dependency.
/// </para>
/// <para>
/// The browser is created on the <em>first play</em>, not when the Video is projected: a document full of
/// Videos would otherwise start a browser for each one before the reader had asked for any of them. That
/// laziness is also what makes "a Video never plays until it is asked to" true by construction.
/// </para>
/// </remarks>
public sealed class VideoPlayerView : Control
{
    /// <summary>Identifies the <see cref="Source"/> dependency property — the resolved video to play.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(Uri),
        typeof(VideoPlayerView),
        new PropertyMetadata(defaultValue: null, OnSourceChanged));

    private static readonly DependencyPropertyKey IsPlayingPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsPlaying),
        typeof(bool),
        typeof(VideoPlayerView),
        new PropertyMetadata(defaultValue: false, OnIsPlayingChanged));

    /// <summary>Identifies the read-only <see cref="IsPlaying"/> dependency property.</summary>
    public static readonly DependencyProperty IsPlayingProperty = IsPlayingPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ProgressPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Progress),
        typeof(double),
        typeof(VideoPlayerView),
        new PropertyMetadata(defaultValue: 0d));

    /// <summary>Identifies the read-only <see cref="Progress"/> dependency property.</summary>
    public static readonly DependencyProperty ProgressProperty = ProgressPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey HasStartedPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasStarted),
        typeof(bool),
        typeof(VideoPlayerView),
        new PropertyMetadata(defaultValue: false));

    /// <summary>Identifies the read-only <see cref="HasStarted"/> dependency property.</summary>
    public static readonly DependencyProperty HasStartedProperty = HasStartedPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ElapsedTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ElapsedText),
        typeof(string),
        typeof(VideoPlayerView),
        new PropertyMetadata("0:00 / 0:00"));

    /// <summary>Identifies the read-only <see cref="ElapsedText"/> dependency property.</summary>
    public static readonly DependencyProperty ElapsedTextProperty = ElapsedTextPropertyKey.DependencyProperty;

    /// <summary>The template part the video plays in: the app's embedded browser.</summary>
    public const string BrowserPartName = "PART_Browser";

    /// <summary>The template part standing for the Scrubber: the track a click seeks within.</summary>
    public const string ScrubberPartName = "PART_Scrubber";

    /// <summary>The virtual host a local video is served from, so the page may read it.</summary>
    private const string VideoHostName = "video.host";

    static VideoPlayerView() =>
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(VideoPlayerView), new FrameworkPropertyMetadata(typeof(VideoPlayerView)));

    /// <summary>Creates a paused Video Player. A document that plays at its reader has taken something from them.</summary>
    public VideoPlayerView()
    {
        Focusable = false;

        // Every player is remembered so that starting one can pause the rest (INV-069). Weakly, because
        // a Visual Document is re-projected often and a player left behind by an old projection must be
        // free to go; the list is swept whenever it is walked.
        lock (Players)
        {
            Players.Add(new WeakReference<VideoPlayerView>(this));
        }

        // Leaving the visual tree — a re-projection, a Tab switch — stops the sound. A Video the reader
        // can no longer see is not a Video they are still watching.
        Unloaded += (_, _) => PauseIfGone();
    }

    /// <summary>Raised when the Video cannot be played, so its alt text can stand in instead (INV-069).</summary>
    public event EventHandler? PlaybackFailed;

    /// <summary>The resolved video this player plays — a Media Source already resolved (INV-031).</summary>
    public Uri? Source
    {
        get => (Uri?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Whether the Video is playing. A freshly projected Video is paused (INV-069).</summary>
    public bool IsPlaying => (bool)GetValue(IsPlayingProperty);

    /// <summary>How far through the Video is, from 0 to 1 — what the Scrubber shows.</summary>
    public double Progress => (double)GetValue(ProgressProperty);

    /// <summary>
    /// Whether the Video has been started at least once, and so whether the video surface has replaced
    /// the still plate. It is what the template shows the play mark over.
    /// </summary>
    public bool HasStarted => (bool)GetValue(HasStartedProperty);

    /// <summary>The elapsed and total time, as <c>m:ss / m:ss</c>.</summary>
    public string ElapsedText => (string)GetValue(ElapsedTextProperty);

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _browser = GetTemplateChild(BrowserPartName) as IWebView2;
        _scrubber = GetTemplateChild(ScrubberPartName) as FrameworkElement;
    }

    /// <summary>
    /// Takes the width the line offers — never more than the video's own, and never more than
    /// <see cref="MaximumWidth"/> — and the height that width's aspect ratio calls for.
    /// </summary>
    /// <param name="constraint">The space the line has for this player.</param>
    /// <returns>The size the player will occupy.</returns>
    /// <remarks>
    /// The width is asked of the layout rather than fixed, because a Video sits in the text column and a
    /// column is not a constant: it narrows with the window, and in Page View it is the Page's Margins
    /// that set it. A player sized to a number of its own overhangs the right margin while sitting flush
    /// with the left — which is exactly what a reader notices.
    /// </remarks>
    protected override Size MeasureOverride(Size constraint)
    {
        var offered = double.IsInfinity(constraint.Width) || constraint.Width <= 0
            ? PlateWidth
            : constraint.Width;

        var width = Math.Min(offered, MaximumWidth);
        if (_naturalWidth > 0)
        {
            // Never blown up past its own resolution: the Image rule, which shows a small picture at its
            // own size rather than stretched to fill the line (INV-031).
            width = Math.Min(width, _naturalWidth);
        }

        // The control bar sits below the picture, so it is height the video does not get.
        var picture = Math.Max(width, MinimumWidth);
        var size = new Size(picture, (picture * _aspect) + ControlBarHeight);
        base.MeasureOverride(size);
        return size;
    }

    /// <summary>
    /// Starts the Video, or pauses it if it is playing — what a click on the plate does, and what the
    /// page's own Play Toggle reports back. It is not an edit (INV-069).
    /// </summary>
    public void TogglePlay() => SetValue(IsPlayingPropertyKey, !IsPlaying);

    /// <summary>Pauses the Video if it is playing. Not an edit (INV-069).</summary>
    public void Pause() => SetValue(IsPlayingPropertyKey, false);

    /// <summary>
    /// Seeks to <paramref name="fraction"/> of the way through the Video, from 0 (the start) to 1 (the
    /// end), clamped. It is the Scrubber's action, and it is not an edit (INV-069).
    /// </summary>
    /// <param name="fraction">How far through to seek, from 0 to 1.</param>
    public void SeekTo(double fraction)
    {
        var clamped = Math.Clamp(double.IsNaN(fraction) ? 0d : fraction, 0d, 1d);
        SetValue(ProgressPropertyKey, clamped);
        Post($"seek({clamped.ToString(CultureInfo.InvariantCulture)})");
    }

    /// <summary>
    /// Routes a click at <paramref name="point"/> — in this player's own coordinates — to what it landed
    /// on: the Scrubber seeks, anywhere else plays or pauses.
    /// </summary>
    /// <param name="point">Where the click landed, relative to this control.</param>
    /// <returns><see langword="true"/> — a click on the player is always the player's.</returns>
    /// <remarks>
    /// The editor calls this because nothing inside this control can be clicked directly: the page is
    /// composition-hosted inside a <c>RichTextBox</c>, where a WPF hit-test at the video lands on nothing
    /// at all. That is also why the Play Toggle and the Scrubber are this control's own rather than the
    /// browser's — the browser's could never answer a click.
    /// </remarks>
    public bool HandleClickAt(Point point)
    {
        if (ScrubberBounds() is { } track && track.Contains(point) && track.Width > 0)
        {
            SeekTo((point.X - track.X) / track.Width);
            return true;
        }

        TogglePlay();
        return true;
    }

    // Pauses only a Video that has really gone, checked once the layout has settled. A document reflow —
    // which this control causes itself, by resizing to the video's shape the moment its metadata arrives
    // — detaches and reattaches the elements embedded in the text, so an Unloaded that paused on the spot
    // would stop a Video a second after the reader started it.
    private void PauseIfGone() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!IsLoaded)
            {
                Pause();
            }
        }));

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((VideoPlayerView)d).Pause();

    private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var player = (VideoPlayerView)d;
        if (player.IsPlaying)
        {
            // One Video plays at a time (INV-069). This is the seam every play passes through — the
            // reader's click on the plate, and the page's own Play Toggle, which reports back here — so
            // no way of starting a Video can slip past the rule.
            PauseEveryOther(player);
            _ = player.StartAsync();
        }
        else
        {
            player.Post("pause()");
        }
    }

    // Pauses every other Video, and sweeps away the players that have been collected. Only starting does
    // this: pausing takes no Video's place, so it leaves the others alone.
    private static void PauseEveryOther(VideoPlayerView starting)
    {
        var others = new List<VideoPlayerView>();
        lock (Players)
        {
            for (var i = Players.Count - 1; i >= 0; i--)
            {
                if (!Players[i].TryGetTarget(out var player))
                {
                    Players.RemoveAt(i);
                    continue;
                }

                // Only the players this thread owns: a Video is paused by the same UI thread that shows
                // it, and touching another thread's control would throw rather than pause anything. One
                // running app has one UI thread, so this excludes nothing a reader can see.
                if (!ReferenceEquals(player, starting) && player.CheckAccess())
                {
                    others.Add(player);
                }
            }
        }

        // Outside the lock: pausing runs the property callback, which walks this same list.
        foreach (var player in others)
        {
            player.Pause();
        }
    }

    // Brings the video surface up on the first play, then plays. Everything after the first call is just
    // a play message; the browser is created once, for a Video the reader actually asked for.
    private async Task StartAsync()
    {
        if (_browser is null || Source is null)
        {
            return;
        }

        if (_ready)
        {
            Post("play()");
            return;
        }

        if (_starting)
        {
            return;
        }

        _starting = true;
        try
        {
            var environment = await BrowserEnvironment.GetAsync().ConfigureAwait(true);
            await _browser.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

            var core = _browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.WebMessageReceived += OnWebMessage;

            // A local file is served through a virtual host: a page may not read file:// of its own, and
            // mapping only the folder the video sits in keeps the page to what the document already names.
            var address = Source.IsFile ? Serve(core, Source) : Source.AbsoluteUri;

            _browser.NavigationCompleted += (_, _) => _ready = true;
            core.NavigateToString(Page(address));
            SetValue(HasStartedPropertyKey, true);
        }
        catch (Exception exception) when (
            exception is WebView2RuntimeNotFoundException or InvalidOperationException
                or COMException or IOException or ArgumentException)
        {
            // No browser, no video: the alt text stands in, exactly as it does for a Video whose file is
            // missing (INV-031/069). Logged, because a Video that silently refuses to start is otherwise
            // indistinguishable from one the reader never clicked.
            Log.Error(exception, "The browser behind a Video Player could not be started");
            Pause();
            PlaybackFailed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _starting = false;
        }
    }

    // Maps the folder the video lives in to the virtual host, and returns the address the page loads it
    // from. Read access only: the page shows the file, it never writes beside it.
    private static string Serve(CoreWebView2 core, Uri file)
    {
        core.SetVirtualHostNameToFolderMapping(
            VideoHostName,
            Path.GetDirectoryName(file.LocalPath)!,
            CoreWebView2HostResourceAccessKind.Allow);

        return $"https://{VideoHostName}/{Uri.EscapeDataString(Path.GetFileName(file.LocalPath))}";
    }

    // The page the video plays in: the element, the functions this control drives it by, and the events it
    // needs back to keep its own controls honest. Nothing else is loaded — no network, no script beyond
    // this.
    //
    // Deliberately *without* the browser's own controls. The page is composition-hosted inside a
    // RichTextBox, where a WPF hit-test lands on nothing at all — so a control bar drawn by the browser
    // could never be clicked. Showing controls that do not answer is worse than showing none; the Play
    // Toggle and Scrubber the reader sees are this control's, and they drive the page through these.
    private static string Page(string address) =>
        $$"""
          <!doctype html><html><head><meta charset="utf-8"><style>
          html,body{margin:0;height:100%;background:#101114;overflow:hidden}
          video{width:100%;height:100%;display:block;background:#101114}
          </style></head><body>
          <video id="v" autoplay playsinline src="{{address}}"></video>
          <script>
          var v = document.getElementById('v');
          function say(e, extra) {
            window.chrome.webview.postMessage(Object.assign({ event: e }, extra || {}));
          }
          function play() { v.play(); }
          function pause() { v.pause(); }
          function seek(f) { if (isFinite(v.duration)) { v.currentTime = v.duration * f; } }
          function tell() {
            say('time', {
              progress: isFinite(v.duration) && v.duration > 0 ? v.currentTime / v.duration : 0,
              elapsed: v.currentTime,
              duration: isFinite(v.duration) ? v.duration : 0
            });
          }
          v.addEventListener('play', function () { say('play'); });
          v.addEventListener('pause', function () { say('pause'); });
          v.addEventListener('ended', function () { say('pause'); });
          v.addEventListener('error', function () { say('error'); });
          v.addEventListener('loadedmetadata', function () {
            say('meta', { width: v.videoWidth, height: v.videoHeight });
            tell();
          });
          v.addEventListener('timeupdate', tell);
          v.addEventListener('seeked', tell);
          </script></body></html>
          """;

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        Message? message;
        try
        {
            message = JsonSerializer.Deserialize<Message>(e.WebMessageAsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        switch (message?.Event)
        {
            case "play":
                SetValue(IsPlayingPropertyKey, true);
                break;
            case "pause":
                SetValue(IsPlayingPropertyKey, false);
                break;
            case "time":
                SetValue(ProgressPropertyKey, Math.Clamp(message.Progress, 0d, 1d));
                SetValue(ElapsedTextPropertyKey, $"{Clock(message.Elapsed)} / {Clock(message.Duration)}");
                break;
            case "meta":
                SizeToVideo(message.Width, message.Height);
                break;
            case "error":
                // A video the browser cannot decode is a Video that cannot be played: the alt text stands
                // in, and the swap happens above this control so it is not an edit (INV-069).
                Pause();
                PlaybackFailed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    // Shaped by the video itself once it is known: its aspect ratio, and its own resolution as a ceiling.
    // The width still comes from the line (see MeasureOverride) — the video says what shape it is, the
    // column says how much room there is for it.
    private void SizeToVideo(int videoWidth, int videoHeight)
    {
        if (videoWidth <= 0 || videoHeight <= 0)
        {
            return;
        }

        _naturalWidth = videoWidth;
        _aspect = (double)videoHeight / videoWidth;
        InvalidateMeasure();
    }

    // The Scrubber's track in this control's own coordinates, or null when there is no template (and so
    // no Scrubber) to click — in which case every click is a Play Toggle.
    private Rect? ScrubberBounds()
    {
        if (_scrubber is null || !_scrubber.IsDescendantOf(this) || _scrubber.RenderSize.Width <= 0)
        {
            return null;
        }

        return _scrubber.TransformToAncestor(this).TransformBounds(new Rect(_scrubber.RenderSize));
    }

    // Runs a function of the page, if there is a page yet. Before the first play there is nothing to tell.
    private void Post(string script)
    {
        if (_ready && _browser?.CoreWebView2 is { } core)
        {
            _ = core.ExecuteScriptAsync(script);
        }
    }

    private sealed record Message(
        string? Event, double Progress, double Elapsed, double Duration, int Width, int Height);

    // Seconds as a reader reads a clock: m:ss, or h:mm:ss once there is an hour to show.
    private static string Clock(double seconds)
    {
        var time = TimeSpan.FromSeconds(double.IsFinite(seconds) && seconds > 0 ? seconds : 0);
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }

    // Every Video Player alive in the process, so that starting one can pause the rest (INV-069). Held
    // weakly and swept on use, and only ever touched on the UI thread — a play arrives either from a
    // dependency-property callback or from a web message, both of which are dispatched there.
    private static readonly List<WeakReference<VideoPlayerView>> Players = [];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // A player wider than this is scaled down to fit: the rule an Image's picture follows.
    private const double MaximumWidth = 700d;

    // What an unmeasured player is worth showing: the size of the plate before any line has offered a
    // width, and the floor below which a player is not a player.
    private const double PlateWidth = 480d;
    private const double MinimumWidth = 120d;

    // The height of the control bar below the picture — the Play Toggle, the Scrubber, and the time.
    private const double ControlBarHeight = 30d;

    // The video's shape and its own resolution, learned when the page reports its metadata. Until then a
    // plate in the usual 16:9.
    private double _aspect = 9d / 16d;
    private double _naturalWidth;

    // Typed as the interface, not the control: the template hosts the composition-hosted WebView2, which
    // renders into the WPF visual tree so the editor's chrome clips it and draws above it.
    private IWebView2? _browser;
    private FrameworkElement? _scrubber;
    private bool _ready;
    private bool _starting;
}
