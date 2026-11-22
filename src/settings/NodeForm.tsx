import { ACTION_TYPES, DISPLAY_MODES, type ActionT, type ActionType, type NodeT } from "../lib/types";
import { ColorInput, Field, IconPicker, ImageInput, PathInput } from "./widgets";

const DISPLAY_LABEL: Record<string, string> = {
  text: "Text only", icon: "Icon only", image: "Image only", "icon+text": "Icon + text", "image+text": "Image + text",
};

export function defaultAction(type: ActionType): ActionT {
  switch (type) {
    case "open": return { type, path: "" };
    case "rdp": return { type, file: "" };
    case "run": return { type, command: "" };
    case "url": return { type, url: "https://" };
    case "settings": return { type };
  }
}

function ActionEditor({ action, onChange }: { action: ActionT; onChange: (a: ActionT) => void }) {
  const set = (p: Partial<ActionT>) => onChange({ ...action, ...p } as ActionT);
  const meta = ACTION_TYPES.find((a) => a.type === action.type);
  return (
    <>
      <Field label="Action" hint={meta?.hint}>
        <select value={action.type} onChange={(e) => onChange(defaultAction(e.target.value as ActionType))}>
          {ACTION_TYPES.map((a) => <option key={a.type} value={a.type}>{a.label}</option>)}
        </select>
      </Field>
      {action.type === "rdp" && (
        <>
          <Field label="RDP file" hint="Use ${NAME} to insert a value from your .env, e.g. ${RDP_DIR}\admin\server1.rdp">
            <PathInput value={action.file} onChange={(file) => set({ file })} extensions={["rdp"]} title="Choose an .rdp file" />
          </Field>
          <Field label="Extra mstsc arguments" hint="One per line, e.g. /admin or /multimon">
            <textarea rows={2} value={(action.args ?? []).join("\n")} spellCheck={false}
              onChange={(e) => set({ args: e.target.value ? e.target.value.split("\n") : undefined })} />
          </Field>
        </>
      )}
      {action.type === "open" && (
        <Field label="Path" hint="File, folder, document or shortcut. ${NAME} reads from .env">
          <PathInput value={action.path} onChange={(path) => set({ path })} />
        </Field>
      )}
      {action.type === "url" && (
        <Field label="URL"><input value={action.url} onChange={(e) => set({ url: e.target.value })} spellCheck={false} /></Field>
      )}
      {action.type === "run" && (
        <>
          <Field label="Program"><PathInput value={action.command} onChange={(command) => set({ command })} extensions={["exe", "bat", "cmd", "lnk"]} /></Field>
          <Field label="Arguments" hint="One per line">
            <textarea rows={3} value={(action.args ?? []).join("\n")} spellCheck={false}
              onChange={(e) => set({ args: e.target.value ? e.target.value.split("\n") : undefined })} />
          </Field>
          <Field label="Working directory"><input value={action.cwd ?? ""} onChange={(e) => set({ cwd: e.target.value || undefined })} spellCheck={false} /></Field>
          <label className="check"><input type="checkbox" checked={!!action.hidden} onChange={(e) => set({ hidden: e.target.checked || undefined })} /> Hide the console window</label>
        </>
      )}
    </>
  );
}

export function NodeForm({ node, onChange }: { node: NodeT; onChange: (n: NodeT) => void }) {
  const isFolder = Array.isArray(node.children);
  const set = (p: Partial<NodeT>) => onChange({ ...node, ...p });
  const display = node.display ?? (node.image ? "image+text" : node.icon ? "icon+text" : "text");

  return (
    <div className="form">
      <Field label="Label"><input value={node.label ?? ""} onChange={(e) => set({ label: e.target.value || undefined })} /></Field>

      <Field label="What does this slice do?">
        <span className="seg">
          <button type="button" className={"seg-btn" + (!isFolder ? " on" : "")}
            onClick={() => isFolder && onChange({ ...node, children: undefined, action: defaultAction("open") })}>Run an action</button>
          <button type="button" className={"seg-btn" + (isFolder ? " on" : "")}
            onClick={() => !isFolder && onChange({ ...node, action: undefined, children: [] })}>Open a sub-menu</button>
        </span>
      </Field>

      {!isFolder && <ActionEditor action={node.action ?? defaultAction("open")} onChange={(action) => set({ action })} />}
      {isFolder && <p className="muted">Add items to this sub-menu with <b>+ Item</b> while it is selected. A <b>Back</b> slice is added automatically.</p>}

      <h4>Appearance</h4>
      <Field label="Show">
        <select value={display} onChange={(e) => set({ display: e.target.value as NodeT["display"] })}>
          {DISPLAY_MODES.map((m) => <option key={m} value={m}>{DISPLAY_LABEL[m]}</option>)}
        </select>
      </Field>
      <Field label="Icon"><IconPicker value={node.icon} onChange={(icon) => set({ icon })} /></Field>
      <Field label="Image"><ImageInput value={node.image} onChange={(image) => set({ image })} /></Field>
      <Field label="Slice colour"><ColorInput value={node.color} fallback="#1d2030" onChange={(color) => set({ color })} /></Field>
      <p className="muted small">id: <code>{node.id}</code></p>
    </div>
  );
}
