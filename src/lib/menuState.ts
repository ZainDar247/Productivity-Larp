import type { NodeT } from "./types";

/** What one slice on the ring represents. */
export type Item =
  | { kind: "node"; node: NodeT }
  | { kind: "back" }
  | { kind: "more"; page: number; pages: number };

export interface MenuState {
  /** ids of the folders we have descended into (root = []). */
  path: string[];
  page: number;
  highlight: number | null;
  /** Selection is ignored until the pointer has visited the centre — stops a
   *  freshly opened layer from instantly selecting whatever the cursor rests on. */
  armed: boolean;
}

export const initialState = (): MenuState => ({ path: [], page: 0, highlight: null, armed: true });

export type Effect = { type: "none" } | { type: "run"; id: string } | { type: "recenter" };

/** Resolve the folder chain. Returns null if the config changed under us. */
export function childrenAt(menu: NodeT[], path: string[]): NodeT[] | null {
  let level = menu;
  for (const id of path) {
    const next = level.find((n) => n.id === id);
    if (!next || !next.children) return null;
    level = next.children;
  }
  return level;
}

export function isFolder(n: NodeT): boolean {
  return Array.isArray(n.children);
}

/**
 * Lay a level out as slices. If it doesn't fit in `capacity` slots it is split
 * into pages and a "more" slice cycles through them — so a level can hold any
 * number of entries.
 */
export function buildItems(
  children: NodeT[],
  isSub: boolean,
  page: number,
  capacity: number,
): { items: Item[]; pages: number; page: number } {
  const back: Item[] = isSub ? [{ kind: "back" }] : [];
  if (children.length + back.length <= capacity) {
    return { items: [...back, ...children.map((node): Item => ({ kind: "node", node }))], pages: 1, page: 0 };
  }
  const size = Math.max(2, capacity - back.length - 1);
  const pages = Math.ceil(children.length / size);
  const p = ((page % pages) + pages) % pages;
  const slice = children.slice(p * size, (p + 1) * size).map((node): Item => ({ kind: "node", node }));
  return { items: [...back, ...slice, { kind: "more", page: p, pages }], pages, page: p };
}

/** Apply a chosen slice. Pure: returns the next state and a side effect for the caller. */
export function activate(state: MenuState, item: Item): [MenuState, Effect] {
  switch (item.kind) {
    case "back":
      return [{ path: state.path.slice(0, -1), page: 0, highlight: null, armed: false }, { type: "recenter" }];
    case "more":
      return [{ ...state, page: state.page + 1, highlight: null, armed: false }, { type: "recenter" }];
    case "node":
      if (isFolder(item.node)) {
        return [
          { path: [...state.path, item.node.id], page: 0, highlight: null, armed: false },
          { type: "recenter" },
        ];
      }
      return [state, { type: "run", id: item.node.id }];
  }
}

/** Keyboard navigation around the ring. */
export function step(highlight: number | null, n: number, dir: 1 | -1): number | null {
  if (n <= 0) return null;
  if (highlight === null) return dir > 0 ? 0 : n - 1;
  return (highlight + dir + n) % n;
}

export function itemLabel(item: Item): string {
  switch (item.kind) {
    case "back": return "Back";
    case "more": return `More (${item.page + 1}/${item.pages})`;
    case "node": return item.node.label ?? item.node.id;
  }
}
