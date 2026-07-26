# VideoPlayerView

How a [Video](../UbiquitousLanguage.md) is shown in the Visual Document. A Video is written with the
Image's own syntax — `![alt](clip.mp4)` — and told apart from an Image by its Media Source alone
(INV-069); this control is what that Video looks like, and where it plays.

- **Class:** `UI.Controls.VideoPlayerView` (derives from `System.Windows.Controls.Control`)
- **Look:** [`VideoPlayerView.xaml`](../../src/UI/Controls/VideoPlayerView.xaml), merged in `App.xaml`
- **Hosted in:** an `InlineUIContainer` tagged with a `VideoRole` (which carries the authored Media
  Source and alt text, so Capture re-emits `![alt](clip.mp4)`), via a `ContentControl` host

Authored as a custom Control — a class plus a ResourceDictionary for its look — per the project's
Control exception to the zero-code-behind rule, the same pattern as [PanelHeader](PanelHeader.md). It
holds no state Capture reads: playing, pausing, and seeking are presentation, so watching a document
never changes it (INV-069).

## Two states

**The Plate.** Until the reader asks, the player is a still dark plate with a play mark, and nothing
else exists — no browser, no decoding, no process. A document full of Videos costs no more than a
document full of empty rectangles, and "a Video never plays until it is asked to" is true by
construction rather than by discipline.

**The video.** Started, the plate gives way to the video itself, with a control bar below it — a Play
Toggle, a Scrubber, elapsed and total time.

## Why a browser and not a MediaElement

The video surface is the app's embedded browser showing a `<video>` element. That is a decision forced
by evidence, not a preference: WPF's `MediaElement` plays through the legacy Windows Media Player
pipeline, which on current Windows **fails to decode ordinary H.264 MP4s** — `MediaFailed` with
`0xC00D109B`, and a natural video size of 0×0 — which is the format nearly every video a user has is
in. The same files play correctly in the browser, which brings its own codecs. The app already carries
that browser for Mermaid Diagrams (INV-047), so this costs no new dependency; both share the one
[`BrowserEnvironment`](../../src/UI/Platform/BrowserEnvironment.cs).

### And why the *composition* control

The page is hosted through `WebView2CompositionControl`, not the plain `WebView2`. A plain one is a
native child window: it draws on top of everything, so a Video scrolled to the bottom of the pane paints
straight over the status bar. The composition control renders into the WPF visual tree instead, so the
pane clips it and the chrome sits above it, as chrome must.

That control initialises through the Windows SDK's D3D image interop, which is why the UI project
targets `net10.0-windows10.0.19041.0` — under a plain `net10.0-windows` it throws
`FileNotFoundException` for `Microsoft.Windows.SDK.NET` inside `OnApplyTemplate`, taking the app with
it.

A local file is served to the page through a virtual host mapped to the folder the video sits in — a
page may not read `file://` of its own — so the page reaches exactly what the document already names,
and nothing else.

## How it works

The [MarkdownToFlowDocumentProjector](../../src/UI/Wysiwyg/MarkdownToFlowDocumentProjector.cs) projects
an image link whose Media Source `Domain.VideoSource.IsVideo` recognises as a Video, and composes it
through `VideoFormatting.CreateVideo` — the same seam the **Insert Video** Formatting Action uses, so a
loaded Video and a user-inserted one are identical to Capture (INV-018). The Media Source is resolved
through the shared `MediaSource.Resolve`, so a relative source resolves against the Base Directory
exactly as an Image Source does (INV-031).

A Video that names nothing playable — a missing file, or a relative source with no Base Directory —
never reaches this control at all: `VideoFormatting` shows the **alt text** instead. One that reaches
it and then fails (no browser runtime, or a file the browser cannot decode either) raises
`PlaybackFailed`, and the alt text is swapped into the `ContentControl` host *below* the Visual
Document, so a video that would not play is not an edit the user made (INV-069).

### One Video at a time

Starting a Video pauses every other one in the process (INV-069). Every play — the reader's click on a
plate, and the page's own Play Toggle, which reports back through a web message — passes through the
`IsPlaying` property callback, so no way of starting a Video can slip past the rule. The players are
held in a static list of weak references, swept on use, and only those on the starting player's own
dispatcher are paused.

### Clicks, and where they come from

Nothing inside this control can be clicked directly. A `RichTextBox` lays a text layer over the elements
embedded in its document, and with composition hosting a WPF hit-test at the video lands on *nothing at
all* — so neither the plate nor the browser ever sees a click of its own.

Every click therefore arrives the same way: the editor's `OnPreviewMouseLeftButtonDown` finds the player
a click landed in — by its rendered bounds, since the text hit-test answers about the text *beside* an
embedded element — and calls `HandleClickAt`, which decides between the Scrubber and the Play Toggle
from where it landed. That is also why the page is given **no** `controls` attribute: a control bar
drawn by the browser could never answer a click, and controls that do not answer are worse than none.

The trade this makes is deliberate: the reader gets a Play Toggle and a Scrubber that work, and does not
get the browser's volume or fullscreen buttons.

### Sizing

The player takes the width the line offers, capped at 700px and at the video's own resolution, and the
height its aspect ratio calls for. It is asked of the layout rather than fixed because the text column
is not a constant — it narrows with the window, and in Page View the Page's Margins set it. A player
sized to a number of its own overhangs the right margin while sitting flush with the left.

Resizing on `loadedmetadata` reflows the document, and a reflow detaches and reattaches the elements
embedded in the text — so `Unloaded` pauses only after checking, once the layout has settled, that the
player has really gone. Pausing on the spot would stop a Video a second after the reader started it.

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `Source` | `Uri?` | The resolved video to play — a Media Source already resolved against the Base Directory. |
| `IsPlaying` | `bool` (read-only) | Whether the Video is playing. A freshly projected Video is paused. |
| `Progress` | `double` (read-only) | How far through the Video is, 0 to 1, as the page reports it — what the Scrubber shows. |
| `ElapsedText` | `string` (read-only) | The elapsed and total time, as `m:ss / m:ss`. |
| `HasStarted` | `bool` (read-only) | Whether the Video has been started at least once — what the template hides the plate on. |

## Events

| Event | Description |
| --- | --- |
| `PlaybackFailed` | The Video cannot be played, so its alt text should stand in (INV-069). |

## Methods

| Method | Description |
| --- | --- |
| `TogglePlay()` | Starts the Video, or pauses it if it is playing. Starting pauses every other Video. Not an edit. |
| `Pause()` | Pauses the Video if it is playing. Leaves every other Video as it was. Not an edit. |
| `SeekTo(double fraction)` | Seeks to a fraction of the way through, clamped to 0–1. Not an edit. |
| `HandleClickAt(Point point)` | Takes a click the editor routed here: the Scrubber seeks, anywhere else toggles play. |

## Template parts

| Part | Type | Description |
| --- | --- | --- |
| `PART_Browser` | `WebView2CompositionControl` | Where the video plays. Its `CoreWebView2` is created on the first play, never before. |
| `PART_Scrubber` | `ProgressBar` | The Scrubber's track — the element a click is measured against to turn an x into a position. |
