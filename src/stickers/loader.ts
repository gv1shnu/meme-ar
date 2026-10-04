import type { EventType, PlannedReaction } from '../analysis/types';
import type { StickerHit } from '../../server/giphy';
import { REACTIONS } from './catalog';
import { decodeGif, frameIndexAt, type DecodedAnimation } from './gif';
import { pick, rng } from '../util/random';

export interface Sticker {
  source: 'giphy' | 'emoji';
  width: number;
  height: number;
  frameAt(ms: number): CanvasImageSource;
  /** GIPHY attribution, when the sticker came from GIPHY. */
  credit?: { title: string; page: string; id: string };
  thumb?: string;
}

export type GiphyStatus = 'unknown' | 'ok' | 'unavailable';

export interface StickerLoaderOptions {
  rating: string;
  seed: number;
  /** Override for tests. */
  fetchImpl?: typeof fetch;
}

/** Draws the built-in fallback sticker: big emoji over a punchy caption. */
export function emojiSticker(type: EventType): Sticker {
  const style = REACTIONS[type];
  const W = 320;
  const H = 300;
  const canvas = new OffscreenCanvas(W, H);
  const ctx = canvas.getContext('2d')!;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.font = '170px "Apple Color Emoji","Segoe UI Emoji","Noto Color Emoji",sans-serif';
  ctx.fillText(style.emoji, W / 2, 110);

  let size = 52;
  ctx.font = `900 ${size}px "Arial Black",Impact,system-ui,sans-serif`;
  while (ctx.measureText(style.caption).width > W - 16 && size > 22) {
    size -= 2;
    ctx.font = `900 ${size}px "Arial Black",Impact,system-ui,sans-serif`;
  }
  ctx.lineJoin = 'round';
  ctx.lineWidth = Math.max(6, size / 5);
  ctx.strokeStyle = '#000';
  ctx.strokeText(style.caption, W / 2, 245);
  ctx.fillStyle = '#fff';
  ctx.fillText(style.caption, W / 2, 245);

  return { source: 'emoji', width: W, height: H, frameAt: () => canvas };
}

function animatedSticker(anim: DecodedAnimation, hit: StickerHit): Sticker {
  return {
    source: 'giphy',
    width: anim.width,
    height: anim.height,
    frameAt: (ms) => anim.frames[frameIndexAt(anim, ms)],
    credit: { title: hit.title, page: hit.page, id: hit.id },
    thumb: hit.gif,
  };
}

/**
 * Picks and decodes one sticker per reaction. Search results are cached per
 * query, stickers are not repeated within a video, and any GIPHY failure
 * degrades that reaction to the emoji sticker rather than failing the render.
 */
export class StickerLoader {
  status: GiphyStatus = 'unknown';
  private searches = new Map<string, Promise<StickerHit[]>>();
  private used = new Set<string>();
  private random: () => number;
  private fetchImpl: typeof fetch;

  constructor(private opts: StickerLoaderOptions) {
    this.random = rng(opts.seed);
    this.fetchImpl = opts.fetchImpl ?? fetch.bind(globalThis);
  }

  private search(q: string): Promise<StickerHit[]> {
    const key = `${q}|${this.opts.rating}`;
    let p = this.searches.get(key);
    if (!p) {
      p = this.fetchImpl(`/api/stickers?${new URLSearchParams({ q, rating: this.opts.rating })}`).then(async (res) => {
        if (res.status === 503) this.status = 'unavailable';
        if (!res.ok) throw new Error(`sticker search ${res.status}`);
        this.status = 'ok';
        return ((await res.json()) as { hits: StickerHit[] }).hits;
      });
      p.catch(() => this.searches.delete(key));
      this.searches.set(key, p);
    }
    return p;
  }

  async load(reaction: PlannedReaction): Promise<Sticker> {
    const type = reaction.event.type;
    if (this.status === 'unavailable') return emojiSticker(type);
    try {
      const queries = [...REACTIONS[type].queries].sort(() => this.random() - 0.5);
      for (const q of queries) {
        const fresh = (await this.search(q)).filter((h) => !this.used.has(h.id));
        if (!fresh.length) continue;
        const hit = pick(fresh.slice(0, 12), this.random);
        this.used.add(hit.id);
        const res = await this.fetchImpl(hit.gif);
        if (!res.ok) continue;
        return animatedSticker(await decodeGif(await res.arrayBuffer()), hit);
      }
    } catch {
      // fall through to the built-in sticker
    }
    return emojiSticker(type);
  }
}
