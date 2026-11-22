import { describe, expect, it } from "vitest";
import { angleOf, fitCount, polar, sectorPath, sliceAt } from "./geometry";

describe("angleOf", () => {
  it("is clockwise from the top", () => {
    expect(angleOf(0, -10)).toBeCloseTo(0);
    expect(angleOf(10, 0)).toBeCloseTo(90);
    expect(angleOf(0, 10)).toBeCloseTo(180);
    expect(angleOf(-10, 0)).toBeCloseTo(270);
  });
});

describe("sliceAt", () => {
  it("has a dead zone in the middle", () => {
    expect(sliceAt(5, 5, 4, 64)).toBeNull();
  });
  it("slice 0 is centred on top, indices go clockwise", () => {
    expect(sliceAt(0, -100, 4, 64)).toBe(0);
    expect(sliceAt(100, 0, 4, 64)).toBe(1);
    expect(sliceAt(0, 100, 4, 64)).toBe(2);
    expect(sliceAt(-100, 0, 4, 64)).toBe(3);
  });
  it("wraps just left of top back to slice 0", () => {
    expect(sliceAt(-1, -100, 6, 64)).toBe(0);
    expect(sliceAt(1, -100, 6, 64)).toBe(0);
  });
  it("selects a slice for EVERY direction outside the dead zone and any distance", () => {
    for (const n of [1, 2, 3, 5, 8, 13, 40]) {
      for (let a = 0; a < 360; a += 3) {
        for (const r of [65, 200, 5000]) {
          const { x, y } = polar(r, a);
          const s = sliceAt(x, y, n, 64);
          expect(s).not.toBeNull();
          expect(s!).toBeGreaterThanOrEqual(0);
          expect(s!).toBeLessThan(n);
        }
      }
    }
  });
  it("agrees with the drawn slice centres", () => {
    const n = 7;
    for (let i = 0; i < n; i++) {
      const { x, y } = polar(150, (360 / n) * i);
      expect(sliceAt(x, y, n, 64)).toBe(i);
    }
  });
  it("returns null for an empty ring", () => {
    expect(sliceAt(0, -100, 0, 64)).toBeNull();
  });
});

describe("fitCount / sectorPath", () => {
  it("fits more slices on a bigger ring but never fewer than 4", () => {
    expect(fitCount(10, 12)).toBe(4);
    expect(fitCount(64, 214)).toBeGreaterThan(fitCount(64, 150));
  });
  it("produces finite paths for any n", () => {
    for (const n of [1, 2, 3, 12, 60]) {
      for (let i = 0; i < n; i++) {
        expect(sectorPath(64, 214, i, n, 6)).not.toMatch(/NaN|Infinity/);
      }
    }
  });
});
