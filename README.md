# productivity.larp

A global **radial-menu launcher** for Windows. Press a trigger (default **Ctrl+Shift+Z**) and a ring of your own
choices appears around the cursor. Rest the pointer on a slice and it fires — open an RDP file, start a program,
open a URL — or descends into a sub-menu. Everything (layers, actions, icons, theme, trigger) is configurable
from a built-in Settings window.

## How it behaves

- **Trigger:** keyboard shortcut, or a mouse button — each in *press* or *hold* mode.
- **Hover = select.** A slice fires after a short, configurable dwell (default 150 ms). Clicking a slice fires it instantly.
- **The pointer can't leave the menu:** selection depends on the *angle* from the centre, so every direction maps to a slice.
- **Layers:** a folder slice opens the next ring and re-centres the cursor. A **Back** slice (or `Backspace`) goes up.
- **Close:** click inside the centre circle, press `Esc`, or let an action finish — the menu closes itself.
- **Keyboard:** `←`/`↑` `→`/`↓`/`Tab` move around the ring, `Enter` selects, `Backspace` goes back.
- **No limits:** unlimited items and depth. A crowded ring paginates itself with a “More” slice.
- **Per-slice look:** icon, emoji, image, text, or icon/image + text; optional colour per slice.

## Stack

| Layer | Choice | Why |
|---|---|---|
| Shell / native | **Tauri 2 (Rust)** | Much lighter than Electron (it reuses the WebView2 already in Windows instead of bundling Chromium) — not yet measured here |
| UI | **React 19 + TypeScript + Vite** | ring is plain SVG, no UI framework |
| Core logic | `crates/larp-core` (Rust) | config model, `${ENV}` expansion, action runner — GUI-free and unit-tested |
| Config | JSON, validated twice (Zod in the UI, serde in Rust) | a bad config shows a message instead of crashing |

## Getting started (Windows)

Prerequisites: [Node 20+](https://nodejs.org), [Rust](https://rustup.rs) (with the MSVC build tools), WebView2 (already on Windows 10/11).

```powershell
npm install
copy .env.example .env      # then edit it — .env is git-ignored
npm run app                 # = tauri dev   (first build takes a few minutes)
npm run app:build           # produces an installer in src-tauri/target/release/bundle
```

On first launch a starter menu is written and the Settings window opens. The app then lives in the tray.

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
The webview never sends actions to the backend — it only reports *which node id* was chosen, and the backend looks
the action up in its own copy of the config.

## Develop & test

```bash
npm run dev          # UI in a browser at http://localhost:1420/?view=overlay  (mock backend)
npm test             # vitest: ring geometry, menu state machine, tree editing
cargo test -p larp-core
npm run typecheck
```

`?view=overlay` and `?view=settings` select which window the bundle renders; in a plain browser a mock backend
stands in for Tauri so the whole UI can be exercised without Windows.

## Layout

```
src/            React UI  (overlay/ = the ring, settings/ = the editor, lib/ = pure logic)
crates/larp-core/   config, env expansion, actions (Rust, no GUI deps)
src-tauri/      Tauri glue: windows, tray, hotkey + mouse trigger, commands
config.example.json  .env.example
```
