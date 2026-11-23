import { useEffect, useState } from "react";
import { bridge } from "../lib/bridge";
import { buildItems } from "../lib/menuState";
import { DEFAULT_BEHAVIOR, DEFAULT_LAYOUT, parseConfig, resolveBehavior, resolveTheme, type Config, type MouseButton, type Theme, type ThemeConfig, type Trigger } from "../lib/types";
import { PRESETS, toHex } from "../lib/themePresets";
import { fitCount } from "../lib/geometry";
import { Ring } from "../ui/Ring";
import { ColorInput, Field, Slider } from "./widgets";
import exampleConfig from "../../config.example.json?raw";

type P = { cfg: Config; setCfg: (c: Config) => void };

// ---------------- Trigger ----------------
function keyName(e: KeyboardEvent): string | null {
  if (["Control", "Shift", "Alt", "Meta"].includes(e.key)) return null;
  if (/^Key[A-Z]$/.test(e.code)) return e.code.slice(3);
  if (/^Digit\d$/.test(e.code)) return e.code.slice(5);
  return e.code || e.key;
}

export function TriggerTab({ cfg, setCfg }: P) {
  const t = cfg.trigger;
  const [rec, setRec] = useState(false);
  const set = (nt: Trigger) => setCfg({ ...cfg, trigger: nt });

  useEffect(() => {
    if (!rec) return;
    const h = (e: KeyboardEvent) => {
      e.preventDefault();
      e.stopPropagation();
      if (e.key === "Escape") return setRec(false);
      const k = keyName(e);
      if (!k) return;
      const parts = [e.ctrlKey && "Ctrl", e.shiftKey && "Shift", e.altKey && "Alt", e.metaKey && "Super"].filter(Boolean) as string[];
      if (t.type === "keyboard") set({ ...t, shortcut: [...parts, k].join("+") });
      setRec(false);
    };
    window.addEventListener("keydown", h, true);
    return () => window.removeEventListener("keydown", h, true);
  });

  const hold = t.mode === "hold";
  return (
    <div className="form wide">
      <Field label="Open the menu with">
        <span className="seg">
          <button type="button" className={"seg-btn" + (t.type === "keyboard" ? " on" : "")}
            onClick={() => t.type !== "keyboard" && set({ type: "keyboard", shortcut: "Ctrl+Shift+Z", mode: t.mode, holdMs: t.holdMs })}>Keyboard shortcut</button>
          <button type="button" className={"seg-btn" + (t.type === "mouse" ? " on" : "")}
            onClick={() => t.type !== "mouse" && set({ type: "mouse", button: "middle", mode: t.mode, holdMs: t.holdMs })}>Mouse button</button>
        </span>
      </Field>

      {t.type === "keyboard" ? (
        <Field label="Shortcut" hint="Press Record, then the key combination. Note: Ctrl+Shift+Z is “Redo” in many apps — this shortcut will be taken away from them while the app runs.">
          <span className="row">
            <input value={rec ? "Press the keys… (Esc to cancel)" : t.shortcut} onChange={(e) => set({ ...t, shortcut: e.target.value })} spellCheck={false} />
            <button type="button" className={"btn" + (rec ? " primary" : "")} onClick={() => setRec(!rec)}>{rec ? "Cancel" : "Record"}</button>
          </span>
        </Field>
      ) : (
        <Field label="Button" hint={t.button === "left" || t.button === "right" ? "⚠ The click still reaches the app underneath, and left/right clicks are used constantly. Middle or side buttons work best." : "The click still reaches the app underneath."}>
          <select value={t.button} onChange={(e) => set({ ...t, button: e.target.value as MouseButton })}>
            <option value="middle">Middle (wheel click)</option>
            <option value="back">Side button — back</option>
            <option value="forward">Side button — forward</option>
            <option value="right">Right</option>
            <option value="left">Left</option>
          </select>
        </Field>
      )}

      <Field label="Behaviour">
        <select value={t.mode ?? "press"} onChange={(e) => set({ ...t, mode: e.target.value as "press" | "hold" })}>
          <option value="press">Press — open immediately (press again to close)</option>
          <option value="hold">Hold — open after holding for a moment</option>
        </select>
      </Field>
      {hold && (
        <Field label="Hold time"><Slider value={t.holdMs ?? 350} min={100} max={1500} step={50} unit=" ms" onChange={(holdMs) => set({ ...t, holdMs })} /></Field>
      )}
      <p className="muted small">The trigger is registered when you press <b>Save</b>. If the combination is already taken by another app you will see an error and the old one stays active.</p>
    </div>
  );
}

// ---------------- Appearance ----------------
const OVERRIDES: (keyof ThemeConfig)[] = ["accent", "sliceColor", "textColor", "mode"];

export function AppearanceTab({ cfg, setCfg }: P) {
  const th = resolveTheme(cfg);
  const set = (p: Partial<ThemeConfig>) => setCfg({ ...cfg, theme: { ...cfg.theme, ...p } });
  // picking a theme starts from that theme's own colours: drop the colour overrides
  const pick = (id: string) => {
    const next = { ...cfg.theme, preset: id };
    for (const k of OVERRIDES) delete next[k];
    setCfg({ ...cfg, theme: next });
  };
  const hasOverrides = OVERRIDES.some((k) => cfg.theme?.[k] !== undefined);
  const inner = Math.min(th.innerRadius, 120);
  const outer = inner + Math.min(th.thickness, 220);
  const sample = buildItems(cfg.menu, false, 0, fitCount(inner, outer)).items;
  const pad = outer + 14;
  return (
    <div className="two">
      <div className="form">
        <Field label="Theme">
          <div className="theme-grid" role="radiogroup" aria-label="Ring theme">
            {PRESETS.map((p) => (
              <button
                type="button"
                key={p.id}
                role="radio"
                aria-checked={th.preset === p.id}
                className={"theme-card" + (th.preset === p.id ? " on" : "")}
                onClick={() => pick(p.id)}
              >
                <span className="theme-swatch" style={{ background: `linear-gradient(135deg, ${p.accent}, ${p.accent2})` }}>
                  <span style={{ background: p.slice, borderColor: p.rim }} />
                </span>
                {p.label}
              </button>
            ))}
          </div>
        </Field>
        <Field label="Settings window"><select value={th.mode} onChange={(e) => set({ mode: e.target.value as Theme["mode"] })}><option value="dark">Dark</option><option value="light">Light</option></select></Field>
        <Field label="Overall size" hint="Scales the whole ring, text included. Lower it to make the menu smaller.">
          <Slider value={Math.round(th.scale * 100)} min={40} max={150} unit="%" onChange={(v) => set({ scale: v / 100 })} />
        </Field>
        <Field label="Centre size"><Slider value={cfg.theme?.innerRadius ?? DEFAULT_LAYOUT.innerRadius} min={30} max={140} unit=" px" onChange={(innerRadius) => set({ innerRadius })} /></Field>
        <Field label="Ring thickness"><Slider value={cfg.theme?.thickness ?? DEFAULT_LAYOUT.thickness} min={60} max={320} unit=" px" onChange={(thickness) => set({ thickness })} /></Field>
        <Field label="Gap between slices"><Slider value={cfg.theme?.gap ?? DEFAULT_LAYOUT.gap} min={0} max={24} unit=" px" onChange={(gap) => set({ gap })} /></Field>
        <Field label="Text size"><Slider value={cfg.theme?.fontSize ?? DEFAULT_LAYOUT.fontSize} min={9} max={24} unit=" px" onChange={(fontSize) => set({ fontSize })} /></Field>
        <label className="check"><input type="checkbox" checked={th.animation} onChange={(e) => set({ animation: e.target.checked })} /> Animations</label>

        <details className="adv-colors" open={hasOverrides}>
          <summary>Advanced colour overrides</summary>
          <p className="field-hint">Optional: these sit on top of the chosen theme. Picking a theme clears them.</p>
          <Field label="Highlight colour"><ColorInput value={cfg.theme?.accent} fallback={toHex(th.accent)} onChange={(v) => set({ accent: v ?? undefined })} /></Field>
          <Field label="Slice colour"><ColorInput value={cfg.theme?.sliceColor} fallback={toHex(th.sliceColor)} onChange={(v) => set({ sliceColor: v ?? undefined })} /></Field>
          <Field label="Text colour"><ColorInput value={cfg.theme?.textColor} fallback={toHex(th.textColor)} onChange={(v) => set({ textColor: v ?? undefined })} /></Field>
          <button type="button" className="btn" disabled={!hasOverrides} onClick={() => pick(th.preset)}>Clear overrides</button>
        </details>
      </div>
      <section className="pane preview">
        <h4>Live preview</h4>
        <svg viewBox={`${-pad} ${-pad} ${pad * 2} ${pad * 2}`} className="preview-svg">
          <Ring items={sample} highlight={sample.length ? 0 : null} innerR={inner} outerR={outer} theme={th} title="Preview" />
        </svg>
      </section>
    </div>
  );
}

// ---------------- Behaviour ----------------
/** Not part of config.json: registers/unregisters the app with Windows right away (no Save needed). */
function StartupToggle() {
  const [on, setOn] = useState<boolean | null>(null);
  const [err, setErr] = useState<string | null>(null);
  useEffect(() => { bridge.getAutostart().then(setOn).catch((e) => setErr(String(e))); }, []);
  return (
    <>
      <label className="check">
        <input type="checkbox" disabled={on === null} checked={!!on}
          onChange={(e) => { setErr(null); bridge.setAutostart(e.target.checked).then(setOn).catch((x) => setErr(String(x))); }} />
        Start productivity.larp when I sign in to Windows
      </label>
      <p className="muted small">It starts silently in the tray and keeps running in the background, so your shortcut works straight away. This applies immediately — no Save needed.</p>
      {err && <div className="banner err">{err}</div>}
    </>
  );
}

export function BehaviorTab({ cfg, setCfg }: P) {
  const b = resolveBehavior(cfg);
  const set = (p: Partial<typeof b>) => setCfg({ ...cfg, behavior: { ...cfg.behavior, ...p } });
  return (
    <div className="form wide">
      <Field label="Hover time before a slice fires" hint="The pointer must rest on a slice this long. 0 = instant (careful: any movement can trigger actions). A click always fires immediately.">
        <Slider value={b.dwellMs} min={0} max={1000} step={10} unit=" ms" onChange={(dwellMs) => set({ dwellMs })} />
      </Field>
      <label className="check">
        <input type="checkbox" checked={b.warpCursor} onChange={(e) => set({ warpCursor: e.target.checked })} />
        Move the cursor back to the centre when a new layer opens (recommended)
      </label>
      <p className="muted small">Defaults: {DEFAULT_BEHAVIOR.dwellMs} ms, re-centre on.</p>
      <h4>Background</h4>
      <StartupToggle />
      <h4>Keyboard (in the open menu)</h4>
      <ul className="muted">
        <li><kbd>←</kbd> <kbd>↑</kbd> / <kbd>→</kbd> <kbd>↓</kbd> or <kbd>Tab</kbd> — move around the ring</li>
        <li><kbd>Enter</kbd> / <kbd>Space</kbd> — select · <kbd>Backspace</kbd> — back · <kbd>Esc</kbd> — close</li>
        <li>Click in the centre circle — close</li>
      </ul>
    </div>
  );
}

// ---------------- Advanced ----------------
export function AdvancedTab({ cfg, setCfg, configDir, loadError, rawText }: P & { configDir: string; loadError: string | null; rawText: string }) {
  const [text, setText] = useState(rawText || JSON.stringify(cfg, null, 2));
  const [err, setErr] = useState<string | null>(loadError);
  useEffect(() => { setText(JSON.stringify(cfg, null, 2)); }, [cfg]);
  const apply = (raw: string) => {
    const r = parseConfig(raw);
    if (r.ok) { setCfg(r.config); setErr(null); } else setErr(r.error);
  };
  return (
    <div className="advanced">
      <div className="row wrap">
        <button type="button" className="btn" onClick={() => bridge.openConfigDir()}>Open config folder</button>
        <button type="button" className="btn" onClick={() => { if (confirm("Replace the whole configuration with the bundled example?")) apply(exampleConfig); }}>Reset to example</button>
        <span className="muted small">{configDir}</span>
      </div>
      <p className="muted small">Secrets belong in <code>.env</code> in that folder (see <code>.env.example</code>) — reference them as <code>{"${NAME}"}</code>. Never paste passwords into this JSON.</p>
      {err && <div className="banner err">{err}</div>}
      <textarea className="json" value={text} spellCheck={false} onChange={(e) => setText(e.target.value)} />
      <div className="row"><button type="button" className="btn primary" onClick={() => apply(text)}>Apply JSON</button><span className="muted small">Then press Save to store it.</span></div>
    </div>
  );
}
