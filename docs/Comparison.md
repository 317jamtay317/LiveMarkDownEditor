# How LiveMarkDownEditor compares

A side-by-side look at LiveMarkDownEditor and three Markdown editors people most often weigh it
against: **Typora**, **VS Code with the Markdown All in One extension**, and **Haroopad**.

**Compared as of 24 August 2026** — LiveMarkDownEditor 1.2.3 · Typora 1.14.9 · Markdown All in One
3.6.3 (in VS Code) · Haroopad 0.13.2. Every competitor cell was checked against that project's own
documentation or issue tracker; the [sources](#sources) are listed at the end.

This document is written by the author of LiveMarkDownEditor, so read it the way you would read any
comparison written by one of the entrants. The honest summary is up front, the rows where
LiveMarkDownEditor loses are not hidden at the bottom, and **[corrections are welcome as an
issue](https://github.com/317jamtay317/LiveMarkDownEditor/issues)** — if a cell is wrong it will be
fixed.

---

## The short version

**Pick VS Code + Markdown All in One** if Markdown is one of many things you do in a day and the
document lives in a repository. Nothing here competes with its extension ecosystem, its Git
integration, or the fact that it is already open on your machine. You are editing raw `#` and `*` in
a text buffer with a preview beside it, and for a lot of people that is exactly right.

**Pick Typora** if you want a mature, polished WYSIWYG editor and you are on macOS or Linux — or you
want one license to cover all three platforms. It is years ahead of LiveMarkDownEditor in maturity,
it has LaTeX math, CSS theming, and export to `docx`/LaTeX/ePub, and $14.99 for three devices is a
fair price for a tool you will live in. This is the closest comparison here; a good part of what
LiveMarkDownEditor does, Typora has done well for a long time.

**Pick LiveMarkDownEditor** if you are on Windows and either (a) files change underneath you — an AI
agent, a teammate, a sync client, a build script — and you want to *see* what changed rather than
have it land invisibly, (b) you draw a lot of Mermaid diagrams and would rather drag boxes around a
canvas than hand-write diagram syntax, or (c) you want word-processor page layout and print output
from a Markdown file. It is free and MIT-licensed.

**Haroopad** was a good editor and it is no longer maintained — the last release was **0.13.2 in
March 2015**, and the repository's development branch stopped in 2016. It still runs, and if it does
what you need there is nothing wrong with continuing to use it, but it will not gain a feature or a
security fix. Its column below reflects its last documented feature set.

---

## At a glance

| | LiveMarkDownEditor | Typora | VS Code + Markdown All in One | Haroopad |
| --- | --- | --- | --- | --- |
| **Platforms** | Windows only | Windows · macOS · Linux | Windows · macOS · Linux | Windows · macOS · Linux |
| **Price** | Free | $14.99 one-time, 3 devices, 15-day trial | Free | Free |
| **Source** | Open — MIT | Closed | Open — MIT (extension) | Open — GPL-3.0 |
| **Maintained** | ✅ Active | ✅ Active | ✅ Active (~14.3M installs) | ❌ Last release March 2015 |
| **Maturity** | Young — v1.2.3 | Mature, years of releases | Mature, very widely used | Frozen |
| **Editing model** | WYSIWYG, with an optional live source panel beside it | WYSIWYG, with a source-mode toggle | Raw Markdown text + side preview | Raw Markdown text + side preview |
| **Runtime** | .NET 10 / WPF | Electron | Electron | Web stack (CodeMirror) |

Legend for the tables below: ✅ yes · ⚠️ partial, or with a caveat in the footnote · ❌ no ·
❔ could not verify.

---

## Live updates — what the name is about

This is the section LiveMarkDownEditor exists for, so it is the section to be most skeptical of.
Every editor here notices that a file changed on disk. The difference is what happens next.

| | LiveMarkDownEditor | Typora | VS Code + Markdown All in One | Haroopad |
| --- | --- | --- | --- | --- |
| Notices the open file changed on disk | ✅ | ✅ | ✅ | ❔ |
| Reloads it without asking you first | ✅ | ❌ prompts *"Reload content?"*[^t-reload] | ✅ when you have no unsaved edits | ❔ |
| **Shows you which paragraphs changed** | ✅ shading that fades on its own, plus a rule where text was deleted | ❌ | ❌ | ❌ |
| Keeps your cursor and scroll position through a reload | ✅ | ❔ | ✅ | ❔ |
| Conflict when the file changed *and* you have unsaved edits | ✅ a bar offering keep-mine / reload / look-first | ⚠️ reload prompt only[^t-conflict] | ✅ save-conflict dialog with a compare view | ❔ |
| Line-by-line difference of yours against the disk copy | ✅ in the editor | ❌ | ✅ | ❔ |
| A rewrite that changes bytes but not content raises no reload | ✅ | ❔ | n/a | ❔ |
| Live-updating folder tree | ✅ | ✅ | ✅ | ❔ |

**The honest claim:** watching a file is not unusual — VS Code has reloaded unmodified files for
years, and Typora prompts. What no other editor here does is **show you what the other writer
changed**. When an agent rewrites three paragraphs of your document while you are looking at it, the
difference between "the text is now different" and "*these* paragraphs are now different" is the
whole feature.

[^t-reload]: Typora shows a *"File content is changed by external applications. Reload content?"*
message rather than reloading seamlessly; there is no setting for automatic reload.

[^t-conflict]: Smarter save-conflict handling is an open Typora feature request.

---

## Diagrams and math

| | LiveMarkDownEditor | Typora | VS Code + Markdown All in One | Haroopad |
| --- | --- | --- | --- | --- |
| Mermaid rendered in the document | ✅ flowchart, sequence, state, class, ER, gantt, pie | ✅ | ⚠️ needs a second extension[^v-mermaid] | ❌ |
| Other diagram syntaxes | ❌ | ✅ flowchart.js, js-sequence | ⚠️ by extension | ✅ flowchart, sequence |
| **Drag-and-drop diagram builder** | ✅ nodes and arrows on a canvas, writing canonical Mermaid back out — flowchart, state, class, ER | ❌ | ❌ | ❌ |
| Enlarged preview of the selected diagram | ✅ | ❌ | ⚠️ the whole preview pane | ⚠️ the whole preview pane |
| Diagrams survive into exported HTML and PDF | ✅ | ✅ | ⚠️ by extension | ✅ |
| **LaTeX / math** | ❌ **not supported** | ✅ MathJax, chemical equations, auto-numbering | ✅ KaTeX, with completions | ✅ |

If you write mathematics, LiveMarkDownEditor is the wrong tool today and the other three are all
fine choices. If you write diagrams, the builder is the thing nothing else here has: Typora renders
Mermaid beautifully, but you still author it by typing the syntax.

[^v-mermaid]: For example *Markdown Preview Mermaid Support*. Markdown All in One deliberately
leaves diagram rendering to other extensions.

---

## Writing and Markdown support

| | LiveMarkDownEditor | Typora | VS Code + Markdown All in One | Haroopad |
| --- | --- | --- | --- | --- |
| Headings, bold, italic, strikethrough, inline code | ✅ | ✅ | ✅ raw text | ✅ raw text |
| Fenced code blocks with syntax highlighting | ✅ | ✅ | ✅ | ✅ 112 languages |
| Tables edited as tables | ✅ alignment, add/remove row and column, striped rows, Enter walks out of the last row | ✅ | ⚠️ a formatter for the raw text | ❌ raw text |
| Task lists you tick in the document | ✅ | ✅ | ⚠️ toggled by shortcut in the text | ⚠️ rendered in the preview |
| Footnotes | ✅ collected and numbered for you | ✅ | ⚠️ not rendered by the built-in preview[^v-footnote] | ❔ |
| **Definition lists** | ✅ | ❌ open feature request | ❌ built-in preview | ❔ |
| Images | ✅ | ✅ | ✅ in preview | ✅ |
| **Video played inline** | ✅ from image syntax, with a scrubber and elapsed time | ⚠️ raw HTML `<video>` only | ⚠️ raw HTML `<video>` only | ⚠️ raw HTML `<video>` only |
| Spell check as you type | ✅ splits `camelCase`, skips code, permanent custom dictionary | ✅ system or built-in spellcheck | ⚠️ by extension | ❔ |
| Raw source visible beside the document | ✅ side panel, edit either, scroll locked together | ⚠️ a mode that replaces the document | ✅ the text *is* the source | ✅ |

[^v-footnote]: VS Code's built-in Markdown preview does not render `[^1]` footnotes; an extension
adds it.

---

## Getting around

| | LiveMarkDownEditor | Typora | VS Code + Markdown All in One | Haroopad |
| --- | --- | --- | --- | --- |
| **Tabs for open documents** | ✅ with pinning that survives a restart | ❌ one document per window[^t-tabs] | ✅ | ❔ |
| Folder tree of your Markdown files | ✅ live-updating, Markdown only | ✅ | ✅ the whole workspace | ❔ |
| Heading outline, with your position tracked | ✅ | ✅ | ✅ | ❔ |
| Fold a section to its heading | ✅ | ❔ | ✅ | ❔ |
| Find and replace with a match count | ✅ reaches inside folded sections | ✅ | ✅ | ✅ |
| `Ctrl`+Click to follow a link or open a linked `.md` | ✅ | ✅ | ✅ | ❔ |
| Word count, reading time, caret position, current section | ✅ | ✅ | ⚠️ by extension | ❔ |
| Dockable / auto-hiding panels | ✅ every panel, remembered between runs | ⚠️ one sidebar | ✅ | ⚠️ fixed split |
| Windows jump list, file associations, reuses the running window | ✅ | ⚠️ file associations | ⚠️ file associations | ⚠️ file associations |

[^t-tabs]: A long-standing feature request; an unofficial community plugin adds tabs.

---

## Output — export, print, and paste

| | LiveMarkDownEditor | Typora | VS Code + Markdown All in One | Haroopad |
| --- | --- | --- | --- | --- |
| Export HTML | ✅ standalone page or bare fragment | ✅ | ✅ | ✅ |
| Export PDF | ✅ | ✅ with bookmarks | ⚠️ by extension | ✅ |
| Export `docx`, LaTeX, ePub, MediaWiki | ❌ | ✅[^t-pandoc] | ⚠️ by extension | ❌ |
| **Page view — whole 8.5 × 11 sheets on screen** | ✅ | ❌ | ❌ | ❌ |
| **Print preview and page setup** (orientation, margins) | ✅ one setting governs screen, preview and paper | ❌ | ❌ | ❌ |
| Copy as rich text for Word, Outlook, Slack | ✅ | ✅ | ❌ | ✅ |
| Smart paste — URL becomes a link, clipboard image saved beside the file, HTML converted to Markdown | ✅ | ⚠️ image save and HTML paste | ⚠️ path completion | ❔ |
| Presentation mode | ❌ | ❌ | ⚠️ by extension | ✅ |

[^t-pandoc]: Typora's `docx`/LaTeX/ePub export requires Pandoc to be installed separately.

---

## Extensibility and customization — where LiveMarkDownEditor loses

| | LiveMarkDownEditor | Typora | VS Code + Markdown All in One | Haroopad |
| --- | --- | --- | --- | --- |
| **Plugins / extensions** | ❌ none | ⚠️ no official API; an unofficial plugin loader exists | ✅ **the whole VS Code marketplace** | ❌ |
| **Custom CSS themes** | ❌ built-in light and dark only | ✅ fully configurable by CSS | ✅ | ✅ 7 preview + 30 editor themes |
| Dark theme | ✅ | ✅ | ✅ | ✅ |
| **Vim / Emacs keybindings** | ❌ | ❌ | ✅ by extension | ❔ |
| **Git integration** | ❌ | ❌ | ✅ built in | ❌ |
| Focus mode / typewriter scrolling | ❌ | ✅ | ⚠️ by extension | ❔ |
| Autosave | ❌ | ✅ | ✅ | ❔ |
| Multi-cursor editing | ❌ | ❌ | ✅ | ❔ |

This table is the reason to think twice. If you want to shape your editor — themes, keybindings,
plugins — VS Code wins and it is not close, and Typora's CSS theming beats a fixed light-and-dark
pair.

---

## Known gaps

Plainly, the things LiveMarkDownEditor does not do today:

- **Windows only.** It is a .NET 10 / WPF application. There is no macOS or Linux build, and that is
  a real limitation rather than a temporary one.
- **No LaTeX or math.** No KaTeX, no MathJax.
- **No plugin system, no custom CSS themes, no Vim mode, no multi-cursor.**
- **No `docx` / LaTeX / ePub export.** HTML, PDF, print, and rich-text copy only.
- **No autosave, focus mode, or typewriter scrolling.**
- **No sync, no mobile app, no collaborative cursors.** The live-update feature is about a file
  changing on disk, not about two people typing in one document at once.
- **It is young.** Typora has had years of bug reports to learn from; this has had far fewer.

---

## Sources

Competitor facts were taken from these pages, checked on 24 August 2026:

- [typora.io](https://typora.io/) — version, price, platforms, features
- Typora Support — [File Management](https://support.typora.io/File-Management/) and
  [Spellcheck](https://support.typora.io/Spellcheck/)
- Typora issue tracker — [reload on external change](https://github.com/typora/typora-issues/issues/165),
  [save-conflict handling](https://github.com/typora/typora-issues/issues/5209),
  [tabbed interface](https://github.com/typora/typora-issues/issues/6569),
  [definition lists](https://github.com/typora/typora-issues/issues/2351)
- [Markdown All in One on the VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=yzhang.markdown-all-in-one),
  [its repository](https://github.com/yzhang-gh/vscode-markdown), and its
  [documentation](https://markdown-all-in-one.github.io/docs/guide/)
- [Markdown and Visual Studio Code](https://code.visualstudio.com/docs/languages/markdown) ·
  [Markdown Footnotes](https://marketplace.visualstudio.com/items?itemName=bierner.markdown-footnotes) ·
  [Markdown Preview Mermaid Support](https://marketplace.visualstudio.com/items?itemName=bierner.markdown-mermaid)
- [Haroopad repository and README](https://github.com/rhiokim/haroopad) — feature set, GPL-3.0,
  last activity
