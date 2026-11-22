import { describe, expect, it } from "vitest";
import { activate, buildItems, childrenAt, initialState, step } from "./menuState";
import type { NodeT } from "./types";

const leaf = (id: string): NodeT => ({ id, action: { type: "url", url: "x" } });
const folder = (id: string, children: NodeT[]): NodeT => ({ id, children });
const menu: NodeT[] = [folder("rdp", [folder("admin", [leaf("s1")]), leaf("x")]), leaf("web")];

describe("buildItems", () => {
  it("adds a back slice in sub-menus only", () => {
    expect(buildItems(menu, false, 0, 12).items).toHaveLength(2);
    const sub = buildItems(menu, true, 0, 12).items;
    expect(sub[0]).toEqual({ kind: "back" });
    expect(sub).toHaveLength(3);
  });
  it("paginates when a level has more entries than fit — no entry is lost", () => {
    const many = Array.from({ length: 100 }, (_, i) => leaf(`n${i}`));
    const seen = new Set<string>();
    let pages = 1;
    for (let p = 0; p < 50; p++) {
      const r = buildItems(many, true, p, 12);
      pages = r.pages;
      expect(r.items.length).toBeLessThanOrEqual(12);
      expect(r.items[0].kind).toBe("back");
      expect(r.items[r.items.length - 1].kind).toBe("more");
      r.items.forEach((it) => it.kind === "node" && seen.add(it.node.id));
    }
    expect(pages).toBeGreaterThan(1);
    expect(seen.size).toBe(100);
  });
  it("wraps the page index", () => {
    const many = Array.from({ length: 30 }, (_, i) => leaf(`n${i}`));
    const a = buildItems(many, false, 0, 10);
    expect(buildItems(many, false, a.pages, 10).page).toBe(0);
    expect(buildItems(many, false, -1, 10).page).toBe(a.pages - 1);
  });
});

describe("activate", () => {
  it("descends into a folder, disarms, and asks to recentre", () => {
    const [s, e] = activate(initialState(), { kind: "node", node: menu[0] });
    expect(s.path).toEqual(["rdp"]);
    expect(s.armed).toBe(false);
    expect(e).toEqual({ type: "recenter" });
  });
  it("runs a leaf without changing state", () => {
    const st = initialState();
    const [s, e] = activate(st, { kind: "node", node: menu[1] });
    expect(s).toBe(st);
    expect(e).toEqual({ type: "run", id: "web" });
  });
  it("back pops one level", () => {
    const [s] = activate({ ...initialState(), path: ["rdp", "admin"] }, { kind: "back" });
    expect(s.path).toEqual(["rdp"]);
  });
  it("treats an empty folder as a folder (shows only Back)", () => {
    const [s, e] = activate(initialState(), { kind: "node", node: folder("empty", []) });
    expect(s.path).toEqual(["empty"]);
    expect(e.type).toBe("recenter");
  });
});

describe("childrenAt / step", () => {
  it("resolves paths and returns null for stale ones", () => {
    expect(childrenAt(menu, ["rdp", "admin"])?.[0].id).toBe("s1");
    expect(childrenAt(menu, ["gone"])).toBeNull();
    expect(childrenAt(menu, ["web"])).toBeNull();
  });
  it("keyboard stepping wraps both ways", () => {
    expect(step(null, 5, 1)).toBe(0);
    expect(step(null, 5, -1)).toBe(4);
    expect(step(4, 5, 1)).toBe(0);
    expect(step(0, 5, -1)).toBe(4);
    expect(step(null, 0, 1)).toBeNull();
  });
});
