/** Ring themes, ported from the PSW wheel. Colours are CSS strings; `style` picks how a slice is drawn. */

export type RingStyle = "solid" | "glass" | "neon" | "gradient" | "flat";

export interface Palette {
  id: string;
  label: string;
  style: RingStyle;
  /** The ring is light (dark text on pale slices). */
  isLight: boolean;
  slice: string;
  sliceHover: string;
  rim: string;
  accent: string;
  accent2: string;
  center: string;
  text: string;
  subText: string;
}

const c = (r: number, g: number, b: number, a = 255) =>
  a === 255 ? `rgb(${r} ${g} ${b})` : `rgb(${r} ${g} ${b} / ${(a / 255).toFixed(3)})`;

/** The Back slice is tinted red in every theme so it is easy to spot. */
export const BACK_RED = "#e24646";
export const BACK_RED2 = "#b82f4a";

export const PRESETS: Palette[] = [
  {
    id: "dark", label: "Dark", style: "solid", isLight: false,
    slice: c(26, 29, 41, 242), sliceHover: c(40, 47, 70, 244), rim: c(255, 255, 255, 48),
    accent: c(79, 140, 255), accent2: c(123, 97, 255), center: c(17, 19, 28, 244), text: c(255, 255, 255), subText: c(163, 171, 194),
  },
  {
    id: "light", label: "Light", style: "solid", isLight: true,
    slice: c(250, 251, 254, 244), sliceHover: c(222, 233, 255, 248), rim: c(0, 0, 0, 44),
    accent: c(28, 108, 235), accent2: c(88, 80, 236), center: c(255, 255, 255, 248), text: c(26, 30, 40), subText: c(98, 106, 124),
  },
  {
    id: "midnight", label: "Midnight", style: "solid", isLight: false,
    slice: c(31, 28, 58, 242), sliceHover: c(60, 50, 118, 246), rim: c(190, 175, 255, 60),
    accent: c(150, 120, 255), accent2: c(205, 110, 255), center: c(17, 15, 34, 246), text: c(240, 238, 255), subText: c(170, 164, 208),
  },
  {
    id: "ocean", label: "Ocean", style: "solid", isLight: false,
    slice: c(16, 46, 56, 242), sliceHover: c(18, 78, 92, 246), rim: c(150, 240, 245, 60),
    accent: c(32, 208, 208), accent2: c(60, 140, 235), center: c(8, 27, 35, 246), text: c(232, 251, 253), subText: c(140, 188, 198),
  },
  {
    id: "sunset", label: "Sunset", style: "solid", isLight: false,
    slice: c(54, 33, 36, 242), sliceHover: c(96, 54, 46, 246), rim: c(255, 214, 190, 60),
    accent: c(255, 144, 70), accent2: c(255, 94, 110), center: c(33, 19, 21, 246), text: c(255, 241, 233), subText: c(204, 172, 162),
  },
  {
    id: "glass", label: "Glass", style: "glass", isLight: false,
    slice: c(255, 255, 255, 34), sliceHover: c(255, 255, 255, 78), rim: c(255, 255, 255, 90),
    accent: c(120, 190, 255), accent2: c(176, 140, 255), center: c(14, 18, 34, 168), text: c(255, 255, 255), subText: c(208, 214, 232),
  },
  {
    id: "neon", label: "Neon", style: "neon", isLight: false,
    slice: c(8, 10, 22, 238), sliceHover: c(20, 26, 56, 242), rim: c(0, 229, 255, 175),
    accent: c(0, 229, 255), accent2: c(255, 64, 200), center: c(6, 8, 18, 244), text: c(225, 250, 255), subText: c(128, 170, 190),
  },
  {
    id: "gradient", label: "Gradient", style: "gradient", isLight: false,
    slice: c(22, 24, 44, 238), sliceHover: c(30, 32, 60, 242), rim: c(255, 255, 255, 56),
    accent: c(255, 106, 136), accent2: c(111, 102, 255), center: c(14, 16, 32, 246), text: c(255, 255, 255), subText: c(182, 188, 214),
  },
  {
    id: "flat", label: "Minimal", style: "flat", isLight: true,
    slice: c(244, 245, 248), sliceHover: c(244, 245, 248), rim: c(0, 0, 0, 0),
    accent: c(37, 99, 235), accent2: c(37, 99, 235), center: c(255, 255, 255), text: c(24, 28, 36), subText: c(110, 116, 130),
  },
];

export const DEFAULT_PRESET = "dark";

export function getPreset(id: string | undefined): Palette {
  return PRESETS.find((p) => p.id === id?.toLowerCase()) ?? PRESETS[0];
}

/** "rgb(79 140 255 / 0.9)" → "#4f8cff" (alpha dropped); anything else is returned unchanged. */
export function toHex(css: string): string {
  const m = /^rgb\((\d+) (\d+) (\d+)/.exec(css);
  if (!m) return css;
  return "#" + [m[1], m[2], m[3]].map((v) => Number(v).toString(16).padStart(2, "0")).join("");
}
