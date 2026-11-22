import { polar, sliceAngle as sectorAngle, sectorPath } from "../lib/geometry";
import type { Item } from "../lib/menuState";
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
  /** Re-mount key so layer changes replay the pop-in animation. */
  animKey?: string;
}

/** Presentational ring, drawn around (0,0). Used by the overlay and the settings preview. */
export function Ring({ items, highlight, innerR, outerR, theme, title, subtitle, onHover, onPick, animKey }: Props) {
  const n = items.length;
  const mid = (innerR + outerR) / 2;
  const arc = n > 0 ? (2 * Math.PI * mid) / n : mid * 6;
  const avail = Math.min(outerR - innerR - 14, arc - theme.gap - 10);

  return (
    <g key={animKey} className={theme.animation ? "ring pop" : "ring"}>
      {items.map((it, i) => {
        const hl = highlight === i;
        const base = it.kind === "node" && it.node.color ? it.node.color : theme.sliceColor;
        const fill = hl ? theme.accent : base;
        const p = polar(mid, sectorAngle(i, n));
        return (
          <g key={i} data-slice={i} data-hl={hl ? "1" : undefined}>
            <path
              d={sectorPath(innerR, outerR, i, n, theme.gap)}
              fill={fill}
              fillRule="evenodd"
              stroke={hl ? theme.accent : "rgba(255,255,255,0.16)"}
              strokeWidth={1}
              className={theme.animation ? "slice" : undefined}
              onPointerEnter={onHover && (() => onHover(i))}
              onPointerLeave={onHover && (() => onHover(null))}
              onClick={onPick && (() => onPick(i))}
              style={{ cursor: onPick ? "pointer" : undefined }}
            />
            <g transform={`translate(${p.x.toFixed(1)} ${p.y.toFixed(1)})`}>
              <Glyph item={it} avail={avail} color={hl ? onColor(theme.accent) : theme.textColor} fontSize={theme.fontSize} />
            </g>
          </g>
        );
      })}
      <circle r={innerR - 6} fill={theme.sliceColor} stroke={theme.accent} strokeWidth={2} opacity={0.96} />
      <text textAnchor="middle" y={subtitle ? -2 : 5} fontSize={theme.fontSize + 3} fontWeight={600} fill={theme.textColor} pointerEvents="none">
        {title}
      </text>
      {subtitle && (
        <text textAnchor="middle" y={theme.fontSize + 6} fontSize={Math.max(9, theme.fontSize - 3)} fill={theme.textColor} opacity={0.55} pointerEvents="none">
          {subtitle}
        </text>
      )}
    </g>
  );
}
