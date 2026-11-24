import { useEffect, useState } from "react";
import { bridge } from "../lib/bridge";

const cache = new Map<string, string>();
const direct = /^(data:|https?:|blob:)/i;

/** Resolve an image reference (URL, data: URL or local file path) to something an <image> can show. */
export function useImage(src?: string): string | undefined {
  const [url, setUrl] = useState<string | undefined>(() => (src ? (direct.test(src) ? src : cache.get(src)) : undefined));
  useEffect(() => {
    if (!src) return setUrl(undefined);
    if (direct.test(src)) return setUrl(src);
    // stale-while-revalidate: show what we have at once, but always re-check the file, so a deleted or
    // replaced image never lingers from an old in-memory copy
    const hit = cache.get(src);
    setUrl(hit);
    let live = true;
    bridge
      .loadImage(src)
      .then((u) => {
        cache.set(src, u);
        if (live) setUrl(u);
      })
      .catch(() => {
        cache.delete(src);
        if (live) setUrl(undefined);
      });
    return () => {
      live = false;
    };
  }, [src]);
  return url;
}

export const forgetImage = (src: string) => cache.delete(src);
