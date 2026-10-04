import { describe, expect, it, vi } from 'vitest';
import { handleStickerSearch } from './giphy';

const req = (qs: string, method = 'GET') => new Request(`http://localhost/api/stickers?${qs}`, { method });

const giphyResponse = {
  data: [
    { id: 'abc', title: 'Shocked Sticker', url: 'https://giphy.com/stickers/abc', images: { fixed_height: { url: 'https://media.giphy.com/abc.gif', width: '240', height: '200' } } },
    { id: 'no-image', images: {} },
  ],
};

describe('sticker search proxy', () => {
  it('reports when no API key is configured', async () => {
    const res = await handleStickerSearch(req('q=shocked'), undefined);
    expect(res.status).toBe(503);
  });

  it('only forwards queries from the reaction catalog', async () => {
    const fetchImpl = vi.fn();
    const res = await handleStickerSearch(req('q=anything+else'), 'key', fetchImpl);
    expect(res.status).toBe(400);
    expect(fetchImpl).not.toHaveBeenCalled();
  });

  it('rejects unknown ratings and non-GET methods', async () => {
    expect((await handleStickerSearch(req('q=shocked&rating=nsfw'), 'key', vi.fn())).status).toBe(400);
    expect((await handleStickerSearch(req('q=shocked', 'POST'), 'key', vi.fn())).status).toBe(405);
  });

  it('calls GIPHY stickers search and returns trimmed, cacheable hits', async () => {
    const fetchImpl = vi.fn(async (_url: URL) => new Response(JSON.stringify(giphyResponse)));
    const res = await handleStickerSearch(req('q=Shocked&rating=pg'), 'secret', fetchImpl as unknown as typeof fetch);
    expect(res.status).toBe(200);
    expect(res.headers.get('cache-control')).toContain('s-maxage');

    const url = fetchImpl.mock.calls[0][0];
    expect(url.origin + url.pathname).toBe('https://api.giphy.com/v1/stickers/search');
    expect(url.searchParams.get('q')).toBe('shocked');
    expect(url.searchParams.get('rating')).toBe('pg');

    const body = await res.json();
    expect(body.hits).toEqual([{ id: 'abc', title: 'Shocked Sticker', gif: 'https://media.giphy.com/abc.gif', width: 240, height: 200, page: 'https://giphy.com/stickers/abc' }]);
    expect(JSON.stringify(body)).not.toContain('secret');
  });

  it('maps upstream failures to 502', async () => {
    const down = vi.fn(async () => new Response('nope', { status: 429 }));
    expect((await handleStickerSearch(req('q=lol'), 'k', down as unknown as typeof fetch)).status).toBe(502);
    const offline = vi.fn(async () => {
      throw new Error('ENOTFOUND');
    });
    expect((await handleStickerSearch(req('q=lol'), 'k', offline as unknown as typeof fetch)).status).toBe(502);
  });
});
