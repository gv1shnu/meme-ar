# Punchline

Drop in a video and Punchline adds meme reactions exactly when the funny moments happen: a surprised face, a facepalm, a fall, a snack grab, an awkward silence. Stickers come from GIPHY, follow the person they're reacting to, and the result plays back in the page or downloads as a video.

**Everything runs in the browser.** The video is analyzed on-device with MediaPipe and is never uploaded. The only server code is a small proxy that keeps the GIPHY API key secret.

## How it works

```
video ──► sample frames (8 fps) ──► MediaPipe: pose · face expressions · objects
                                     + camera cut / pan estimation + audio loudness
      ──► track people (ephemeral ids P1, P2…, reset at every camera cut)
      ──► detect events  ──► score & plan reactions with comedic timing
      ──► fetch GIPHY stickers ──► place & animate (preview canvas / MP4 export)
```

| Stage | File | Notes |
| --- | --- | --- |
| Perception | `src/analysis/perception.ts` | Pose, face blendshapes (plus an upscaled head-crop pass for distant faces), COCO objects. GPU with CPU fallback. |
| Camera | `src/analysis/camera.ts` | Cut detection (pixel + color-histogram change) and global shift, so pans and tilts aren't read as people moving. |
| Tracking | `src/analysis/tracker.ts` | Greedy IoU tracker. No identity recognition; ids never outlive a video. |
| Events | `src/analysis/detectors.ts` | 14 event types. Since the whole clip is known, cues are confirmed as *sustained* yet stamped at their true onset. Plausibility gates drop pose-model hallucinations in crowds. |
| Planning | `src/analysis/planner.ts` | Scores events, enforces spacing, per-type cooldowns and a density budget, then adds a deliberate comedic delay (instant / beat / delayed) per event type. Seeded, so plans are reproducible. |
| Placement | `src/analysis/placement.ts` | Sticker rides beside the person's head on the roomier side; scene-level reactions take the emptiest corner. |
| Stickers | `src/stickers/` | GIPHY sticker search via `/api/stickers`, GIF decoding to frames, emoji fallback when GIPHY is unavailable. |
| Render | `src/render/` | One compositor drives both the live preview and the export (canvas + MediaRecorder, original audio kept). |

Events: surprise, laugh, celebrate, facepalm, fall, jump, sudden motion, snack grab, phone check, pet spotted, entrance, exit, awkward silence, loud noise.

## Run locally

Requires Node 20+ and pnpm.

```bash
pnpm install
cp .env.example .env   # then set GIPHY_API_KEY
pnpm dev
```

Get a free key at [developers.giphy.com](https://developers.giphy.com/) → **Create an App** → **API**. Without a key the app still works, using built-in emoji stickers.

```bash
pnpm test        # unit tests (detectors, planner, placement, camera, proxy, render)
pnpm build       # typecheck + production build into dist/
```

## Deploy (Vercel)

The repo is ready for Vercel as-is: `vercel.json` configures the Vite build, and `api/stickers.ts` becomes a serverless function.

1. Import the repository in Vercel (framework preset **Vite** is detected).
2. Add the environment variable `GIPHY_API_KEY` (Production and Preview).
3. Deploy.

Before going to production, upgrade your GIPHY key from beta to production in the GIPHY dashboard. Beta keys are heavily rate-limited. Sticker search responses are CDN-cached for a day, and the proxy only accepts the app's own fixed search terms, so the key can't be used as an open search proxy.

## Limits

- Videos up to 3 minutes / 500 MB. Analysis takes about 0.8x the video's length on a laptop GPU.
- Export re-plays the video in real time, so keep the tab in front while it renders. MP4 where the browser can record it (Chrome, Edge, Safari), WebM otherwise.
- Face-based events need reasonably clear faces. Body events need people at least ~15% of the frame tall.
- Stickers are GIPHY content under GIPHY's terms. The page shows the required "Powered by GIPHY" attribution.

## Privacy

No uploads, no face recognition, no persistent identity. Detected people get session-local ids (P1, P2…) that exist only while the page is open.
