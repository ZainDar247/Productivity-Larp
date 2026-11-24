// Builds the app, then starts it fully detached from this terminal: closing the terminal does NOT stop it.
// Quit it from its tray icon (right-click > Quit).
import { spawn, spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const exe = resolve(root, "app/Larp.App/bin/Release/net10.0-windows/productivity.larp.exe");

const b = spawnSync("dotnet", ["build", "app/Larp.App", "-c", "Release", "-nologo", "-v", "q"], { cwd: root, stdio: "inherit", shell: true });
if (b.status !== 0) {
  console.error("\nBuild failed. If productivity.larp is already running, quit it from its tray icon first (it locks its own files), then run this again.");
  process.exit(b.status ?? 1);
}
if (!existsSync(exe)) { console.error("Built, but " + exe + " was not found."); process.exit(1); }

const child = spawn(exe, process.argv.slice(2), { cwd: root, detached: true, stdio: "ignore", windowsHide: true });
child.unref();
console.log("productivity.larp is running in the background (tray icon). You can close this terminal; quit it from the tray.");
