import { defineConfig, loadEnv, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import { cpSync } from 'node:fs';
import { resolve } from 'node:path';
import { handleStickerSearch } from './server/giphy';

const MEDIAPIPE_WASM = resolve(import.meta.dirname, 'node_modules/@mediapipe/tasks-vision/wasm');

/** Serves MediaPipe's wasm runtime in dev and copies it into the build, pinned to the installed version. */
function mediapipeWasm(): Plugin {
  return {
    name: 'mediapipe-wasm',
    configureServer() {
      cpSync(MEDIAPIPE_WASM, resolve(import.meta.dirname, 'public/mediapipe/wasm'), { recursive: true });
    },
    buildStart() {
      cpSync(MEDIAPIPE_WASM, resolve(import.meta.dirname, 'public/mediapipe/wasm'), { recursive: true });
    },
  };
}

/** Runs the /api/stickers function inside the dev server (production uses the Vercel Function). */
function devApi(apiKey: string | undefined): Plugin {
  return {
    name: 'dev-api',
    configureServer(server) {
      server.middlewares.use('/api/stickers', async (req, res) => {
        const request = new Request(new URL(req.originalUrl ?? req.url ?? '/', 'http://localhost'), { method: req.method });
        const response = await handleStickerSearch(request, apiKey);
        res.statusCode = response.status;
        response.headers.forEach((v, k) => res.setHeader(k, v));
        res.end(await response.text());
      });
    },
  };
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  return {
    plugins: [react(), mediapipeWasm(), devApi(env.GIPHY_API_KEY)],
    test: { environment: 'node', include: ['src/**/*.test.ts', 'server/**/*.test.ts'] },
  };
});
