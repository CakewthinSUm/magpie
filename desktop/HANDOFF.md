# Handoff: the WinUI 3 desktop app

Written 2026-10-20 at the end of a long session. Read this, then
`desktop/README.md`, before touching `desktop/`.

## Where it stands

`desktop/` is magpie's Windows app: a WinUI 3 shell that starts `magpie web`
beside itself (its own `MAGPIE_WEB_KEY` and loopback port) and draws the page
from that API. The Go backend is untouched — nothing under `internal/` is
modified for the app.

All nine pages and all eight Settings parts are XAML. The Agents and
Providers pages are drawn as the web page draws them (compact rows, the
connect switch, the CLI's update pill, the model-list dialog, profiles; the
provider editor, the add sheet, the subscription sign-ins with the plugins').
What is still the page's own is said in `desktop/README.md`'s "What this
prototype doesn't do yet".

## How the pages are drawn now

The Agents page is `Pages/AgentsPage.xaml` plus three partials:
`AgentsPage.xaml.cs` (the rows, the switch, the CLI, arranging), `Detail`
(the opened row's fields, the tiers square, the picker menu), `Models` (the
model-list dialog), `Profiles` (the chips, the manage dialog). It reads
GET /api/state and draws again from every write's answer, which is the whole
state.

The Providers page is `Pages/ProvidersPage.xaml` plus partials: the main
file draws the rows, the banners and the arrange; `Editor` draws a provider's
editor; `Add` draws the add sheet and a new provider's editor; `SignIn` runs
a subscription's sign-in (built-in and plugin) and the account import.

## Strings

`desktop/Magpie.Desktop/Strings.cs` is generated (3732 keys × zh, zh-TW, ja,
de) by `node desktop/gen-strings.mjs`, from `internal/gui/assets/i18n.js`
plus `desktop/strings-extra.json`. Rerun it after changing a word a page
says. `L.T` looks a word up by the English the page writes; a word with no
key stays English.

## How this app is verified

Build (the .NET build cleans the output folder, so the Go build goes after it):

```powershell
dotnet build desktop/Magpie.Desktop/Magpie.Desktop.csproj -c Debug -p:Platform=x64
go build -tags nogui -o desktop/Magpie.Desktop/bin/x64/Debug/net8.0-windows10.0.19041.0/magpie.exe .
```

Run it on a view: `Magpie.Desktop.exe "settings&tab=sync"`. It starts its own
backend; a running `magpie.exe` of the user's is not touched. The app's own
config dir is `~/.config/magpie-desktop`.

Screenshot this checkout's window:

```powershell
pwsh desktop/shot.ps1 -Out desktop/shot.png
```

Drive the real UI with UI Automation — `Add-Type -AssemblyName
UIAutomationClient,UIAutomationTypes`, then
`AutomationElement::FromHandle($p.MainWindowHandle)`, `FindAll` by
`NameProperty` / `ControlTypeProperty`, and `SelectionItemPattern` /
`InvokePattern` / `TogglePattern` to act. ToggleSwitch reads its state off
TogglePattern, not off the screen. PasswordBox has no `ValuePattern`; type
into it another way.

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
  the language first, then the theme.
- **UI Automation names are the words on screen**, so after a language or
  theme change the nav items are Chinese. Find them by name as drawn, and
  re-fetch the window element after a redraw — a stale one goes null.
- **A ToggleSwitch fires Toggled when the draw sets IsOn.** Every page owns a
  `_drawing` guard set around the draw; a switch's handler no-ops under it.
- **POST /api/open is a no-op under `magpie web`** (webHost.OpenURL does
  nothing): a link the app opens goes through `PageKitUI.OpenUrl`, the
  Windows shell.
- **A provider save that omits a field keeps it for an existing provider,
  and drops it for a custom or new one.** The editor therefore sends only
  what changed for an existing provider, and carries the whole form for a
  new one.
- Writing HKCU's Run key (Open at login) is refused by this session's
  permission rules, so `Autostart.Set`'s write path is still unexercised.
