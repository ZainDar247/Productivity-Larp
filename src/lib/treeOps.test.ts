import { describe, expect, it } from "vitest";
import { addNode, allIds, canMoveTo, moveTo, duplicateNode, findNode, indentNode, locate, moveNode, outdentNode, removeNode, uniqueId } from "./treeOps";
import type { NodeT } from "./types";

const leaf = (id: string): NodeT => ({ id, action: { type: "url", url: "u" } });
const mk = (): NodeT[] => [
  { id: "a", children: [leaf("a1"), leaf("a2")] },
  { id: "b", children: [] },
  leaf("c"),
];

describe("treeOps", () => {
  it("adds to root and into a folder", () => {
    expect(addNode(mk(), null, leaf("z")).at(-1)?.id).toBe("z");
    expect(findNode(addNode(mk(), "b", leaf("z")), "b")?.children?.[0].id).toBe("z");
  });
  it("removes a nested node", () => {
    expect(findNode(removeNode(mk(), "a1"), "a1")).toBeNull();
    expect(findNode(removeNode(mk(), "a1"), "a2")).not.toBeNull();
  });
  it("moves within siblings and stops at the ends", () => {
    expect(locate(moveNode(mk(), "a1", 1), "a1")).toEqual({ parent: "a", index: 1 });
    expect(locate(moveNode(mk(), "a1", -1), "a1")).toEqual({ parent: "a", index: 0 });
    expect(locate(moveNode(mk(), "c", -1), "c")).toEqual({ parent: null, index: 1 });
  });
  it("indent puts a node into the previous folder; refuses if it isn't a folder", () => {
    const m = indentNode(mk(), "b"); // b's previous sibling "a" is a folder
    expect(locate(m, "b")).toEqual({ parent: "a", index: 2 });
    expect(locate(indentNode(mk(), "c"), "c")).toEqual({ parent: "b", index: 0 }); // "b" is an (empty) folder
  });
  it("indent is a no-op after a leaf", () => {
    const m: NodeT[] = [leaf("x"), leaf("y")];
    expect(indentNode(m, "y")).toBe(m);
  });
  it("outdent moves a child right after its folder", () => {
    const m = outdentNode(mk(), "a1");
    expect(m.map((n) => n.id)).toEqual(["a", "a1", "b", "c"]);
    expect(findNode(m, "a")?.children?.map((n) => n.id)).toEqual(["a2"]);
  });
  it("generates unique ids", () => {
    expect(uniqueId(mk(), "New Item!")).toBe("new-item");
    expect(uniqueId(mk(), "a")).toBe("a-2");
  });
  it("duplicates a subtree with fresh ids and no collisions", () => {
    const { menu, newId } = duplicateNode(mk(), "a");
    const ids: string[] = [];
    const walk = (ns: NodeT[]) => ns.forEach((n) => (ids.push(n.id), n.children && walk(n.children)));
    walk(menu);
    expect(new Set(ids).size).toBe(ids.length);
    expect(newId).not.toBe("a");
    expect(allIds(menu).size).toBe(8); // 5 original + a copy of a, a1, a2
  });
});

describe("drag and drop moves", () => {
  it("drops before / after a sibling at the root", () => {
    expect(moveTo(mk(), "c", "a", "before").map((n) => n.id)).toEqual(["c", "a", "b"]);
    expect(moveTo(mk(), "a", "c", "after").map((n) => n.id)).toEqual(["b", "c", "a"]);
  });
  it("moves an item into another folder (as its last child)", () => {
    const m = moveTo(mk(), "c", "a", "into");
    expect(findNode(m, "a")?.children?.map((n) => n.id)).toEqual(["a1", "a2", "c"]);
    expect(m.map((n) => n.id)).toEqual(["a", "b"]);
  });
  it("moves an item out of its folder to sit beside a root item", () => {
    const m = moveTo(mk(), "a1", "c", "before");
    expect(m.map((n) => n.id)).toEqual(["a", "b", "a1", "c"]);
    expect(findNode(m, "a")?.children?.map((n) => n.id)).toEqual(["a2"]);
  });
  it("reorders within a folder and handles drop after the last item", () => {
    expect(findNode(moveTo(mk(), "a1", "a2", "after"), "a")?.children?.map((n) => n.id)).toEqual(["a2", "a1"]);
  });
  it("refuses to drop a folder onto itself or into its own subtree", () => {
    expect(canMoveTo(mk(), "a", "a", "into")).toBe(false);
    expect(canMoveTo(mk(), "a", "a1", "before")).toBe(false);
    expect(moveTo(mk(), "a", "a1", "after")).toEqual(mk());
  });
  it("refuses 'into' a plain item", () => {
    expect(canMoveTo(mk(), "a1", "c", "into")).toBe(false);
  });
  it("never loses or duplicates nodes", () => {
    const before = [...allIds(mk())].sort();
    expect([...allIds(moveTo(mk(), "a2", "b", "into"))].sort()).toEqual(before);
  });
});
