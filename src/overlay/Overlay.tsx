import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { bridge, type OpenPayload } from "../lib/bridge";
import { clamp, fitCount, sliceAt } from "../lib/geometry";
import { activate, buildItems, childrenAt, initialState, isFolder, itemLabel, step, type MenuState } from "../lib/menuState";
import { findNode } from "../lib/treeOps";
import { parseConfig, resolveBehavior, resolveTheme, type Config } from "../lib/types";
import { Ring } from "../ui/Ring";

const EDGE = 12;
const EMPTY: Config = { version: 1, trigger: { type: "keyboard", shortcut: "" }, menu: [] };

export function Overlay() {
  const [cfg, setCfg] = useState<Config | null>(null);
  const [cfgError, setCfgError] = useState<string | null>(null);
  const [visible, setVisible] = useState(!bridge.native); // native window is hidden by the OS until a trigger fires
  const [geom, setGeom] = useState<OpenPayload>({ x: innerWidth / 2, y: innerHeight / 2, w: innerWidth, h: innerHeight });
  const [st, setSt] = useState<MenuState>(initialState);
  const [openKey, setOpenKey] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [dwellIdx, setDwellIdx] = useState<number | null>(null); // slice whose dwell timer is running (drives the sweep fill)

  // ---- config ----
  const load = useCallback(async () => {
    const p = await bridge.getConfig();
    const r = parseConfig(p.raw);
    if (r.ok) {
      setCfg(r.config);
      setCfgError(p.error);
    } else {
      setCfgError(r.error);
    }
  }, []);

  // ---- layout derived from config + viewport ----
  const layout = useMemo(() => {
    if (!cfg) return null;
    const theme = resolveTheme(cfg);
    const behavior = resolveBehavior(cfg);
    const maxOuter = Math.max(120, Math.min(geom.w, geom.h) / 2 - EDGE);
    const innerR = Math.min(theme.innerRadius, maxOuter - 60);
    const outerR = Math.min(innerR + theme.thickness, maxOuter);
    const level = childrenAt(cfg.menu, st.path) ?? cfg.menu;
    const built = buildItems(level, st.path.length > 0, st.page, fitCount(innerR, outerR));
    return { theme, behavior, innerR, outerR, ...built };
  }, [cfg, geom.w, geom.h, st.path, st.page]);

  // ---- latest values for the long-lived window listeners ----
  const latest = useRef({ st, layout, geom, cfg, visible });
  latest.current = { st, layout, geom, cfg, visible };
  const dwell = useRef<number | undefined>(undefined);
  const openedAt = useRef(0);
  const cancelDwell = () => {
    window.clearTimeout(dwell.current);
    dwell.current = undefined;
    setDwellIdx(null);
  };

  const close = useCallback(() => {
    cancelDwell();
    bridge.closeOverlay();
  }, []);

  const choose = useCallback((idx: number) => {
    const L = latest.current;
    if (!L.layout) return;
    const item = L.layout.items[idx];
    if (!item) return;
    cancelDwell();
    const [next, eff] = activate(L.st, item);
    setSt(next);
    setError(null);
    if (eff.type === "recenter") {
      if (L.layout.behavior.warpCursor) {
        // Selection stays disarmed until a pointer event is actually seen inside the
        // centre — the warp itself produces that event. We never *assume* it landed.
        bridge.warpCursor(L.geom.x, L.geom.y).catch(() => {});
      }
    } else if (eff.type === "run") {
      bridge.runNode(eff.id).catch((e) => setError(String(e)));
    }
  }, []);

  // ---- open / close / config events ----
  useEffect(() => {
    load();
    const offs: Promise<() => void>[] = [
      bridge.on("config:changed", () => load()),
      bridge.on<OpenPayload>("menu:open", (p) => {
        const L = latest.current;
        const th = resolveTheme(L.cfg ?? EMPTY);
        const maxOuter = Math.max(120, Math.min(p.w, p.h) / 2 - EDGE);
        const outer = Math.min(Math.min(th.innerRadius, maxOuter - 60) + th.thickness, maxOuter);
        const m = outer + EDGE;
        const cx = p.w <= 2 * m ? p.w / 2 : clamp(p.x, m, p.w - m);
        const cy = p.h <= 2 * m ? p.h / 2 : clamp(p.y, m, p.h - m);
        // keep the pointer in the middle of the ring even when the cursor was near a screen edge
        if ((cx !== p.x || cy !== p.y) && resolveBehavior(L.cfg ?? EMPTY).warpCursor) bridge.warpCursor(cx, cy);
        cancelDwell();
        openedAt.current = Date.now();
        setGeom({ x: cx, y: cy, w: p.w, h: p.h });
        setSt(initialState());
        setError(null);
        setOpenKey((k) => k + 1);
        setVisible(true);
        load();
      }),
      bridge.on("menu:close", () => {
        cancelDwell();
        setVisible(false);
        setSt(initialState());
        setError(null);
      }),
    ];
    return () => offs.forEach((o) => o.then((f) => f()));
  }, [load]);

  // stale path after a config edit → go back to the root
  useEffect(() => {
    if (cfg && st.path.length && !childrenAt(cfg.menu, st.path)) setSt(initialState());
  }, [cfg, st.path]);

  // Tell the shell when the first frame is really on screen, so the window is revealed only once there is
  // something correct to show — no flash of an empty transparent window.
  useEffect(() => {
    if (!visible || openKey === 0 || !bridge.native) return;
    let cancelled = false;
    requestAnimationFrame(() => requestAnimationFrame(() => {
      if (!cancelled) bridge.overlayReady().catch(() => {});
    }));
    return () => { cancelled = true; };
  }, [openKey]); // eslint-disable-line react-hooks/exhaustive-deps

  // ---- pointer + keyboard ----
  useEffect(() => {
    const rel = (e: PointerEvent) => {
      const { geom: g } = latest.current;
      return { dx: e.clientX - g.x, dy: e.clientY - g.y };
    };
    const onMove = (e: PointerEvent) => {
      const L = latest.current;
      if (!L.layout || !L.visible) return;
      const { dx, dy } = rel(e);
      if (Math.hypot(dx, dy) < L.layout.innerR) {
        cancelDwell();
        setSt((s) => (s.armed && s.highlight === null ? s : { ...s, armed: true, highlight: null }));
        return;
      }
      if (!L.st.armed) return;
      const idx = sliceAt(dx, dy, L.layout.items.length, L.layout.innerR);
      if (idx === L.st.highlight) return;
      cancelDwell();
      setError(null);
      setSt((s) => ({ ...s, highlight: idx }));
      if (idx === null || L.layout.behavior.activation === "click") return; // click mode: hover only highlights
      const ms = L.layout.behavior.dwellMs;
      if (ms <= 0) choose(idx);
      else {
        setDwellIdx(idx);
        dwell.current = window.setTimeout(() => choose(idx), ms);
      }
    };
    const onDown = (e: PointerEvent) => {
      const L = latest.current;
      if (e.button !== 0 || !L.layout || !L.visible) return;
      const { dx, dy } = rel(e);
      if (Math.hypot(dx, dy) < L.layout.innerR) return close(); // click inside the centre closes
      const idx = sliceAt(dx, dy, L.layout.items.length, L.layout.innerR);
      if (idx !== null) choose(idx); // a click selects immediately, no need to wait for the dwell
    };
    const onKey = (e: KeyboardEvent) => {
      const L = latest.current;
      if (!L.layout || !L.visible) return;
      const n = L.layout.items.length;
      const move = (dir: 1 | -1) => {
        e.preventDefault();
        cancelDwell();
        setError(null);
        setSt((s) => ({ ...s, highlight: step(s.highlight, n, dir), armed: true }));
      };
      switch (e.key) {
        case "Escape": e.preventDefault(); return close();
        case "ArrowRight": case "ArrowDown": return move(1);
        case "ArrowLeft": case "ArrowUp": return move(-1);
        case "Tab": return move(e.shiftKey ? -1 : 1);
        case "Enter": case " ":
          e.preventDefault();
          if (L.st.highlight !== null) choose(L.st.highlight);
          return;
        case "Backspace": {
          e.preventDefault();
          const i = L.layout.items.findIndex((it) => it.kind === "back");
          if (i >= 0) choose(i);
          return;
        }
      }
    };
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerdown", onDown);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerdown", onDown);
      window.removeEventListener("keydown", onKey);
    };
  }, [choose, close]);

  // ---- render ----
  if (!layout || !cfg) {
    return cfgError ? <div className="overlay-error">{cfgError}</div> : null;
  }
  if (!visible) return null;

  const { theme, innerR, outerR, items, pages, page } = layout;
  const hl = st.highlight !== null ? items[st.highlight] : undefined;
  const crumbs = st.path.map((id) => findNode(cfg.menu, id)?.label ?? id);
  const folderNode = st.path.length ? findNode(cfg.menu, st.path[st.path.length - 1]) : null;

  let title = hl ? itemLabel(hl) : (folderNode?.label ?? "");
  let sub = hl ? undefined : crumbs.length ? crumbs.join(" › ") : "Esc to close";
  if (hl?.kind === "node" && !isFolder(hl.node) && !hl.node.action) sub = "no action set";
  if (error) (title = "⚠ " + error.slice(0, 40)), (sub = undefined);
  if (!hl && pages > 1) sub = `page ${page + 1}/${pages}`;
  // the centre circle is small: keep only the tail of a long breadcrumb
  if (sub && sub.length > 18) sub = "…" + sub.slice(-17);

  return (
    <div className="overlay">
      <svg width={geom.w} height={geom.h} role="menu" aria-label="Radial menu">
        <g transform={`translate(${geom.x} ${geom.y})`}>
          <Ring
            key={`${openKey}:${st.path.join("/")}:${page}`}
            items={items}
            highlight={st.highlight}
            innerR={innerR}
            outerR={outerR}
            theme={theme}
            title={title}
            subtitle={sub}
            dwellMs={layout.behavior.dwellMs}
            dwelling={dwellIdx !== null && dwellIdx === st.highlight}
          />
        </g>
      </svg>
      {cfgError && <div className="overlay-warn">Config problem: {cfgError}</div>}
    </div>
  );
}
