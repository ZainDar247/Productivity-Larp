# productivity.larp

A global **radial-menu launcher** for Windows. Press a trigger (default **Ctrl+Shift+Z**) and a ring of your own
choices appears around the cursor. Rest the pointer on a slice and it fires — open an RDP file, start a program,
open a URL — or descends into a sub-menu. Everything (layers, actions, icons, theme, trigger) is configurable
from a built-in Settings window.

## How it behaves

- **Trigger:** keyboard shortcut, or a mouse button — each in *press* or *hold* mode. The trigger is swallowed,
  so the app underneath never receives your `Ctrl+Shift+Z` (or your middle-click).
- **Hover = select.** A slice fires after a short, configurable dwell (default 150 ms). Clicking a slice fires it instantly.
- **The pointer can't leave the menu:** selection depends on the *angle* from the centre, so every direction maps to a slice.
- **Layers:** a folder slice opens the next ring and re-centres the cursor. A **Back** slice (or `Backspace`) goes up.
- **Close:** click inside the centre circle, press `Esc`, or let an action finish — the menu closes itself.
- **Keyboard:** `←`/`↑` `→`/`↓`/`Tab` move around the ring, `Enter` selects, `Backspace` goes back.
- **No limits:** unlimited items and depth. A crowded ring paginates itself with a "More" slice.
- **Per-slice look:** icon, emoji, image, text, or icon/image + text; optional colour per slice.
- **Backdrop:** the menu freezes a screenshot of your screen and draws the ring over it, so it looks transparent on
  every Windows version and clicks can't leak through to other apps.

## Stack

| Layer | Choice |
|---|---|
| Backend | **ASP.NET Core MVC** (.NET 10, C#) — controllers = the JSON API + live event stream; Razor views host the UI |
| Windows shell | **C# WinForms** + **WebView2** — tray icon, low-level keyboard/mouse hooks, the overlay and settings windows |
| Frontend | **React 19 + TypeScript + Vite** — the ring is plain SVG |
| Core logic | `Larp.Core` — config model, `${ENV}` expansion, action runner, shortcut parser, trigger state machine (no Windows APIs) |

```
React UI (WebView2) ──HTTP + SSE──▶ ASP.NET Core MVC (Kestrel, 127.0.0.1) ──▶ Larp.Core
                                              ▲
                          Windows shell (hooks, windows, tray) implements IDesktop
```

### Security of the local server

The MVC backend can launch programs, so it is locked down: it listens on **127.0.0.1 only**, on a **random port**;
every request except the static bundle needs a **secret token generated at each launch** (only the app's own
windows know it); a **Host-header check** blocks DNS-rebinding from web pages. The UI never sends commands — it only
says *which menu item id* was chosen, and the server looks the action up in its own copy of the config.

## Getting started (Windows)

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node 20+](https://nodejs.org), and the
WebView2 Runtime (already part of Windows 10/11).

```powershell
npm install
copy .env.example .env      # then edit it — .env is git-ignored
npm run app                 # builds the UI, then runs the app (tray icon appears)
npm run app:build           # publishes to ./publish
```

On first launch a starter menu is written and the Settings window opens. The app then lives in the tray.
If the foreground window is an *elevated* (run-as-administrator) program, Windows doesn't let a normal app's hooks
see its keys — run productivity.larp as administrator if you need the trigger to work over those.

### Your files

| What | Where |
|---|---|
| Config | `%APPDATA%\larp.productivity\config.json` (override the folder with `LARP_CONFIG_DIR`) |
| Secrets | `.env` in that folder, or `./.env` when running from the repo, or real environment variables |

Use secrets in the config as `${NAME}`, e.g. `"file": "${RDP_DIR}\\admin\\server1.rdp"`. A missing variable
aborts the action with a clear error instead of launching something half-formed.
`.env`, `node_modules`, build output and `*.rdp` files are in `.gitignore` — never commit them.

## Action types

```jsonc
{ "type": "rdp",      "file": "${RDP_DIR}\\admin\\server1.rdp", "args": ["/admin"] }  // mstsc.exe <file>
{ "type": "open",     "path": "${USERPROFILE}\\Downloads" }                          // default handler
{ "type": "run",      "command": "wt.exe", "args": [], "cwd": "...", "hidden": false }
{ "type": "url",      "url": "https://github.com" }
{ "type": "settings" }
```

A node with `children` is a sub-menu; a node with an `action` runs it. See [`config.example.json`](config.example.json).

## Develop & test

```bash
npm test                         # vitest: ring geometry, menu state machine, tree editing (26)
dotnet test app/Larp.sln         # config, env, actions, shortcuts, trigger machine, store + the MVC API & security (70)
npm run dev                      # UI alone in a browser, mock backend: http://localhost:1420/?view=overlay
npm run build && dotnet run --project app/Larp.DevHost   # REAL MVC backend + fake desktop, on any OS
```

`Larp.DevHost` runs the actual MVC backend with a fake desktop, so the full stack (React → HTTP/SSE → controllers →
core) can be developed and tested on Linux/macOS. Only the Windows shell (hooks, windows, tray) needs Windows to run;
it compiles anywhere (`EnableWindowsTargeting`).

## Layout

```
src/                    React UI  (overlay/ = the ring, settings/ = the editor, lib/ = pure logic)
app/Larp.Core/          config, env expansion, actions, shortcut + trigger logic   (net10.0)
app/Larp.Web/           ASP.NET Core MVC: controllers, Razor views, SSE, security   (net10.0)
app/Larp.App/           Windows shell: hooks, overlay, settings, tray               (net10.0-windows)
app/Larp.DevHost/       MVC backend + fake desktop for dev/tests on any OS
app/Larp.Tests/         xUnit
assets/                 app icon
config.example.json  .env.example
```
