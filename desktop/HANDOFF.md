# Handoff: the WinUI 3 desktop app

Written 2026-10-20 at the end of a long session. Read this, then
`desktop/README.md`, before touching `desktop/`.

## Where it stands

`desktop/` is magpie's Windows app: a WinUI 3 shell that starts `magpie web`
beside itself (its own `MAGPIE_WEB_KEY` and loopback port) and draws the page
from that API. The Go backend is untouched — nothing under `internal/` is
modified for the app.

Branch `claude/current-progress-8d519b`, on top of the merged `f0bd2cb7`:

```
the icons every page's rows draw, read through the signed-in client
60117384  a handoff note for the next session
eb23abcf  the Settings item in the pane opens this app's Settings, not a webview
1da8d138  the pages say the page's own words, in the language Settings picked
7b00ddac  the window runs into the title bar over Mica; tray, links, login are the app's own
459150fc  Settings' Sync and backup drawn in XAML
254bf4cf  Settings' Allowances, Network, Models, Privacy and Observability drawn in XAML
```

All nine pages and all eight Settings parts are XAML now. Nothing under
`desktop/` falls back to `WebHostPage` except a community plugin's own page,
which is meant to stay a web page.

## The reported problem: missing icons — fixed

The user said "一堆地方缺图标" (lots of places are missing icons). It was
reproduced and fixed. Three separate faults, all in `desktop/`:

1. **The wrong route.** `IconOf` asked for `/api/icons/<name>.svg`, which
   serves only the pictures magpie stores for a provider the user gave one
   (`16 hex chars.ext`, `provider.IconFile`). A vendor's own mark is the
   page's static `/icons/<name>.svg`, served beside `app.js` and *not*
   behind `webGuard`. Asking `/api/icons/deepseek-color.svg` is a 404, so
   the picture was empty even when the request went through.
2. **No cookie on a stored picture.** A stored picture (`file:<name>`, and
   a plugin's) *is* behind `webGuard`, and an `Image`/`SvgImageSource`
   loading a `Uri` carries no cookie — 401. (The `?k=` Usage used to put in
   the address worked, but only because `webGuard` trades it for a cookie.)
3. **`currentColor` in an SVG.** 65 of the 171 marks are mono and paint
   with `fill="currentColor"`, which the page draws as a CSS mask tinted
   with the text colour. `SvgImageSource` has no page to take a colour
   from, so those paths had no paint at all.

What it is now (`Pages/PageBase.cs`):

- `PageKitUI.IconAddress` is the one place an icon name becomes an address:
  a vendor's is `icons/<name>.svg` (`.png` for `PageKitUI.PngIcons`), a
  stored one is `api/icons/<file>`, a plugin's own is its `https:`/`data:`
  URL.
- `PageKitUI.LoadIconsAsync(icons, redraw)` reads them through the signed-in
  `MagpieClient.GetBytesAsync`, decodes once into a cache shared by every
  page, then calls `redraw` — so a page draws at once and its logos appear a
  pass later. `IconCached` is what a draw uses; a failed read is kept as "no
  picture" and never retried on a redraw.
- `InkSvg` writes the theme's ink in for `currentColor`, and `App.UseTheme`
  drops the cache and redraws, so a theme change re-reads the marks.

Every page calls `LoadIconsAsync` from its own `LoadAsync`; grep for it to
find the ones that don't. Plugins asks the market first, because an installed
plugin with no picture of its own takes the market's.

**Not the bug:** a name magpie has no file for (`intern-ai.org.cn`,
`generic`) draws an empty slot, exactly as the page does. `MainWindow.xaml`
uses `FontIcon` glyphs in the pane, which is correct.

## What is left, in the order the user cares about

1. **The release shape**: still one `magpie-windows-<arch>.exe`; shipping two
   programs (the app + `magpie.exe`) is its own change — magpie-releases,
   self-update, the site's downloads.
2. **The panel's glide** from the Wails app (`internal/gui/app.go`'s
   `glidePanel`); the panel has acrylic now but no motion.
3. **`magpie://import`**: the link registers and opens Providers, and says it
   cannot apply yet. The Providers page has no import drawn.
4. **Backend-generated strings stay English**: what `settingsJSON` and the
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
- **Two icon routes, and only one is guarded.** `/icons/<name>` is the page's
  own static file and is served to anyone; `/api/icons/<file>` is a stored
  picture behind `webGuard`. Checking one and assuming the other is how the
  first fix looked right and drew nothing.
- **`Application.RequestedTheme` throws once the app has started**
  (`COMException`, and it takes the window down). The look is the window
  root's `RequestedTheme` (`ElementTheme`), which changes at run time; read
  the saved theme before the window is made, and set the root again after.
- **A `RequestedTheme` set after `L.Use` redraws the pane in English**: set
  the language first, then the theme. That was a regression caught only by
  walking the pane after a theme change.
- **UI Automation names are the words on screen**, so after a language or
  theme change the nav items are Chinese. Find them by name as drawn, and
  re-fetch the window element after a redraw — a stale one goes null.
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
