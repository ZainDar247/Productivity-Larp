import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { open as openDialog } from "@tauri-apps/plugin-dialog";
import exampleConfig from "../../config.example.json?raw";

export interface ConfigPayload {
  raw: string;
  error: string | null;
  configDir: string;
  firstRun: boolean;
}

export interface OpenPayload {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface Bridge {
  /** false when running in a plain browser (dev / tests) with a mock backend. */
  native: boolean;
  getConfig(): Promise<ConfigPayload>;
  saveConfig(json: string): Promise<void>;
  runNode(id: string): Promise<void>;
  closeOverlay(): Promise<void>;
  warpCursor(x: number, y: number): Promise<void>;
  loadImage(path: string): Promise<string>;
  openSettings(): Promise<void>;
  openConfigDir(): Promise<void>;
  pickFile(opts?: { title?: string; extensions?: string[] }): Promise<string | null>;
  on<T = unknown>(event: "menu:open" | "menu:close" | "config:changed", cb: (p: T) => void): Promise<() => void>;
}

const isNative = typeof window !== "undefined" && "__TAURI_INTERNALS__" in window;

const native: Bridge = {
  native: true,
  getConfig: () => invoke("get_config"),
  saveConfig: (json) => invoke("save_config", { json }),
  runNode: (id) => invoke("run_node", { id }),
  closeOverlay: () => invoke("close_overlay"),
  warpCursor: (x, y) => invoke("warp_cursor", { x, y }),
  loadImage: (path) => invoke("load_image", { path }),
  openSettings: () => invoke("open_settings"),
  openConfigDir: () => invoke("open_config_dir"),
  async pickFile(opts) {
    const r = await openDialog({
      multiple: false,
      directory: false,
      title: opts?.title,
      filters: opts?.extensions ? [{ name: "Files", extensions: opts.extensions }] : undefined,
    });
    return typeof r === "string" ? r : null;
  },
  on: (event, cb) => listen(event, (e) => cb(e.payload as never)),
};

// ---- browser mock: lets the UI run (and be tested) without Tauri ----
const KEY = "larp.mock.config";
const bus = new EventTarget();
const emit = (name: string, detail?: unknown) => bus.dispatchEvent(new CustomEvent(name, { detail }));

export const mockLog: string[] = [];
(globalThis as { __larpLog?: string[] }).__larpLog = mockLog;
// browser-mock only: lets tests push events (e.g. "config:changed") into the UI
(globalThis as { __larpEmit?: typeof emit }).__larpEmit = emit;

const mock: Bridge = {
  native: false,
  async getConfig() {
    let raw = exampleConfig;
    try { raw = localStorage.getItem(KEY) ?? exampleConfig; } catch { /* storage blocked */ }
    return { raw, error: null, configDir: "(browser mock)", firstRun: false };
  },
  async saveConfig(json) {
    try { localStorage.setItem(KEY, json); } catch { /* ignore */ }
    emit("config:changed");
  },
  async runNode(id) { mockLog.push(`run:${id}`); emit("menu:close"); },
  async closeOverlay() { mockLog.push("close"); emit("menu:close"); },
  async warpCursor(x, y) { mockLog.push(`warp:${Math.round(x)},${Math.round(y)}`); },
  async loadImage(path) { return path; },
  async openSettings() { mockLog.push("settings"); },
  async openConfigDir() { mockLog.push("open-config-dir"); },
  async pickFile() { return null; },
  async on(event, cb) {
    const h = (e: Event) => cb((e as CustomEvent).detail);
    bus.addEventListener(event, h);
    return () => bus.removeEventListener(event, h);
  },
};

export const bridge: Bridge = isNative ? native : mock;
export const emitMock = emit;
