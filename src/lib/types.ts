import { z } from "zod";
import { getPreset, type RingStyle } from "./themePresets";

export type DisplayMode = "text" | "icon" | "image" | "icon+text" | "image+text";
export const DISPLAY_MODES: DisplayMode[] = ["text", "icon", "image", "icon+text", "image+text"];

export type ActionT =
  | { type: "open"; path: string }
  | { type: "rdp"; file: string; args?: string[] }
  | { type: "run"; command: string; args?: string[]; cwd?: string; hidden?: boolean; terminal?: boolean; shell?: Shell; keepOpen?: boolean }
  | { type: "url"; url: string }
  | { type: "settings" };
export type ActionType = ActionT["type"];
export type Shell = "powershell" | "pwsh" | "cmd";
export const ACTION_TYPES: { type: ActionType; label: string; hint: string }[] = [
  { type: "rdp", label: "Remote desktop (.rdp)", hint: "Opens an .rdp file with mstsc.exe" },
  { type: "open", label: "Open file / folder / app", hint: "Opens with the default handler" },
  { type: "run", label: "Run program / command", hint: "Starts a program, or runs a command in a terminal" },
  { type: "url", label: "Open URL", hint: "Opens in the default browser" },
  { type: "settings", label: "Open settings", hint: "Opens this settings window" },
];

export interface NodeT {
  id: string;
  label?: string;
  icon?: string;
  image?: string;
  display?: DisplayMode;
  color?: string;
  children?: NodeT[];
  action?: ActionT;
}

/** What may be written in config.json under "theme" (all optional). */
export interface ThemeConfig {
  /** One of the built-in ring themes (see themePresets.ts). */
  preset: string;
  /** Settings-window light/dark; defaults to whatever the preset is. */
  mode: "dark" | "light";
  /** Optional overrides on top of the preset: */
  accent: string;
  sliceColor: string;
  textColor: string;
  innerRadius: number;
  thickness: number;
  gap: number;
  fontSize: number;
  animation: boolean;
  /** Overall size multiplier for the whole ring (1 = normal). */
  scale: number;
}

/** The fully resolved theme the ring is drawn with. */
export interface Theme {
  preset: string;
  mode: "dark" | "light";
  style: RingStyle;
  isLight: boolean;
  accent: string;
  accent2: string;
  sliceColor: string;
  sliceHover: string;
  rim: string;
  center: string;
  textColor: string;
  subText: string;
  innerRadius: number;
  thickness: number;
  gap: number;
  fontSize: number;
  animation: boolean;
  scale: number;
}

export interface Behavior {
  dwellMs: number;
  warpCursor: boolean;
  /** "hover": resting on a slice fires it after dwellMs (a click fires it at once). "click": only a click / Enter fires it. */
  activation: "hover" | "click";
}

export type MouseButton = "left" | "right" | "middle" | "back" | "forward";
export type TriggerMode = "press" | "hold";
export type Trigger =
  | { type: "keyboard"; shortcut: string; mode?: TriggerMode; holdMs?: number }
  | { type: "mouse"; button: MouseButton; mode?: TriggerMode; holdMs?: number };

export interface Config {
  version: number;
  trigger: Trigger;
  theme?: Partial<ThemeConfig>;
  behavior?: Partial<Behavior>;
  menu: NodeT[];
}

/** Geometry defaults. */
export const DEFAULT_LAYOUT = { innerRadius: 64, thickness: 150, gap: 6, fontSize: 13, animation: true };

/** Preset first, then any explicit overrides from the config. Old configs (accent/sliceColor/textColor) keep working. */
export function buildTheme(t: Partial<ThemeConfig> = {}): Theme {
  const p = getPreset(t.preset);
  const accent = t.accent ?? p.accent;
  const sliceColor = t.sliceColor ?? p.slice;
  const scale = Math.min(1.5, Math.max(0.4, typeof t.scale === "number" ? t.scale : 1));
  return {
    preset: p.id,
    mode: t.mode ?? (p.isLight ? "light" : "dark"),
    style: p.style,
    isLight: p.isLight,
    accent,
    accent2: t.accent ?? p.accent2,
    sliceColor,
    sliceHover: t.sliceColor ? `color-mix(in srgb, ${sliceColor} 78%, ${accent})` : p.sliceHover,
    rim: p.rim,
    center: t.sliceColor ?? p.center,
    textColor: t.textColor ?? p.text,
    subText: t.textColor ?? p.subText,
    innerRadius: Math.round((t.innerRadius ?? DEFAULT_LAYOUT.innerRadius) * scale),
    thickness: Math.round((t.thickness ?? DEFAULT_LAYOUT.thickness) * scale),
    gap: Math.round((t.gap ?? DEFAULT_LAYOUT.gap) * scale),
    fontSize: Math.max(8, Math.round((t.fontSize ?? DEFAULT_LAYOUT.fontSize) * scale)),
    scale,
    animation: t.animation ?? DEFAULT_LAYOUT.animation,
  };
}

export const DEFAULT_THEME: Theme = buildTheme();
export const DEFAULT_BEHAVIOR: Behavior = { dwellMs: 150, warpCursor: true, activation: "hover" };

export const resolveTheme = (c: Config): Theme => buildTheme(c.theme);
export const resolveBehavior = (c: Config): Behavior => ({ ...DEFAULT_BEHAVIOR, ...c.behavior });

// ---- validation (loose: unknown keys survive a load/save round-trip) ----
const ActionSchema = z.discriminatedUnion("type", [
  z.looseObject({ type: z.literal("open"), path: z.string() }),
  z.looseObject({ type: z.literal("rdp"), file: z.string(), args: z.array(z.string()).optional() }),
  z.looseObject({
    type: z.literal("run"),
    command: z.string(),
    args: z.array(z.string()).optional(),
    cwd: z.string().optional(),
    hidden: z.boolean().optional(),
    terminal: z.boolean().optional(),
    shell: z.enum(["powershell", "pwsh", "cmd"]).optional(),
    keepOpen: z.boolean().optional(),
  }),
  z.looseObject({ type: z.literal("url"), url: z.string() }),
  z.looseObject({ type: z.literal("settings") }),
]);

const NodeSchema: z.ZodType<NodeT> = z.lazy(() =>
  z.looseObject({
    id: z.string().min(1),
    label: z.string().optional(),
    icon: z.string().optional(),
    image: z.string().optional(),
    display: z.enum(["text", "icon", "image", "icon+text", "image+text"]).optional(),
    color: z.string().optional(),
    children: z.array(NodeSchema).optional(),
    action: ActionSchema.optional(),
  }),
);

const TriggerSchema = z.discriminatedUnion("type", [
  z.looseObject({
    type: z.literal("keyboard"),
    shortcut: z.string().min(1),
    mode: z.enum(["press", "hold"]).optional(),
    holdMs: z.number().min(0).optional(),
  }),
  z.looseObject({
    type: z.literal("mouse"),
    button: z.enum(["left", "right", "middle", "back", "forward"]),
    mode: z.enum(["press", "hold"]).optional(),
    holdMs: z.number().min(0).optional(),
  }),
]);

export const ConfigSchema: z.ZodType<Config> = z.looseObject({
  version: z.number().default(1),
  trigger: TriggerSchema,
  theme: z.looseObject({}).optional(),
  behavior: z.looseObject({}).optional(),
  menu: z.array(NodeSchema),
}) as unknown as z.ZodType<Config>;

export type ParseResult = { ok: true; config: Config } | { ok: false; error: string };

export function parseConfig(raw: string): ParseResult {
  let json: unknown;
  try {
    json = JSON.parse(raw);
  } catch (e) {
    return { ok: false, error: `Not valid JSON: ${(e as Error).message}` };
  }
  const r = ConfigSchema.safeParse(json);
  if (!r.success) {
    const i = r.error.issues[0];
    return { ok: false, error: `${i.path.join(".") || "config"}: ${i.message}` };
  }
  const dup = findDuplicateId(r.data.menu);
  if (dup) return { ok: false, error: `Duplicate menu id "${dup}"` };
  return { ok: true, config: r.data };
}

export function findDuplicateId(menu: NodeT[]): string | null {
  const seen = new Set<string>();
  const walk = (ns: NodeT[]): string | null => {
    for (const n of ns) {
      if (seen.has(n.id)) return n.id;
      seen.add(n.id);
      const d = n.children ? walk(n.children) : null;
      if (d) return d;
    }
    return null;
  };
  return walk(menu);
}
