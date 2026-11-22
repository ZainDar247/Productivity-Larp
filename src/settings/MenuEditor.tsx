import { useMemo, useState } from "react";
import { ArrowDown, ArrowUp, ChevronDown, ChevronRight, Copy, CornerLeftUp, CornerRightDown, Folder, FolderPlus, Plus, Trash2 } from "lucide-react";
import { clamp, fitCount } from "../lib/geometry";
import { buildItems } from "../lib/menuState";
import { addNode, duplicateNode, findNode, indentNode, locate, mapNode, moveNode, outdentNode, removeNode, uniqueId } from "../lib/treeOps";
import type { Config, NodeT, Theme } from "../lib/types";
import { Ring } from "../ui/Ring";
import { iconByName } from "../ui/Glyph";
import { defaultAction, NodeForm } from "./NodeForm";

interface Props {
  cfg: Config;
  theme: Theme;
  setMenu: (m: NodeT[]) => void;
}

function Row({ node, depth, sel, collapsed, onSel, onToggle }: {
  node: NodeT; depth: number; sel: string | null; collapsed: Set<string>; onSel: (id: string) => void; onToggle: (id: string) => void;
}) {
  const folder = Array.isArray(node.children);
  const open = !collapsed.has(node.id);
  const Icon = iconByName(node.icon) ?? (folder ? Folder : undefined);
  return (
    <>
      <div className={"tree-row" + (sel === node.id ? " on" : "")} style={{ paddingLeft: 8 + depth * 16 }} onClick={() => onSel(node.id)}>
        <span className="twisty" onClick={(e) => { e.stopPropagation(); folder && onToggle(node.id); }}>
          {folder ? (open ? <ChevronDown size={14} /> : <ChevronRight size={14} />) : null}
        </span>
        <span className="tree-icon">{Icon ? <Icon size={15} /> : node.icon ?? ""}</span>
        <span className="tree-label">{node.label ?? node.id}</span>
        {folder && <span className="badge">{node.children!.length}</span>}
      </div>
      {folder && open && node.children!.map((c) => (
        <Row key={c.id} node={c} depth={depth + 1} sel={sel} collapsed={collapsed} onSel={onSel} onToggle={onToggle} />
      ))}
    </>
  );
}

export function MenuEditor({ cfg, theme, setMenu }: Props) {
  const [sel, setSel] = useState<string | null>(cfg.menu[0]?.id ?? null);
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const menu = cfg.menu;
  const node = sel ? findNode(menu, sel) : null;
  const loc = sel ? locate(menu, sel) : null;

  const toggle = (id: string) => setCollapsed((s) => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; });

  const add = (folder: boolean) => {
    const base = folder ? "New folder" : "New item";
    const n: NodeT = {
      id: uniqueId(menu, base), label: base,
      ...(folder ? { children: [] } : { action: defaultAction("open") }),
    };
    // into the selected folder, else next to the selected item, else at the root
    const parent = node && Array.isArray(node.children) ? node.id : loc?.parent ?? null;
    setMenu(addNode(menu, parent, n));
    if (parent) setCollapsed((s) => { const c = new Set(s); c.delete(parent); return c; });
    setSel(n.id);
  };
  const remove = () => {
    if (!sel) return;
    const next = removeNode(menu, sel);
    setMenu(next);
    setSel(next[0]?.id ?? null);
  };
  const dup = () => {
    if (!sel) return;
    const r = duplicateNode(menu, sel);
    setMenu(r.menu);
    setSel(r.newId);
  };

  // preview: a folder shows its own contents, anything else shows the level it lives in
  const preview = useMemo(() => {
    const isFolder = node && Array.isArray(node.children);
    let level: NodeT[] = menu;
    if (isFolder) level = node!.children!;
    else if (loc?.parent) level = findNode(menu, loc.parent)?.children ?? menu;
    const inner = Math.min(theme.innerRadius, 120);
    const outer = inner + clamp(theme.thickness, 60, 220);
    const { items } = buildItems(level, !!isFolder || !!loc?.parent, 0, fitCount(inner, outer));
    const hl = !isFolder && sel ? items.findIndex((i) => i.kind === "node" && i.node.id === sel) : -1;
    return { items, inner, outer, hl: hl >= 0 ? hl : null };
  }, [menu, node, loc?.parent, sel, theme.innerRadius, theme.thickness]);

  const pad = preview.outer + 10;
  const tb = (title: string, onClick: () => void, icon: React.ReactNode, disabled = false) => (
    <button type="button" className="icon-btn" title={title} aria-label={title} onClick={onClick} disabled={disabled}>{icon}</button>
  );

  return (
    <div className="menu-editor">
      <section className="pane tree">
        <div className="toolbar">
          <button type="button" className="btn primary" onClick={() => add(false)}><Plus size={14} /> Item</button>
          <button type="button" className="btn" onClick={() => add(true)}><FolderPlus size={14} /> Folder</button>
          <span className="spacer" />
          {tb("Move up", () => sel && setMenu(moveNode(menu, sel, -1)), <ArrowUp size={15} />, !sel)}
          {tb("Move down", () => sel && setMenu(moveNode(menu, sel, 1)), <ArrowDown size={15} />, !sel)}
          {tb("Move out of folder", () => sel && setMenu(outdentNode(menu, sel)), <CornerLeftUp size={15} />, !loc?.parent)}
          {tb("Move into the folder above", () => sel && setMenu(indentNode(menu, sel)), <CornerRightDown size={15} />, !sel)}
          {tb("Duplicate", dup, <Copy size={15} />, !sel)}
          {tb("Delete", remove, <Trash2 size={15} />, !sel)}
        </div>
        <div className="tree-list" role="tree">
          {menu.map((n) => <Row key={n.id} node={n} depth={0} sel={sel} collapsed={collapsed} onSel={setSel} onToggle={toggle} />)}
          {menu.length === 0 && <p className="muted pad">The menu is empty. Add your first item.</p>}
        </div>
      </section>

      <section className="pane details">
        {node ? <NodeForm key={node.id} node={node} onChange={(n) => setMenu(mapNode(menu, node.id, () => n))} />
              : <p className="muted pad">Select an item to edit it.</p>}
      </section>

      <section className="pane preview">
        <h4>Live preview</h4>
        <svg viewBox={`${-pad} ${-pad} ${pad * 2} ${pad * 2}`} className="preview-svg">
          <Ring items={preview.items} highlight={preview.hl} innerR={preview.inner} outerR={preview.outer} theme={theme}
            title={node?.label ?? ""} subtitle={undefined}
            onPick={(i) => { const it = preview.items[i]; if (it?.kind === "node") setSel(it.node.id); }} />
        </svg>
        <p className="muted small">Click a slice to edit it. A folder previews its own contents.</p>
      </section>
    </div>
  );
}
