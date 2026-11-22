import type { NodeT } from "./types";

/** Pure helpers behind the settings tree editor. All return a new tree. */

export function mapNode(menu: NodeT[], id: string, fn: (n: NodeT) => NodeT): NodeT[] {
  return menu.map((n) => {
    if (n.id === id) return fn(n);
    return n.children ? { ...n, children: mapNode(n.children, id, fn) } : n;
  });
}

export function findNode(menu: NodeT[], id: string): NodeT | null {
  for (const n of menu) {
    if (n.id === id) return n;
    const f = n.children ? findNode(n.children, id) : null;
    if (f) return f;
  }
  return null;
}

/** Location of a node: the sibling array's parent id (null = root) and index. */
export function locate(menu: NodeT[], id: string, parent: string | null = null): { parent: string | null; index: number } | null {
  for (let i = 0; i < menu.length; i++) {
    if (menu[i].id === id) return { parent, index: i };
    const c = menu[i].children;
    const f = c ? locate(c, id, menu[i].id) : null;
    if (f) return f;
  }
  return null;
}

function editSiblings(menu: NodeT[], parent: string | null, fn: (s: NodeT[]) => NodeT[]): NodeT[] {
  if (parent === null) return fn(menu);
  return mapNode(menu, parent, (n) => ({ ...n, children: fn(n.children ?? []) }));
}

export function allIds(menu: NodeT[], out = new Set<string>()): Set<string> {
  for (const n of menu) {
    out.add(n.id);
    if (n.children) allIds(n.children, out);
  }
  return out;
}

export function uniqueId(menu: NodeT[], base: string): string {
  const ids = allIds(menu);
  const slug = base.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "item";
  if (!ids.has(slug)) return slug;
  let i = 2;
  while (ids.has(`${slug}-${i}`)) i++;
  return `${slug}-${i}`;
}

export function addNode(menu: NodeT[], parent: string | null, node: NodeT): NodeT[] {
  return editSiblings(menu, parent, (s) => [...s, node]);
}

export function removeNode(menu: NodeT[], id: string): NodeT[] {
  return menu
    .filter((n) => n.id !== id)
    .map((n) => (n.children ? { ...n, children: removeNode(n.children, id) } : n));
}

export function moveNode(menu: NodeT[], id: string, dir: -1 | 1): NodeT[] {
  const loc = locate(menu, id);
  if (!loc) return menu;
  return editSiblings(menu, loc.parent, (s) => {
    const j = loc.index + dir;
    if (j < 0 || j >= s.length) return s;
    const c = [...s];
    [c[loc.index], c[j]] = [c[j], c[loc.index]];
    return c;
  });
}

/** Make the node the last child of its previous sibling (which must be a folder). */
export function indentNode(menu: NodeT[], id: string): NodeT[] {
  const loc = locate(menu, id);
  if (!loc || loc.index === 0) return menu;
  const node = findNode(menu, id)!;
  const siblings = loc.parent === null ? menu : findNode(menu, loc.parent)!.children!;
  const prev = siblings[loc.index - 1];
  if (!prev.children) return menu;
  return addNode(removeNode(menu, id), prev.id, node);
}

/** Move the node out of its folder to sit right after that folder. */
export function outdentNode(menu: NodeT[], id: string): NodeT[] {
  const loc = locate(menu, id);
  if (!loc || loc.parent === null) return menu;
  const node = findNode(menu, id)!;
  const pl = locate(menu, loc.parent)!;
  const without = removeNode(menu, id);
  return editSiblings(without, pl.parent, (s) => {
    const at = s.findIndex((n) => n.id === loc.parent);
    return [...s.slice(0, at + 1), node, ...s.slice(at + 1)];
  });
}

function reid(menu: NodeT[], n: NodeT, taken: Set<string>): NodeT {
  const base = n.id.replace(/-\d+$/, "");
  let id = base, i = 2;
  while (taken.has(id) || id === n.id) id = `${base}-${i++}`;
  taken.add(id);
  return { ...n, id, children: n.children?.map((c) => reid(menu, c, taken)) };
}

/** Duplicate a node (and its subtree) right after itself, with fresh ids. */
export function duplicateNode(menu: NodeT[], id: string): { menu: NodeT[]; newId: string } {
  const loc = locate(menu, id);
  const orig = findNode(menu, id);
  if (!loc || !orig) return { menu, newId: id };
  const copy = reid(menu, structuredClone(orig), allIds(menu));
  const out = editSiblings(menu, loc.parent, (s) => [...s.slice(0, loc.index + 1), copy, ...s.slice(loc.index + 1)]);
  return { menu: out, newId: copy.id };
}
