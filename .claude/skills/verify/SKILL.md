---
name: verify
description: Build, launch, and drive the LiveMarkDownEditor WPF app to observe a change working. Use when verifying UI behaviour end-to-end rather than through tests.
---

# Verifying LiveMarkDownEditor

WPF app, `net10.0-windows`. Tests are headless STA (`StaThread.Run`) and **cannot** observe layout,
focus, undo, or command availability — drive the real app for anything in that list.

## Build and launch

A running `UI.exe` locks `bin/`, so always build to a scratch output:

```bash
dotnet build src/UI/UI.csproj -c Debug -o "<scratch>/app" --nologo -v q
```

Launch with an optional Startup Document (the app takes a `.md` path as argv[1] — the quickest way
to get a document with headings/tables in front of you without driving the Open dialog):

```powershell
$proc = Start-Process "<scratch>/app/UI.exe" -ArgumentList "<scratch>/test.md" -PassThru
```

**Poll for the window** — `MainWindowHandle` is 0 for a second or two after `Start-Process`, and
reading it too early yields an empty handle:

```powershell
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    $l = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    $l.Refresh()
    if ($l.MainWindowHandle -ne 0) { $h = $l.MainWindowHandle; break }
}
```

## Screenshot

`PrintWindow` with flag `2` (`PW_RENDERFULLCONTENT`) — flag 0 renders WPF content blank. Capture the
window even when it is not foreground.

## Drive it

- **Click into the editing area first** (`SetCursorPos` + `mouse_event`, ~(400, 300) window-relative).
  The `RichTextBox` does not have focus at startup, so `SendKeys` goes nowhere and you get a
  confusingly empty document.
- Then `SetForegroundWindow` + `[System.Windows.Forms.SendKeys]::SendWait(...)`, ~600ms between
  steps.
- Command bar hit points (maximized, 1936px wide): Source panel toggle ~(681, 55), Collapse all
  ~(435, 55), Expand all ~(523, 55). Turn the **Source panel** on to watch Capture happen live — it
  is the clearest proof an edit reached the Markdown Document.

## Gotchas

- **The Find Bar floats over the top-right**, which is exactly where the Source Panel is. Its first
  couple of source lines sit *behind* the bar — close the Find Bar (its ✕, ~(1875, 162)) before
  concluding the Source Panel is empty.
- **Escape only closes the Find Bar while focus is in the query/replacement box** — it is bound on
  those `TextBox.InputBindings`, not globally. From the editor, click the ✕ instead.
- **`RoutedUICommand.Execute()` bypasses `CanExecute`.** A command can be dead in the UI (greyed
  out) while its unit test passes happily. If a change adds or alters a command, assert
  `CanExecute` in the test *and* look at the button in a screenshot.
