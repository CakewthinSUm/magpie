# magpie's Windows app (WinUI 3)

The desktop app for Windows, drawn with WinUI 3 instead of the page in a
webview. It is a program of its own: the tray icon, the quick panel and the
window are here, and magpie — the page, the API and the gateway — is the
`magpie.exe` beside it, started as `magpie serve --desktop`.

```
Magpie.Desktop.exe ──starts──▶ magpie.exe serve --desktop --handshake … --config-dir …
        │                                   │
        │  /api/… (loopback, key in a cookie)│  internal/gui (the page, the API, the gateway)
        │  /api/desktop/events (long poll) ──┘  internal/gui/desk.go
        ▼
  tray · panel · window · WebView2 for the pages not drawn here yet
```

## Why a second process

`internal/gui` already serves the page and the whole JSON API, and builds
without Wails (`-tags nogui`). Keeping that in Go means every rule the page
has — the strings in five languages, a click never moving it, the plugin
pages — holds for the pages not yet drawn in XAML, and the gateway is where
it always was.

The page asks for things only a window can do (`/api/window/fit`, a folder
picker). magpie queues those and the app reads them with a long poll
(`GET /api/desktop/events`), answering the ones that need an answer on
`POST /api/desktop/result`. That is the same split `build/dev.sh` uses
between a shell and a backend it restarts.

## Its own files

The app's magpie keeps its settings, accounts and gateway state in
`~/.config/magpie-desktop`, not in an installed magpie's `~/.config/magpie`
(`--config-dir`). A magpie the user already runs keeps its files untouched,
and the two never read each other's. A portable copy's `data` folder still
wins.

## What is drawn here, and what isn't

| Page | How |
| --- | --- |
| Agents | XAML (`Pages/AgentsPage`): the agents, their fields, and the app's own menu for a value |
| Providers | XAML (`Pages/ProvidersPage`): the sources, their keys and sign-ins, each switched on or off in place |
| Usage | XAML (`Pages/UsagePage`): the allowances each subscription and key has left, and a period's tokens, calls and cost by agent and model |
| Sessions | XAML (`Pages/SessionsPage`): one agent's sessions by day, the picked one's facts and resume command, and magpie's trash |
| Library | XAML (`Pages/LibraryPage`): the instructions and their sets, the MCP servers and the skills, each given to an agent or taken from it; editing text, adding a server and installing a skill stay the page's |
| Gateway | XAML (`Pages/GatewayPage`): the gateway's state and address, how to connect in each API, the caller keys, and the recent calls with their bodies a fold away |
| Routing | XAML (`Pages/RoutingPage`): the gateway's trace of each request, its story, and the routing groups |
| Plugins | XAML (`Pages/PluginsPage`): installed plugins, the built-in ones and their switches, and the market to install from |
| Settings | XAML (`Pages/SettingsPage`): every part — General and About, Allowances, Network and sharing, Models, Privacy, Observability and Sync and backup (`SettingsPage.Usage/Network/Models/Privacy/Otel/Sync.cs`) |

Each page drawn here takes one WebView2 away. A community plugin's page stays
a web page, as it is everywhere.

## Building

The app needs the .NET 8 SDK and the Windows App SDK; the backend beside it is
the Go build the releases already make:

```sh
dotnet build desktop/Magpie.Desktop/Magpie.Desktop.csproj -c Debug -p:Platform=x64
go build -tags nogui -o desktop/Magpie.Desktop/bin/x64/Debug/net8.0-windows10.0.19041.0/magpie.exe .
```

The Go build goes after the .NET one: `dotnet build` cleans the output folder.
The app finds `magpie.exe` beside itself, on `PATH`, or at `MAGPIE_EXE`.

`Assets/AppIcon.ico` is drawn from `build/icon/gen.go`, as every other icon is:

```sh
pwsh desktop/Magpie.Desktop/Assets/make-ico.ps1
```

## The shell

The window is drawn as Windows 11's own apps are: its content runs into the
title bar (`ExtendsContentIntoTitleBar`), so the NavigationView's top strip is
the caption — the app's name at the left, the system's buttons at the right,
the empty stretch between them dragging the window — and Mica (`MicaBackdrop`)
shows the desktop's wallpaper, blurred and tinted, behind it and the pane.

The tray icon says what the app does: a notification when the window closes
that magpie keeps running in the tray, and one for a `magpie://import` link,
which the Providers page has no import to answer yet.

Open at login is the app's own Run value, `magpie-desktop`, pointing at this
exe (`Autostart.cs`). It is not the backend's `magpie` value, which starts
`magpie.exe` — the Go program beside this one — so an installed magpie the
user already opens at login keeps its own and the two never take each
other's place. `magpie://` links register this exe as their handler
(`Scheme.cs`), as the Wails app registered its own.

## What this prototype doesn't do yet

- The panel's glide from the Wails app; a `magpie://import` link's provider,
  which needs the Providers page's import drawn.
- The release is still one `magpie-windows-<arch>.exe`; shipping two programs
  is its own change (magpie-releases, self-update, the site's downloads).
