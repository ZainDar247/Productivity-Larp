import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { icons } from "lucide-react";
import { bridge } from "../lib/bridge";
import { iconByName, isEmoji } from "../ui/Glyph";
import { useImage } from "../ui/useImage";

export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="field">
      <span className="field-label">{label}</span>
      {children}
      {hint && <span className="field-hint">{hint}</span>}
    </label>
  );
}

export function PathInput({
  value, onChange, extensions, placeholder, title,
}: { value: string; onChange: (v: string) => void; extensions?: string[]; placeholder?: string; title?: string }) {
  return (
    <span className="row">
      <input value={value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} spellCheck={false} />
      <button type="button" className="btn" onClick={async () => {
        const p = await bridge.pickFile({ extensions, title });
        if (p) onChange(p);
      }}>Browse…</button>
    </span>
  );
}

const ALL_ICONS = Object.keys(icons).sort();

export function IconPicker({ value, onChange }: { value?: string; onChange: (v: string | undefined) => void }) {
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState("");
  const matches = useMemo(() => {
    const s = q.trim().toLowerCase();
    return (s ? ALL_ICONS.filter((n) => n.toLowerCase().includes(s)) : ALL_ICONS).slice(0, 72);
  }, [q]);
  const Cur = iconByName(value);
  return (
    <div className="icon-picker">
      <span className="row">
        <span className="icon-preview" aria-hidden>{Cur ? <Cur size={20} /> : value || "·"}</span>
        <input value={value ?? ""} placeholder="icon name (e.g. Server) or an emoji 🙂" spellCheck={false}
          onChange={(e) => onChange(e.target.value || undefined)} />
        <button type="button" className="btn" onClick={() => setOpen((o) => !o)}>{open ? "Close" : "Browse"}</button>
        {value && <button type="button" className="btn ghost" onClick={() => onChange(undefined)}>Clear</button>}
      </span>
      {value && !Cur && !isEmoji(value) && (
        <span className="field-hint warn">“{value}” is not a known icon name, so the slice falls back to text. Use Browse to pick one.</span>
      )}
      {open && (
        <div className="icon-grid-wrap">
          <input autoFocus placeholder={`Search ${ALL_ICONS.length} icons…`} value={q} onChange={(e) => setQ(e.target.value)} />
          <div className="icon-grid">
            {matches.map((n) => {
              const I = iconByName(n)!;
              return (
                <button type="button" key={n} title={n} className={"icon-cell" + (n === value ? " on" : "")}
                  onClick={() => { onChange(n); setOpen(false); }}>
                  <I size={20} />
                </button>
              );
            })}
            {matches.length === 0 && <span className="muted">No icon matches “{q}”</span>}
          </div>
        </div>
      )}
    </div>
  );
}

/** A file on disk outside the config folder's images/ (not a URL, data: URL or already-imported relative path). */
const isExternalFile = (v?: string): v is string => !!v && !/^(https?:|data:|images[\\/])/i.test(v.trim()) && /^([a-z]:[\\/]|\\\\|\/)/i.test(v.trim());

export function ImageInput({ value, onChange }: { value?: string; onChange: (v: string | undefined) => void }) {
  const url = useImage(value);
  const [draft, setDraft] = useState<string | null>(null); // text being typed, not yet imported
  const [err, setErr] = useState<string | null>(null);

  // Copy a local image into the config folder and point the config at the copy.
  const adopt = async (v: string | undefined) => {
    setDraft(null);
    if (!isExternalFile(v)) { setErr(null); onChange(v || undefined); return; }
    try { onChange(await bridge.importImage(v.trim())); setErr(null); }
    catch (e) { setErr((e as Error).message); onChange(v); }
  };

  // images chosen before this existed still point at the original file: copy them in the first time they're shown
  const adoptRef = useRef(adopt); adoptRef.current = adopt;
  useEffect(() => { if (bridge.native && isExternalFile(value)) void adoptRef.current(value); }, [value]);

  return (
    <span className="row">
      {value && <span className="img-preview">{url ? <img src={url} alt="" /> : "?"}</span>}
      <input value={draft ?? value ?? ""} placeholder="pick an image (it is copied into the config folder), or a URL" spellCheck={false}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={() => { if (draft !== null) void adopt(draft); }}
        onKeyDown={(e) => { if (e.key === "Enter" && draft !== null) void adopt(draft); }} />
      <button type="button" className="btn" onClick={async () => {
        const p = await bridge.pickFile({ extensions: ["png", "jpg", "jpeg", "gif", "webp", "svg", "ico", "bmp"], title: "Choose an image" });
        if (p) await adopt(p);
      }}>Browse…</button>
      {err && <span className="field-hint warn">{err}</span>}
    </span>
  );
}

export function ColorInput({ value, fallback, onChange }: { value?: string; fallback: string; onChange: (v: string | undefined) => void }) {
  return (
    <span className="row">
      <input type="color" value={value ?? fallback} onChange={(e) => onChange(e.target.value)} />
      <input value={value ?? ""} placeholder="default" onChange={(e) => onChange(e.target.value || undefined)} spellCheck={false} />
      {value && <button type="button" className="btn ghost" onClick={() => onChange(undefined)}>Reset</button>}
    </span>
  );
}

export function Slider({ value, min, max, step = 1, onChange, unit = "" }: { value: number; min: number; max: number; step?: number; unit?: string; onChange: (v: number) => void }) {
  return (
    <span className="row">
      <input type="range" min={min} max={max} step={step} value={value} onChange={(e) => onChange(Number(e.target.value))} />
      <span className="num">{value}{unit}</span>
    </span>
  );
}
