import { ALLOWED_QUERIES } from '../src/stickers/catalog.js';

/**
 * GIPHY sticker search proxy. Keeps the API key server-side and only forwards
 * queries from the app's own reaction catalog. Written against the standard
 * Fetch API Request/Response so it runs unchanged as a Vercel Function and as
 * Vite dev-server middleware.
 */

export interface StickerHit {
  id: string;
  title: string;
  /** Animated GIF with transparency (fixed height ~200px). */
  gif: string;
  width: number;
  height: number;
  /** GIPHY page for attribution. */
  page: string;
}

const RATINGS = new Set(['g', 'pg', 'pg-13', 'r']);

interface GiphyImage {
  url?: string;
  width?: string;
  height?: string;
}
interface GiphyItem {
  id: string;
  title?: string;
  url?: string;
  images?: { fixed_height?: GiphyImage };
}

const json = (status: number, body: unknown, cache = 'no-store') =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json', 'cache-control': cache },
  });

export async function handleStickerSearch(
  request: Request,
  apiKey: string | undefined,
  fetchImpl: typeof fetch = fetch,
): Promise<Response> {
  if (request.method !== 'GET') return json(405, { error: 'method_not_allowed' });
  if (!apiKey) return json(503, { error: 'not_configured' });

  const params = new URL(request.url).searchParams;
  const q = (params.get('q') ?? '').trim().toLowerCase();
  const rating = (params.get('rating') ?? 'pg-13').toLowerCase();
  if (!ALLOWED_QUERIES.has(q)) return json(400, { error: 'unknown_query' });
  if (!RATINGS.has(rating)) return json(400, { error: 'bad_rating' });

  const upstream = new URL('https://api.giphy.com/v1/stickers/search');
  upstream.search = new URLSearchParams({ api_key: apiKey, q, limit: '25', rating, lang: 'en', bundle: 'messaging_non_clips' }).toString();

  let res: Response;
  try {
    res = await fetchImpl(upstream);
  } catch {
    return json(502, { error: 'upstream_unreachable' });
  }
  if (!res.ok) return json(502, { error: 'upstream_error', status: res.status });

  const body = (await res.json()) as { data?: GiphyItem[] };
  const hits: StickerHit[] = (body.data ?? [])
    .map((d) => {
      const img = d.images?.fixed_height;
      if (!img?.url) return null;
      return {
        id: d.id,
        title: d.title ?? '',
        gif: img.url,
        width: Number(img.width) || 200,
        height: Number(img.height) || 200,
        page: d.url ?? `https://giphy.com/stickers/${d.id}`,
      };
    })
    .filter((h): h is StickerHit => h !== null);

  // Same query + rating always yields the same list for a while: let the CDN cache it.
  return json(200, { hits }, 'public, s-maxage=86400, stale-while-revalidate=604800');
}
