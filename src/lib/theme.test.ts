import { describe, expect, it } from "vitest";
import { PRESETS, getPreset, toHex } from "./themePresets";
import { buildTheme, parseConfig, resolveTheme } from "./types";

describe("theme presets", () => {
  it("ships the nine PSW themes", () => {
    expect(PRESETS.map((p) => p.id)).toEqual(["dark", "light", "midnight", "ocean", "sunset", "glass", "neon", "gradient", "flat"]);
  });

  it("falls back to dark for an unknown preset", () => {
    expect(getPreset("nope").id).toBe("dark");
    expect(buildTheme({ preset: "nope" }).preset).toBe("dark");
  });

  it("uses the preset's own colours and style", () => {
    const t = buildTheme({ preset: "neon" });
    expect(t.style).toBe("neon");
    expect(t.accent).toBe(getPreset("neon").accent);
    expect(buildTheme({ preset: "light" }).mode).toBe("light");
  });

  it("keeps legacy colour fields working as overrides on top of the preset", () => {
    const t = buildTheme({ preset: "ocean", accent: "#ff0000", sliceColor: "#101010", textColor: "#eeeeee" });
    expect(t.accent).toBe("#ff0000");
    expect(t.accent2).toBe("#ff0000");
    expect(t.sliceColor).toBe("#101010");
    expect(t.center).toBe("#101010");
    expect(t.textColor).toBe("#eeeeee");
    expect(t.style).toBe("solid"); // the preset still decides the style
  });

  it("an old config with no preset still loads (dark) and ignores the removed dim setting", () => {
    const r = parseConfig(JSON.stringify({ trigger: { type: "keyboard", shortcut: "F1" }, theme: { accent: "#7c5cff", dim: 0.35 }, menu: [] }));
    expect(r.ok).toBe(true);
    if (r.ok) {
      const t = resolveTheme(r.config);
      expect(t.preset).toBe("dark");
      expect(t.accent).toBe("#7c5cff");
      expect("dim" in t).toBe(false);
    }
  });

  it("toHex converts rgb() palette colours for the colour picker", () => {
    expect(toHex("rgb(79 140 255)")).toBe("#4f8cff");
    expect(toHex("rgb(79 140 255 / 0.9)")).toBe("#4f8cff");
    expect(toHex("#abcdef")).toBe("#abcdef");
  });
});
