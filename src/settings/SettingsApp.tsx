import { useCallback, useEffect, useMemo, useState } from "react";
import { Save, Undo2 } from "lucide-react";
import { bridge } from "../lib/bridge";
import { parseConfig, resolveTheme, triggerSummary, type Config, type NodeT } from "../lib/types";
import { MenuEditor } from "./MenuEditor";
import { AdvancedTab, AppearanceTab, BehaviorTab, TriggerTab } from "./Tabs";

type Tab = "menu" | "trigger" | "appearance" | "behavior" | "advanced";
const TABS: { id: Tab; label: string }[] = [
  { id: "menu", label: "Menu" },
  { id: "trigger", label: "Trigger" },
  { id: "appearance", label: "Appearance" },
  { id: "behavior", label: "Behaviour" },
  { id: "advanced", label: "Advanced" },
];

const ser = (c: Config) => JSON.stringify(c, null, 2);

export function SettingsApp() {
  const [cfg, setCfg] = useState<Config | null>(null);
  const [saved, setSaved] = useState("");
  const [tab, setTab] = useState<Tab>("menu");
  const [info, setInfo] = useState({ configDir: "", rawText: "", loadError: null as string | null, firstRun: false });
  const [status, setStatus] = useState<{ kind: "ok" | "err"; msg: string } | null>(null);

  const load = useCallback(async () => {
    const p = await bridge.getConfig();
    const r = parseConfig(p.raw);
    setInfo({ configDir: p.configDir, rawText: p.raw, loadError: p.error ?? (r.ok ? null : r.error), firstRun: p.firstRun });
    if (r.ok) { setCfg(r.config); setSaved(ser(r.config)); } else { setCfg(null); setTab("advanced"); }
  }, []);
  useEffect(() => { load(); }, [load]);

  const dirty = cfg ? ser(cfg) !== saved : false;
  const theme = useMemo(() => (cfg ? resolveTheme(cfg) : null), [cfg]);
  useEffect(() => { document.documentElement.dataset.theme = theme?.mode ?? "dark"; }, [theme?.mode]);

  const save = useCallback(async () => {
    if (!cfg) return;
    const json = ser(cfg);
    const v = parseConfig(json);
    if (!v.ok) return setStatus({ kind: "err", msg: v.error });
    try {
      await bridge.saveConfig(json);
      setSaved(json);
      setStatus({ kind: "ok", msg: "Saved — the menu is live." });
    } catch (e) {
      setStatus({ kind: "err", msg: String(e) });
    }
  }, [cfg]);

  useEffect(() => {
    const h = (e: KeyboardEvent) => { if ((e.ctrlKey || e.metaKey) && e.key === "s") { e.preventDefault(); save(); } };
    window.addEventListener("keydown", h);
    return () => window.removeEventListener("keydown", h);
  }, [save]);
  useEffect(() => { if (!status) return; const t = setTimeout(() => setStatus(null), status.kind === "ok" ? 3000 : 9000); return () => clearTimeout(t); }, [status]);

  const setMenu = (menu: NodeT[]) => cfg && setCfg({ ...cfg, menu });

  return (
    <div className="settings">
      <header className="top">
        <h1>productivity<span>.larp</span></h1>
        <nav className="tabs" role="tablist">
          {TABS.map((t) => (
            <button key={t.id} role="tab" aria-selected={tab === t.id} className={"tab" + (tab === t.id ? " on" : "")} onClick={() => setTab(t.id)}>{t.label}</button>
          ))}
        </nav>
        <span className="spacer" />
        {status && <span className={"status " + status.kind} role="status">{status.msg}</span>}
        {dirty && <span className="dot" title="Unsaved changes" />}
        <button className="btn" disabled={!dirty} onClick={load}><Undo2 size={14} /> Revert</button>
        <button className="btn primary" disabled={!dirty || !cfg} onClick={save}><Save size={14} /> Save</button>
      </header>

      {info.firstRun && <div className="banner info">Welcome! A starter menu was created. Edit it below, then press <b>Save</b>. Your trigger is <b>{cfg ? triggerSummary(cfg) : ""}</b>.</div>}
      {info.loadError && cfg && <div className="banner err">Config problem: {info.loadError}</div>}

      <main className="content">
        {cfg && tab === "menu" && theme && <MenuEditor cfg={cfg} theme={theme} setMenu={setMenu} />}
        {cfg && tab === "trigger" && <TriggerTab cfg={cfg} setCfg={setCfg} />}
        {cfg && tab === "appearance" && <AppearanceTab cfg={cfg} setCfg={setCfg} />}
        {cfg && tab === "behavior" && <BehaviorTab cfg={cfg} setCfg={setCfg} />}
        {tab === "advanced" && (cfg
          ? <AdvancedTab cfg={cfg} setCfg={setCfg} configDir={info.configDir} loadError={info.loadError} rawText={info.rawText} />
          : <div className="advanced">
              <div className="banner err">Your config could not be loaded: {info.loadError}</div>
              <p className="muted">The app is running on the built-in defaults. Fix the JSON below and apply it.</p>
              <BrokenEditor raw={info.rawText} onFixed={(c) => { setCfg(c); setTab("menu"); }} />
            </div>)}
      </main>
    </div>
  );
}

function BrokenEditor({ raw, onFixed }: { raw: string; onFixed: (c: Config) => void }) {
  const [text, setText] = useState(raw);
  const [err, setErr] = useState<string | null>(null);
  return (
    <>
      {err && <div className="banner err">{err}</div>}
      <textarea className="json" value={text} spellCheck={false} onChange={(e) => setText(e.target.value)} />
      <div className="row"><button className="btn primary" onClick={() => { const r = parseConfig(text); r.ok ? onFixed(r.config) : setErr(r.error); }}>Apply JSON</button></div>
    </>
  );
}
