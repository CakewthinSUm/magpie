# Handoff: the WinUI 3 desktop app

Written 2026-10-20 at the end of a long session. Read this, then
`desktop/README.md`, before touching `desktop/`.

## Where it stands

`desktop/` is magpie's Windows app: a WinUI 3 shell that starts `magpie web`
beside itself (its own `MAGPIE_WEB_KEY` and loopback port) and draws the page
from that API. The Go backend is untouched — nothing under `internal/` is
modified for the app.

Branch `claude/current-progress-8d519b`, five commits on top of the merged
`f0bd2cb7`:

```
eb23abcf  the Settings item in the pane opens this app's Settings, not a webview
1da8d138  the pages say the page's own words, in the language Settings picked
7b00ddac  the window runs into the title bar over Mica; tray, links, login are the app's own
459150fc  Settings' Sync and backup drawn in XAML
254bf4cf  Settings' Allowances, Network, Models, Privacy and Observability drawn in XAML
```

All nine pages and all eight Settings parts are XAML now. Nothing under
`desktop/` falls back to `WebHostPage` except a community plugin's own page,
which is meant to stay a web page.

## The reported problem: missing icons

The user said "一堆地方缺图标" (lots of places are missing icons) and asked for
a fresh session. **Start by finding them** — it was not reproduced before the
session ended.

Where icons come from:

- `PageKitUI.IconOf(string? icon)` (`desktop/Magpie.Desktop/Pages/PageBase.cs`)
  builds an `ImageSource` from `/api/icons/<file>`; SVG through
  `SvgImageSource`, PNG through `BitmapImage`. A vendor whose logo is a PNG is
  listed in `PageKitUI.PngIcons`.
- The pages bind it in XAML through an `x:Bind` converter or set it in code.
  Grep for `IconOf` and for `Source=` in `Pages/*.xaml`.
- **Suspicion worth checking first:** `IconOf` builds a plain `Uri`, and an
  `Image`/`BitmapImage` loading a URL has no cookie — magpie's `webGuard`
  turns such a request away (401). `MagpieClient.GetBytesAsync` exists for
  exactly this reason and is what the comment on it says. If icons are blank,
  that is the likely cause: the bytes must be fetched through the client and
  handed to the control as a stream, not left to the control to load.
- Also check which rows never asked for an icon at all: the Settings tabs, the
  tray menu, the nav pane's items (`MainWindow.xaml` uses `FontIcon` glyphs
  there, which is correct and is not the bug).

Reproduce by opening the app on Agents, Providers, Usage and Plugins and
looking at the rows.

## What is left, in the order the user cares about

1. **The missing icons** (above).
2. **The release shape**: still one `magpie-windows-<arch>.exe`; shipping two
   programs (the app + `magpie.exe`) is its own change — magpie-releases,
   self-update, the site's downloads.
3. **The panel's glide** from the Wails app (`internal/gui/app.go`'s
   `glidePanel`); the panel has acrylic now but no motion.
4. **`magpie://import`**: the link registers and opens Providers, and says it
   cannot apply yet. The Providers page has no import drawn.
5. **Backend-generated strings stay English**: what `settingsJSON` and the
   usage API return (a window's name, a reset time) is magpie's own text and
   is not in `i18n.js`.

## How this app is verified

Build (the .NET build cleans the output folder, so the Go build goes after it):

```powershell
dotnet build desktop/Magpie.Desktop/Magpie.Desktop.csproj -c Debug -p:Platform=x64
go build -tags nogui -o desktop/Magpie.Desktop/bin/x64/Debug/net8.0-windows10.0.19041.0/magpie.exe .
```

Run it on a view: `Magpie.Desktop.exe "settings&tab=sync"`. It starts its own
backend; a running `magpie.exe` of the user's is not touched.

Screenshot this checkout's window:

```powershell
pwsh desktop/shot.ps1 -Out desktop/shot.png
```

Drive the real UI with UI Automation (this is how every claim in the commit
bodies was checked) — `Add-Type -AssemblyName UIAutomationClient,
UIAutomationTypes`, then `AutomationElement::FromHandle($p.MainWindowHandle)`,
`FindAll` by `NameProperty` / `ControlTypeProperty`, and
`SelectionItemPattern` / `InvokePattern` to act. PasswordBox has no
`ValuePattern`; type into it another way.

Read what a save actually wrote: `~/.config/magpie/settings.json` (the app
runs `magpie web` with no `--config-dir`, so it uses the same folder an
installed magpie does — worth knowing before a test changes a real setting).

## Traps this session hit

- **Click the pane, not the command line.** A view passed as an argument skips
  `Nav_SelectionChanged`. That is how `Tag="Settings"` (capital S) went
  unnoticed and the gear drew a WebView2 page. Test the path the user takes.
- `L.Walk` translates a drawn page by walking the visual tree; a flyout's
  items are not in it, so `PageKitUI.Menu` and `Choices` translate where they
  build. A new control with a `string` label needs one of the two.
- `PageKit.RunAsync` is the one place a page's text is walked for translation
  after a draw — a page that draws outside it will not be translated.
- `SaveAsync` sends the whole settings snapshot with one field overlaid,
  because `POST /api/settings` carries forward only what other pages own.
  A new bulk-saved field must be added to `Snapshot()` or a save resets it.
- Writing HKCU's Run key (Open at login) is refused by this session's
  permission rules, so `Autostart.Set`'s write path is still unexercised.

## Strings

`desktop/Magpie.Desktop/Strings.cs` is generated (1.5 MB, 3732 keys × zh,
zh-TW, ja, de) by `node desktop/gen-strings.mjs`, from
`internal/gui/assets/i18n.js` plus `desktop/strings-extra.json`. Rerun it
after changing a word a page says.
