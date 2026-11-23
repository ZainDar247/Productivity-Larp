import { useEffect, useMemo, useRef, useState } from "react";
import { fitCount, sliceAt } from "../lib/geometry";
import { activate, buildItems, childrenAt, initialState, isFolder, itemLabel, step, type MenuState } from "../lib/menuState";
import { findNode } from "../lib/treeOps";
import { resolveBehavior, resolveTheme, type Config } from "../lib/types";
import { Ring } from "../ui/Ring";

/**
 * The real ring, at its real size, with the real behaviour: hover highlights, dwell/click activation, folders open,
 * Back / More work. Actions are not executed — a leaf just reports what it would have done.
 */
export function RingPreview({ cfg }: { cfg: Config }) {
  const theme = resolveTheme(cfg);
  const behavior = resolveBehavior(cfg);
  const [st, setSt] = useState<MenuState>(initialState);
  const [dwellIdx, setDwellIdx] = useState<number | null>(null);
  const [fired, setFired] = useState<string | null>(null);
  const svg = useRef<SVGSVGElement>(null);
  const timer = useRef<number | undefined>(undefined);
  const firedTimer = useRef<number | undefined>(undefined);

  const { innerR, outerR } = { innerR: theme.innerRadius, outerR: theme.innerRadius + theme.thickness };
  const level = childrenAt(cfg.menu, st.path) ?? cfg.menu;
  const built = useMemo(
    () => buildItems(level, st.path.length > 0, st.page, fitCount(innerR, outerR)),
    [level, st.path, st.page, innerR, outerR],
  );
  const { items, pages, page } = built;

  const latest = useRef({ st, items, innerR, behavior });
  latest.current = { st, items, innerR, behavior };

  const cancel = () => {
    window.clearTimeout(timer.current);
    timer.current = undefined;
    setDwellIdx(null);
  };
  useEffect(() => () => { window.clearTimeout(timer.current); window.clearTimeout(firedTimer.current); }, []);
  // a config edit that removes the folder we are in → back to the root
  useEffect(() => {
    if (st.path.length && !childrenAt(cfg.menu, st.path)) setSt(initialState());
  }, [cfg.menu, st.path]);
  // a theme change must not remount the ring: that replays the open animation on every slider tick and flickers

  const choose = (idx: number) => {
    const L = latest.current;
    const item = L.items[idx];
    if (!item) return;
    cancel();
    const [next, eff] = activate(L.st, item);
    setSt(next);
    if (eff.type === "run") {
      const label = item.kind === "node" ? (item.node.label ?? item.node.id) : "";
      setFired(label);
      window.clearTimeout(firedTimer.current);
      firedTimer.current = window.setTimeout(() => setFired(null), 1400);
    }
  };

  const local = (e: React.PointerEvent) => {
    const el = svg.current;
    const m = el?.getScreenCTM();
    if (!el || !m) return null;
    const p = new DOMPoint(e.clientX, e.clientY).matrixTransform(m.inverse());
    return { dx: p.x, dy: p.y };
  };

  const onMove = (e: React.PointerEvent) => {
    const pt = local(e);
    if (!pt) return;
    const L = latest.current;
    if (Math.hypot(pt.dx, pt.dy) < L.innerR) {
      cancel();
      setSt((s) => (s.armed && s.highlight === null ? s : { ...s, armed: true, highlight: null }));
      return;
    }
    if (!L.st.armed) return;
    const idx = sliceAt(pt.dx, pt.dy, L.items.length, L.innerR);
    if (idx === L.st.highlight) return;
    cancel();
    setSt((s) => ({ ...s, highlight: idx }));
    if (idx === null || L.behavior.activation === "click") return;
    const ms = L.behavior.dwellMs;
    if (ms <= 0) choose(idx);
    else {
      setDwellIdx(idx);
      timer.current = window.setTimeout(() => choose(idx), ms);
    }
  };
  const onDown = (e: React.PointerEvent) => {
    const pt = local(e);
    if (!pt) return;
    const L = latest.current;
    if (Math.hypot(pt.dx, pt.dy) < L.innerR) { cancel(); setSt(initialState()); return; } // centre: start over (the real one closes)
    const idx = sliceAt(pt.dx, pt.dy, L.items.length, L.innerR);
    if (idx !== null) choose(idx);
  };
  const onLeave = () => { cancel(); setSt((s) => ({ ...s, highlight: null })); };

  const hl = st.highlight !== null ? items[st.highlight] : undefined;
  const crumbs = st.path.map((id) => findNode(cfg.menu, id)?.label ?? id);
  const folderNode = st.path.length ? findNode(cfg.menu, st.path[st.path.length - 1]) : null;
  let title = hl ? itemLabel(hl) : (folderNode?.label ?? "");
  let sub: string | undefined = hl ? undefined : crumbs.length ? crumbs.join(" › ") : "Esc to close";
  if (hl?.kind === "node" && !isFolder(hl.node) && !hl.node.action) sub = "no action set";
  if (fired) (title = "▶ " + fired.slice(0, 18)), (sub = "would run");
  if (!hl && !fired && pages > 1) sub = `page ${page + 1}/${pages}`;
  if (sub && sub.length > 18) sub = "…" + sub.slice(-17);

  const pad = outerR + 14;
  const keyNav = (e: React.KeyboardEvent) => {
    const n = items.length;
    if (e.key === "ArrowRight" || e.key === "ArrowDown") { e.preventDefault(); cancel(); setSt((s) => ({ ...s, highlight: step(s.highlight, n, 1), armed: true })); }
    else if (e.key === "ArrowLeft" || e.key === "ArrowUp") { e.preventDefault(); cancel(); setSt((s) => ({ ...s, highlight: step(s.highlight, n, -1), armed: true })); }
    else if ((e.key === "Enter" || e.key === " ") && st.highlight !== null) { e.preventDefault(); choose(st.highlight); }
  };

  return (
    <svg
      ref={svg}
      viewBox={`${-pad} ${-pad} ${pad * 2} ${pad * 2}`}
      className="preview-svg"
      width={pad * 2}
      height={pad * 2}
      tabIndex={0}
      role="img"
      aria-label="Live preview of the radial menu"
      onPointerMove={onMove}
      onPointerDown={onDown}
      onPointerLeave={onLeave}
      onKeyDown={keyNav}
    >
      <Ring
        key={`${st.path.join("/")}:${page}`}
        items={items}
        highlight={st.highlight}
        innerR={innerR}
        outerR={outerR}
        theme={theme}
        title={title}
        subtitle={sub}
        dwellMs={behavior.dwellMs}
        dwelling={dwellIdx !== null && dwellIdx === st.highlight}
      />
    </svg>
  );
}
