import { icons, type LucideIcon } from "lucide-react";
import type { Item } from "../lib/menuState";
import { itemLabel } from "../lib/menuState";
import { clamp } from "../lib/geometry";
import type { DisplayMode } from "../lib/types";
import { useImage } from "./useImage";

export const iconByName = (name?: string): LucideIcon | undefined =>
  name ? (icons as unknown as Record<string, LucideIcon>)[name] : undefined;

/** A short non-alphanumeric string (an emoji) is drawn as text; an unknown icon *name* is ignored. */
export const isEmoji = (v?: string) => !!v && [...v].length <= 4 && !/^[A-Za-z0-9_-]+$/.test(v);

export function wrapLabel(label: string, maxChars: number, maxLines = 2): string[] {
  const words = label.split(/\s+/).filter(Boolean);
  const lines: string[] = [];
  let cur = "";
  for (const w of words) {
    if (!cur) cur = w;
    else if ((cur + " " + w).length <= maxChars) cur += " " + w;
    else (lines.push(cur), (cur = w));
  }
  if (cur) lines.push(cur);
  const out = lines.slice(0, maxLines);
  if (lines.length > maxLines) out[maxLines - 1] += "…";
  return out.map((l) => (l.length > maxChars ? l.slice(0, Math.max(1, maxChars - 1)) + "…" : l));
}

interface Props {
  item: Item;
  /** Space available for the content (px). */
  avail: number;
  color: string;
  fontSize: number;
}

/** Icon / emoji / image / text for one slice, centred on (0,0). */
export function Glyph({ item, avail, color, fontSize }: Props) {
  let icon: string | undefined, image: string | undefined, display: DisplayMode;
  if (item.kind === "node") {
    icon = item.node.icon;
    image = item.node.image;
    display = item.node.display ?? (image ? "image+text" : icon ? "icon+text" : "text");
  } else {
    icon = item.kind === "back" ? "Undo2" : "Ellipsis";
    display = "icon+text";
  }
  const imgUrl = useImage(image);
  const Icon = iconByName(icon);
  if (!Icon && !isEmoji(icon)) icon = undefined; // unknown icon name → fall back to text
  const wantsIcon = display === "icon" || display === "icon+text";
  const wantsImg = display === "image" || display === "image+text";
  const showImage = wantsImg && !!imgUrl;
  const showIcon = !showImage && wantsIcon && !!icon;
  const hasVisual = showImage || showIcon;
  // never show an empty slice: fall back to text when the requested visual is missing
  let showText = display.includes("text") || !hasVisual;
  if (hasVisual && avail < 54 && display.includes("text")) showText = false; // crowded ring → visual only

  const label = itemLabel(item);
  const maxChars = Math.max(4, Math.floor(avail / (fontSize * 0.56)));
  const lines = showText ? wrapLabel(label, maxChars, hasVisual ? 2 : 3) : [];
  const lh = fontSize * 1.2;
  const vis = clamp(avail * (showText ? 0.4 : 0.55), 18, showText ? 34 : 54);
  const gap = hasVisual && lines.length ? 4 : 0;
  const total = (hasVisual ? vis : 0) + gap + lines.length * lh;
  let y = -total / 2;

  return (
    <g pointerEvents="none" fill={color} style={{ color }}>
      {hasVisual && (
        <>
          {showImage && (
            <image href={imgUrl} x={-vis / 2} y={y} width={vis} height={vis} preserveAspectRatio="xMidYMid meet" />
          )}
          {showIcon && Icon && <Icon x={-vis / 2} y={y} size={vis} strokeWidth={1.75} color={color} fill="none" />}
          {showIcon && !Icon && (
            <text x={0} y={y + vis * 0.82} textAnchor="middle" fontSize={vis * 0.85}>
              {icon}
            </text>
          )}
        </>
      )}
      {lines.map((l, i) => (
        <text
          key={i}
          x={0}
          y={y + (hasVisual ? vis + gap : 0) + lh * i + fontSize}
          textAnchor="middle"
          fontSize={fontSize}
          fontWeight={500}
        >
          {l}
        </text>
      ))}
    </g>
  );
}
