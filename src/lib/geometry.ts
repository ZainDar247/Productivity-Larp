/** Radial-menu maths. Angles are in degrees, clockwise from 12 o'clock. */

const TAU = Math.PI * 2;
const rad = (deg: number) => (deg * Math.PI) / 180;

/** Angle of a vector (dx, dy in screen coordinates) in [0, 360). */
export function angleOf(dx: number, dy: number): number {
  const deg = (Math.atan2(dx, -dy) * 180) / Math.PI;
  return (deg + 360) % 360;
}

/**
 * Which of `n` slices does the pointer (relative to the ring centre) select?
 * Selection depends on *angle only*: beyond the dead zone every direction maps
 * to a slice, so the pointer can never "escape" the menu.
 * Slice 0 is centred at 12 o'clock and indices run clockwise.
 */
export function sliceAt(dx: number, dy: number, n: number, innerR: number): number | null {
  if (n <= 0) return null;
  if (Math.hypot(dx, dy) < innerR) return null;
  const step = 360 / n;
  return Math.floor(((angleOf(dx, dy) + step / 2) % 360) / step) % n;
}

export const sliceAngle = (i: number, n: number) => (360 / n) * i;

export function polar(r: number, deg: number): { x: number; y: number } {
  return { x: r * Math.sin(rad(deg)), y: -r * Math.cos(rad(deg)) };
}

/** How many slices fit on one ring before the menu should paginate. */
export function fitCount(innerR: number, outerR: number, minArcPx = 76): number {
  const mid = (innerR + outerR) / 2;
  return Math.max(4, Math.floor((TAU * mid) / minArcPx));
}

/** SVG path of slice `i` of `n` as an annular sector with a constant-width gap. */
export function sectorPath(innerR: number, outerR: number, i: number, n: number, gapPx: number): string {
  const f = (v: number) => v.toFixed(2);
  if (n === 1) {
    const ring = (r: number) => `M ${f(r)} 0 A ${f(r)} ${f(r)} 0 1 1 ${f(-r)} 0 A ${f(r)} ${f(r)} 0 1 1 ${f(r)} 0 Z`;
    return `${ring(outerR)} ${ring(innerR)}`; // used with fill-rule: evenodd
  }
  const step = 360 / n;
  const c = sliceAngle(i, n);
  const half = rad(step / 2);
  // constant-width gap: the angular inset shrinks as the radius grows
  const inset = (r: number) => Math.min((gapPx / 2) / r, half * 0.8);
  const a0o = rad(c) - half + inset(outerR), a1o = rad(c) + half - inset(outerR);
  const a0i = rad(c) - half + inset(innerR), a1i = rad(c) + half - inset(innerR);
  const pt = (r: number, a: number) => `${f(r * Math.sin(a))} ${f(-r * Math.cos(a))}`;
  const largeO = a1o - a0o > Math.PI ? 1 : 0;
  const largeI = a1i - a0i > Math.PI ? 1 : 0;
  return [
    `M ${pt(outerR, a0o)}`,
    `A ${f(outerR)} ${f(outerR)} 0 ${largeO} 1 ${pt(outerR, a1o)}`,
    `L ${pt(innerR, a1i)}`,
    `A ${f(innerR)} ${f(innerR)} 0 ${largeI} 0 ${pt(innerR, a0i)}`,
    "Z",
  ].join(" ");
}

export const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v));
