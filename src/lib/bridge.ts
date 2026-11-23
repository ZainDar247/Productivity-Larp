import exampleConfig from "../../config.example.json?raw";

export interface ConfigPayload {
  raw: string;
  error: string | null;
  configDir: string;
  firstRun: boolean;
}

export interface OpenPayload {
  /** Cursor position inside the overlay, CSS px. */
  x: number;
  y: number;
  /** Overlay size, CSS px. */
  w: number;
  h: number;
  /** URL of the frozen desktop picture to draw behind the ring (absent in the browser mock). */
  shot?: string;
}

export type BridgeEvent = "menu:open" | "menu:close" | "config:changed";

export interface Bridge {
  /** false when running in a plain browser (npm run dev) with a mock backend. */
  native: boolean;
  getConfig(): Promise<ConfigPayload>;
  saveConfig(json: string): Promise<void>;
  runNode(id: string): Promise<void>;
  closeOverlay(): Promise<void>;
  /** The overlay drew its first frame — the shell may now reveal the window. */
  overlayReady(): Promise<void>;
  warpCursor(x: number, y: number): Promise<void>;
  loadImage(path: string): Promise<string>;
  openSettings(): Promise<void>;
  openConfigDir(): Promise<void>;
  pickFile(opts?: { title?: string; extensions?: string[] }): Promise<string | null>;
  /** Add the auth token to a same-origin URL (for <img src>). */
  assetUrl(path: string): string;
  on<T = unknown>(event: BridgeEvent, cb: (p: T) => void): Promise<() => void>;
}

// The ASP.NET Core MVC page that hosts this bundle injects a per-launch secret.
const token: string | undefined = (window as unknown as { __LARP_TOKEN?: string }).__LARP_TOKEN || undefined;
const isNative = token !== undefined;

// ---------------- real backend: ASP.NET Core MVC over loopback HTTP ----------------
async function api<T = void>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(path, {
    method,
    headers: { "X-Larp-Token": token!, ...(body !== undefined ? { "Content-Type": "application/json" } : {}) },
    body: body !== undefined ? JSON.stringify(body) : undefined,
    cache: "no-store",
  });
  if (!res.ok) {
    const j = (await res.json().catch(() => null)) as { error?: string } | null;
    throw new Error(j?.error ?? `${res.status} ${res.statusText}`);
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

let source: EventSource | undefined;
const events = () => (source ??= new EventSource(`/events?t=${encodeURIComponent(token!)}`));

const native: Bridge = {
  native: true,
  getConfig: () => api("GET", "/api/config"),
  saveConfig: (json) => api("PUT", "/api/config", { json }),
  runNode: (id) => api("POST", `/api/run/${encodeURIComponent(id)}`),
  closeOverlay: () => api("POST", "/api/overlay/close"),
  overlayReady: () => api("POST", "/api/overlay/ready"),
  warpCursor: (x, y) => api("POST", "/api/cursor", { x, y }),
  loadImage: async (path) => (await api<{ dataUrl: string }>("GET", `/api/image?path=${encodeURIComponent(path)}`)).dataUrl,
  openSettings: () => api("POST", "/api/settings/open"),
  openConfigDir: () => api("POST", "/api/config-dir/open"),
  pickFile: async (opts) => (await api<{ path: string | null }>("POST", "/api/pick-file", { title: opts?.title, extensions: opts?.extensions })).path,
  assetUrl: (p) => `${p}${p.includes("?") ? "&" : "?"}t=${encodeURIComponent(token!)}`,
  async on(event, cb) {
    const es = events();
    const h = (e: Event) => cb(JSON.parse((e as MessageEvent<string>).data));
    es.addEventListener(event, h);
    return () => es.removeEventListener(event, h);
  },
};

// ---------------- browser mock: lets the UI run (and be tested) with no backend ----------------
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
  async overlayReady() { mockLog.push("ready"); },
  async warpCursor(x, y) { mockLog.push(`warp:${Math.round(x)},${Math.round(y)}`); },
  async loadImage(path) { return path; },
  async openSettings() { mockLog.push("settings"); },
  async openConfigDir() { mockLog.push("open-config-dir"); },
  async pickFile() { return null; },
  assetUrl: (p) => p,
  async on(event, cb) {
    const h = (e: Event) => cb((e as CustomEvent).detail);
    bus.addEventListener(event, h);
    return () => bus.removeEventListener(event, h);
  },
};

export const bridge: Bridge = isNative ? native : mock;
export const emitMock = emit;
