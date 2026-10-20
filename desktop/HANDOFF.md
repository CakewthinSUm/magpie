# Handoff: the WinUI 3 desktop app — Agents and Providers, after the gap-fill

Written 2026-10-20, after PR #3 merged (squash `93c011d1` on main). Read
this, then `desktop/README.md`, before touching `desktop/`. The earlier
handoff's traps still hold; the ones this session hit are at the foot.

## Where it stands

All nine pages are XAML. The Agents and Providers pages are now drawn as the
web page draws them — PR #3's job. What each holds:

- **Agents** (`Pages/AgentsPage.xaml` + `.xaml.cs`, `.Detail.cs`,
  `.Models.cs`, `.Profiles.cs`): compact rows in one card, the connect switch
  and its four kinds (ok/empty/no/link), the status line under the name,
  CLI versions and update pills, the model-list dialog, arranging and
  hiding and Manage, profiles, install commands, the disconnect preview's
  line diff.
- **Providers** (`Pages/ProvidersPage.xaml` + `.xaml.cs`, `.Add.cs`,
  `.Editor.cs`, `.SignIn.cs`): rows with badges and key pills and arrange by
  right-click; the editor; the add sheet; the sign-in state machine.

## What the two pages still miss, for the next session to pick over

Gaps are in the order a user meets them. Line numbers are app.js's.

### Agents page

1. **The model picker is a flat menu, not the page's popover** — magpie's
   picker has the rail (All / Favorites / each provider group), a filter
   that takes any model id typed (`"use as typed"`), effort sliders instead
   of a menu for effort fields (`openPicker` 4518, `renderEffortPicker`),
   free/rate/context badges per row, "Disconnect from magpie" inside the
   model picker, and favorites. Ours is a `MenuFlyout` with groups; long
   lists (83 models) will be unwieldy.
2. **Ultracode is a menu, not a toggle** — the page's ultracode square
   switches on a click (`ultracodeToggle` 4453). Ours lists it among field
   options.
3. **Fast/ultrafast picks** — `POST /api/agent-fast` from the picker
   (#954, #1954) is not drawn (`fastTag`, Option.Fast/FastFor/Ultra fields
   are in the DTO already).
4. **The tiers square opens menus, not the model picker per tier** — ours
   forwards to the flat field menu; the page opens the model picker for
   that tier (with "Same as model" as the reset entry).
5. **omp's roles** are folded into tiers (`tierMenu` 3759) but
   `OMP_UNSET`'s per-role unset notes aren't said.
6. **Stale copies say only the count** — the page names each kind and how
   it is reopened (`staleSaid` 1275, `STALE_HOW`), and the Codex daemon row
   gets a Restart button (`POST codex/daemon/restart`).
7. **The "N new" models line** (bold, seen once, `newModels` 1239) and the
   models-entry under a row's name when it has no 「接入」 line.
8. **Aside's staged model write** (`agents/stage/{id}`,
   `runtime_unavailable` → `disconnect-offline`) is not handled — a stage
   failure will surface as a raw error.
9. **Get started card** (`guideShown` 1564, `POST settings/guide`,
   `groups/template`) is not drawn; ours shows the plain lead only.
10. **The panel variants** (row opens in place, effort bars, quota cards)
    live in the WebView2 panel (`PanelWindow`), not this page — unchanged
    by PR #3, listed so nobody "fixes" it.

### Providers page

11. **The editor's account rows are thinner than the page's** — no
    per-account usage-cap pill (`accountCapPill` 13476), lane pill
    (`laneLimitPill` 13560), per-account models (`accountModels` 13831),
    account rename (`provider/accountname`), pause of the active account
    (`login/on|off` when active+pausable), "First for now"/Returns wording,
    the quota read (`GET /api/login/usage`, cached 60s). The API routes all
    exist; the DTOs for Login/AccountJson are in `Backend/Api.cs`.
12. **Key accounts** (`renderKeyAccounts` 13926) are not drawn for key
    providers: per-key ticks (`keys/on|off`), rename (`keys/rename`),
    replace (`keys/replace`), per-key models/cap/lane, weights when routing
    is By weight, "Paste several"/"Add another key" (`keys/add`).
13. **The add sheet's new-provider editor is the short form** — the page's
    editor adds protocol segs with per-protocol URL keeping (#105, #1231),
    Detect APIs (`provider/detect`), curl paste, Fetch models
    (`provider/list`), regions (a relay's endpoints), icon choice
    (`POST /api/icons`, `icons/favicon`). None drawn; a custom provider
    added by the short form can be completed in the existing-provider
    editor's Advanced, which does carry Base URL… no — the existing-editor
    has no Base URL row either. **Base URL/protocol editing for a custom
    provider is missing in both**; the save body supports it
    (`chat/responses/anthropic/gemini/decide/baseAPI`).
14. **The models card has no vendor-list/picked-only/usage-only segs**
    (`chosen`/`none`), no Free only/Working only, no test dots
    (`provider/test` results per model, `tdot`), no Names & levels
    (`modelPrefs` name/same/price/images/api/efforts/reasonless) — the DTOs
    (`ModelPrefJson`, `PriceJson`) exist; only Kind is wired.
15. **Fallback, extra body, headers editing, auth header, pass
    User-Agent, unredacted, insecure TLS, balance** are not in the editor's
    Advanced (context/proxy/concurrency/RPM/priceRate/searches are).
    Fallback is `provider/save`'s `fallback` list; balance check is
    `provider/balance`.
16. **Draws** (`renderDrawers` 11438, image models under a provider) and
    Step Plan (`renderStepPlan` 12043) are not drawn.
17. **The add sheet's Import button guesses the agent** — it imports for
    the last-viewed importable subscription or falls back; the page's
    import offers a chooser. `StartImportFor(agent)` works; only the
    entry's UX is thin.
18. **Discovery** (`importapps/discovery`, the banner over the list) is not
    drawn; nor `magpie://import` payload (App.LinkSaid still says so).
19. **Upstream badges** (`markUpstream` 5601, `GET /api/upstream`,
    "API degraded") on provider rows are not drawn.
20. **Codex daemon/app notices** (`renderCodexDaemon` 12830,
    `renderCodexApp` 12877) are not drawn on the editor's account card.

### Cross-page

21. **L.T coverage**: the new pages call ~60 keys; all grepped against
    i18n.js before use, but a wording change in i18n.js or a key added
    there later needs `node desktop/gen-strings.mjs` rerun — Strings.cs is
    generated, never edited.
22. **`AgentsPage.InsideControl`** is shared by both pages' row-tap
    handlers; adding a new control type to it changes both pages.

## The traps this session hit (earlier ones are in the older handoff)

- **A ToggleSwitch fires Toggled when the draw sets IsOn.** Every page owns
  a `_drawing` (or `_editorDrawing`) guard set around the draw; a switch's
  handler no-ops under it. The editor resets `_pickedModels` in
  CloseEditor, or the next open draws the previous picks.
- **`maxConcurrency` is null-or-number** in /api/providers (a `*int` whose
  null follows what the provider's plugin says): the DTO is `int?`, not
  `int`, or the whole page fails to read with a JSON error. The other
  limits (queueLimit, queueWait, maxRPM, priceRate) are plain
  omitempty numbers.
- **A `WrapGrid` inside a StackPanel is a catastrophic failure**
  (COMException 0x8000FFFF, and the app dies): its width is unbounded under
  a StackPanel's measure. The add sheet's tiles sit in a horizontal
  StackPanel.
- **POST /api/open is a no-op under `magpie web`** (webHost.OpenURL does
  nothing): a link the app opens goes through `PageKitUI.OpenUrl`, the
  Windows shell — sign-ins included (the browser is opened by the app, once
  per flow, `_urlOpened`).
- **A provider save that omits a field keeps it for an existing provider,
  and drops it for a custom or new one** (providers.go's save): the editor
  sends id/from/name plus what changed; the add sheet's short form sends
  the whole body.
- **The app must be stopped before `dotnet build`** — the build can't
  replace Magpie.Desktop.exe (and `go build` can't replace magpie.exe) while
  either runs; a stale screenshot came from exactly that.
- **UI Automation: ToggleSwitch reads its state off TogglePattern**; a
  screenshot's knob position is not evidence. InvokePattern opens rows'
  editors; a row body itself needs a synthetic double-click at its
  coordinates (mouse_event).
- **UnhandledException surfaces as a dialog, then the window is gone** —
  after any UIA-driven click that ends in a COMException, check the process
  before reading the screen again.

## How this app is verified

Build (stop the app first; the .NET build cleans the output folder, so the
Go build goes after it):

```powershell
dotnet build desktop/Magpie.Desktop/Magpie.Desktop.csproj -c Debug -p:Platform=x64
go build -tags nogui -o desktop/Magpie.Desktop/bin/x64/Debug/net8.0-windows10.0.19041.0/magpie.exe .
```

Run it on a view: `Magpie.Desktop.exe providers`. It starts its own backend
(`magpie web` on a loopback port, its own key); a running magpie of the
user's is not touched. The app's config dir is `~/.config/magpie-desktop` —
worth knowing before a test changes a real setting. (This session's runs
used the real one; PR #3's verification changed nothing.)

Screenshot this checkout's window:

```powershell
pwsh desktop/shot.ps1 -Out desktop/shot.png
```

Drive the UI with UI Automation (`Add-Type -AssemblyName
UIAutomationClient,UIAutomationTypes`,
`AutomationElement::FromHandle($p.MainWindowHandle)`, `FindAll` by
NameProperty, `InvokePattern`/`TogglePattern`/`SelectionItemPattern`).

## Strings

`desktop/Magpie.Desktop/Strings.cs` is generated by
`node desktop/gen-strings.mjs`, from `internal/gui/assets/i18n.js` plus
`desktop/strings-extra.json`. Rerun it after changing a word a page says.
A key the page has no entry for stays English; two of those exist in the
add sheet and editor (marked `// no i18n key yet`).
