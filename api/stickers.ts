import { handleStickerSearch } from '../server/giphy.js';

export function GET(request: Request): Promise<Response> {
  return handleStickerSearch(request, process.env.GIPHY_API_KEY);
}
