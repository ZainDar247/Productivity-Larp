import { useId } from "react";
import { polar, sliceAngle as sectorAngle, sectorPath } from "../lib/geometry";
import type { Item } from "../lib/menuState";
import { BACK_RED, BACK_RED2 } from "../lib/themePresets";
import type { Theme } from "../lib/types";
import { Glyph } from "./Glyph";

/** Readable text colour on top of a background colour. */
export function onColor(hex: string, fallbackDark = "#ffffff", fallbackLight = "#111318"): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return fallbackDark;
  const n = parseInt(m[1], 16);
  const lum = (0.299 * ((n >> 16) & 255) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255)) / 255;
  return lum > 0.6 ? fallbackLight : fallbackDark;
}

/** How far a hovered slice slides outward (px). */
const POP = 9;

interface Look {
  underlay?: string; // dark/light base under translucent (glass) slices
  fill: string;
  overlay?: { fill: string; opacity: number }; // gradient wash (gradient / glass styles)
  edge: string;
  edgeW: number;
  glow?: string; // neon outline glow
  ink: string;
}

/** Per-style look of one slice (ported from the PSW wheel's ring). */
function look(t: Theme, it: Item, hov: boolean, ids: { gloss: string; glassRim: string; gradSoft: string; gradBack: string }): Look {
  const isBack = it.kind === "back";
  const custom = it.kind === "node" && it.node.color ? it.node.color : undefined;
  const accent = isBack ? BACK_RED : t.accent;
  const sliceBack = `color-mix(in srgb, ${t.sliceColor} 78%, ${BACK_RED})`;
  const rest = custom ?? (isBack ? sliceBack : t.sliceColor);
  const body = hov ? (custom ? `color-mix(in srgb, ${custom} 72%, ${accent})` : isBack ? `color-mix(in srgb, ${t.sliceHover} 60%, ${BACK_RED})` : t.sliceHover) : rest;
  const ink = t.textColor;

  switch (t.style) {
    case "flat":
      return { fill: hov ? accent : rest, edge: "none", edgeW: 0, ink: hov ? "#ffffff" : ink };
    case "glass":
      return {
        underlay: t.isLight ? "rgba(255,255,255,.42)" : "rgba(10,14,28,.50)",
        fill: body,
        overlay: { fill: `url(#${ids.gloss})`, opacity: hov ? 1 : 0.8 },
        edge: hov ? accent : `url(#${ids.glassRim})`,
        edgeW: hov ? 2.2 : 1.4,
        ink,
      };
    case "neon":
      return {
        fill: body,
        edge: hov ? (isBack ? BACK_RED : t.accent2) : isBack ? BACK_RED : t.rim,
        edgeW: hov ? 2.8 : 1.8,
        glow: hov ? (isBack ? BACK_RED : t.accent2) : undefined,
        ink,
      };
    case "gradient":
      return {
        fill: rest,
        overlay: { fill: isBack ? `url(#${ids.gradBack})` : `url(#${ids.gradSoft})`, opacity: hov ? 0.95 : 0.34 },
        edge: hov ? accent : t.rim,
        edgeW: hov ? 2.2 : 1.2,
        ink: hov ? "#ffffff" : ink,
      };
    default: // solid
      return { fill: body, edge: hov ? accent : t.rim, edgeW: hov ? 2.6 : 1.6, ink };
  }
}

interface Props {
  items: Item[];
  highlight: number | null;
  innerR: number;
  outerR: number;
  theme: Theme;
  title?: string;
  subtitle?: string;
  onHover?: (i: number | null) => void;
  onPick?: (i: number) => void;
  /** Re-mount key so layer changes replay the open animation. */
  animKey?: string;
  /** While set, the highlighted slice shows a fill sweeping outward over `dwellMs` — the time left before it fires. */
  dwellMs?: number;
  dwelling?: boolean;
}

/** Presentational ring, drawn around (0,0). Used by the overlay and the settings preview. */
export function Ring({ items, highlight, innerR, outerR, theme, title, subtitle, onHover, onPick, animKey, dwellMs = 0, dwelling = false }: Props) {
  const uid = useId().replace(/[^a-zA-Z0-9]/g, "");
  const ids = { gloss: `gloss${uid}`, glassRim: `rim${uid}`, gradSoft: `gs${uid}`, gradBack: `gb${uid}`, clip: `dw${uid}`, hot: `hot${uid}` };
  const n = items.length;
  const mid = (innerR + outerR) / 2;
  const arc = n > 0 ? (2 * Math.PI * mid) / n : mid * 6;
  const avail = Math.min(outerR - innerR - 14, arc - theme.gap - 10);
  const anim = theme.animation;
  const popK = theme.style === "flat" ? 0.5 : 1;
  const hlItem = highlight !== null ? items[highlight] : undefined;
  const sweep = anim && dwelling && dwellMs > 0 && highlight !== null;

  return (
    <g key={animKey} className={anim ? "ring pop" : "ring"} style={{ filter: theme.style === "flat" ? "drop-shadow(0 2px 10px rgba(0,0,0,.30))" : theme.style === "glass" ? "drop-shadow(0 6px 18px rgba(0,0,0,.35))" : "drop-shadow(0 4px 16px rgba(0,0,0,.5))" }}>
      <defs>
        <linearGradient id={ids.gradSoft} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor={theme.accent} />
          <stop offset="1" stopColor={theme.accent2} />
        </linearGradient>
        <linearGradient id={ids.gradBack} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor={BACK_RED} />
          <stop offset="1" stopColor={BACK_RED2} />
        </linearGradient>
        <linearGradient id={ids.gloss} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#fff" stopOpacity={theme.isLight ? 0.7 : 0.3} />
          <stop offset="0.55" stopColor="#fff" stopOpacity="0.04" />
          <stop offset="1" stopColor="#fff" stopOpacity="0.1" />
        </linearGradient>
        <linearGradient id={ids.glassRim} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#fff" stopOpacity="0.75" />
          <stop offset="1" stopColor="#fff" stopOpacity="0.12" />
        </linearGradient>
        <clipPath id={ids.clip}>
          {/* `r` is animated by CSS (see .dwell-sweep) from the inner to the outer edge */}
          <circle
            key={`${highlight}`}
            r={innerR}
            className={sweep ? "dwell-sweep" : undefined}
            style={{ ["--r0" as string]: `${innerR}px`, ["--r1" as string]: `${outerR}px`, ["--dwell" as string]: `${dwellMs}ms` }}
          />
        </clipPath>
      </defs>

      {items.map((it, i) => {
        const hl = highlight === i;
        const lk = look(theme, it, hl, ids);
        const ang = sectorAngle(i, n);
        const p = polar(mid, ang);
        const d = sectorPath(innerR, outerR, i, n, theme.gap);
        const out = polar(hl ? POP * popK : 0, ang);
        const hot1 = it.kind === "back" ? BACK_RED : theme.accent;
        const hot2 = it.kind === "back" ? BACK_RED2 : theme.accent2;
        const g0 = polar(innerR, ang), g1 = polar(outerR, ang);
        return (
          <g
            key={i}
            data-slice={i}
            data-hl={hl ? "1" : undefined}
            className={anim ? "slice" : undefined}
            style={{ transform: `translate(${out.x.toFixed(2)}px, ${out.y.toFixed(2)}px)` }}
          >
            {lk.underlay && <path d={d} fill={lk.underlay} fillRule="evenodd" />}
            <path
              d={d}
              fill={lk.fill}
              fillRule="evenodd"
              onPointerEnter={onHover && (() => onHover(i))}
              onPointerLeave={onHover && (() => onHover(null))}
              onClick={onPick && (() => onPick(i))}
              style={{ cursor: onPick ? "pointer" : undefined }}
            />
            {lk.overlay && <path d={d} fill={lk.overlay.fill} opacity={lk.overlay.opacity} fillRule="evenodd" pointerEvents="none" />}
            {sweep && hl && (
              <>
                <linearGradient id={ids.hot} gradientUnits="userSpaceOnUse" x1={g0.x} y1={g0.y} x2={g1.x} y2={g1.y}>
                  <stop offset="0" stopColor={hot1} />
                  <stop offset="1" stopColor={hot2} />
                </linearGradient>
                <path d={d} fill={`url(#${ids.hot})`} fillRule="evenodd" clipPath={`url(#${ids.clip})`} opacity={0.9} pointerEvents="none" />
              </>
            )}
            {lk.edgeW > 0 && (
              <path
                d={d}
                fill="none"
                stroke={lk.edge}
                strokeWidth={lk.edgeW}
                fillRule="evenodd"
                pointerEvents="none"
                style={lk.glow ? { filter: `drop-shadow(0 0 5px ${lk.glow}) drop-shadow(0 0 10px ${lk.glow})` } : undefined}
              />
            )}
            <g transform={`translate(${p.x.toFixed(1)} ${p.y.toFixed(1)})`}>
              <Glyph item={it} avail={avail} color={sweep && hl ? "#ffffff" : lk.ink} fontSize={theme.fontSize} />
            </g>
          </g>
        );
      })}

      <circle r={innerR - 6} fill={theme.center} stroke={theme.rim} strokeWidth={1} />
      <circle
        className="centre-glow"
        r={innerR - 7}
        fill="none"
        strokeWidth={2}
        stroke={hlItem?.kind === "back" ? BACK_RED : theme.accent}
        style={{ opacity: hlItem && theme.style !== "flat" ? 0.9 : 0, filter: theme.style === "neon" ? `drop-shadow(0 0 6px ${theme.accent})` : undefined }}
      />
      <text textAnchor="middle" y={subtitle ? -2 : 5} fontSize={theme.fontSize + 3} fontWeight={600} fill={hlItem ? theme.textColor : theme.subText} pointerEvents="none">
        {title}
      </text>
      {subtitle && (
        <text textAnchor="middle" y={theme.fontSize + 6} fontSize={Math.max(9, theme.fontSize - 3)} fill={theme.subText} opacity={0.75} pointerEvents="none">
          {subtitle}
        </text>
      )}
    </g>
  );
}
