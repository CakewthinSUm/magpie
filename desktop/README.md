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
| Agents | XAML (`Pages/AgentsPage`): the agents as the page draws them — a compact row per agent in one card, the connect switch, the CLI's version and its update, the model list dialog, arranging and hiding, profiles, and the agents this computer lacks with their install commands |
| Providers | XAML (`Pages/ProvidersPage`): the sources as the page draws them — a row per provider with its key or account, the editor on it (accounts, keys, models, routing, the advanced fields), and the add sheet with the presets, the subscriptions' sign-ins (a plugin's included) and the user's own |
| Usage | XAML (`Pages/UsagePage`): the allowances each subscription and key has left, and a period's tokens, calls and cost by agent and model; its Requests tab lists the period's calls a page at a time, with the agents, providers, status and search as filters. The page's Sessions and Context tabs are not drawn here yet |
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

A page with tabs of its own — Usage, Library, Plugins and Settings — lists them
in the pane under its row, as magpie's page lists them under the open page when
the window is wide enough (`app.js`'s `navSubs`): each tab's row carries the
view it opens (`usage&tab=requests`), the counts the page puts beside a tab
(`Installed · 3`, `Skills · 4`) and, for a plugin with an update, the accent
badge. The tab in sight is the row selected, and the page's own strip of tabs
is hidden while the pane lists them — in a narrow window, whose pane is icons
only, the strip comes back and the rows are not drawn. A page implements
`ITabbed` (`Pages/Tabs.cs`) and the window does the rest. Settings is a footer
menu item rather than the NavigationView's own Settings item, which is the
only kind of row that can carry children at the pane's foot.

The tray icon says what the app does: a notification when the window closes
that magpie keeps running in the tray, and one for a `magpie://import` link,
which the Providers page has no import to answer yet (a provider can be
added by hand; a link's own payload is applied by the page's import, which
is not drawn here).

Open at login is the app's own Run value, `magpie-desktop`, pointing at this
exe (`Autostart.cs`). It is not the backend's `magpie` value, which starts
`magpie.exe` — the Go program beside this one — so an installed magpie the
user already opens at login keeps its own and the two never take each
other's place. `magpie://` links register this exe as their handler
(`Scheme.cs`), as the Wails app registered its own.

## The words

The pages say what the page says, in the language Settings picked. The
translations are the page's own (`internal/gui/assets/i18n.js`), keyed by the
English as the page writes it, so a word the page already translates is the
app's translation too: `desktop/gen-strings.mjs` reads i18n.js and writes
`Magpie.Desktop/Strings.cs`, and `L.T` looks a word up in it. A word the page
has no key for goes in `desktop/strings-extra.json`, which the script merges
after i18n.js.

```sh
node desktop/gen-strings.mjs   # after changing a word a page says
```

`L.Walk` translates a page's text once it is drawn, so a page's own code
needn't wrap each word; a word the table doesn't know stays English, as the
page leaves it. What magpie's API returns (a window's name, a reset time) is
magpie's own and is not translated here.

## What this prototype doesn't do yet

- The panel's glide from the Wails app; a `magpie://import` link's payload.
- The Providers page's import from other apps (the add sheet's Import button
  says so), and a few of the editor's long-tail fields (a StepFun platform
  sign-in, Volcengine and Tencent access keys per region, a Vertex
  credentials file).
- The release is still one `magpie-windows-<arch>.exe`; shipping two programs
  is its own change (magpie-releases, self-update, the site's downloads).
