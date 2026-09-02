# Meme Media & Packs

Reactions can be **generated cards** (animated, zero-asset — the default), **still sprites**,
or **video clips with audio** composited onto the live camera feed. Content is
**source-agnostic**: the pipeline references media by a `MediaReference` that resolves to a
bundled `VideoClip`, a file under `StreamingAssets`, or a URL — so where clips come from (and
therefore the licensing strategy) is a swappable decision, never baked in.

## Copyright — read this first

The footer attribution (`Source: …`) is **credit for the audience, not a license**. Crediting
a creator does **not** grant the right to reproduce a viral Instagram/YouTube clip, a film
excerpt, or its music. Pick a defensible source before shipping real clips:

- **User-provided packs** — the user supplies their own clips and takes responsibility (the
  approach below). Safest default posture.
- **Licensed providers / royalty-free / Creative Commons** libraries, under their terms.
- **Original or commissioned** reaction clips.

The app itself bundles only original placeholder cards, so it is always distributable as-is.

## Adding a pack (user-provided clips)

Drop clips and a manifest under `Assets/StreamingAssets/MemePacks/<packname>/`:

```
Assets/StreamingAssets/MemePacks/
  hype/
    pack.json
    wow.mp4
    lets_go.mp4
```

`pack.json`:

```json
{
  "pack": "hype",
  "memes": [
    {
      "id": "hype_wow",
      "title": "WOW",
      "clip": "wow.mp4",
      "source": "Source: @creator (YouTube)",
      "blend": "chroma",
      "tags": ["surprise", "reaction"],
      "events": ["SuddenMotion", "ObjectMoved"],
      "captions": ["WOW", "no wayyy"],
      "durationSec": 3.5,
      "delayMinMs": 0,
      "delayMaxMs": 120,
      "minPeople": 0,
      "placement": "auto"
    }
  ]
}
```

Fields: `clip` (file in the pack folder), `source` (footer attribution), `blend`
(`opaque` | `chroma` | `alpha`), `events` (EventType names the reaction can fire on),
`captions` (support `{P}`/`{O}` placeholders), `placement`
(`auto` | `person` | `object` | `screen`), plus timing/people constraints. Packs are loaded
at startup and merged into the catalog automatically.

## Background integration (blend modes)

- **opaque** — clip framed on the card.
- **chroma** — a solid background color (default green) is keyed out via the
  `MemeAR/UIChromaKey` shader, so the subject sits "in" the scene. Set `chromaKeyColor` on the
  `MediaReference` for non-green screens.
- **alpha** — clip already carries an alpha channel (VP8/VP9/HEVC-with-alpha); rendered
  straight over the feed.

## Scene consistency (packs)

Within one "scene" the app **locks onto a single pack** (`SessionState.ScenePackId`): the
ranker boosts memes from that pack (`scenePackBias`) so reactions stay tonally consistent
instead of jumping between unrelated styles. The lock releases after
`sceneResetSeconds` of no reactions, and the next reaction may lock a new pack. The bundled
catalog ships two packs — `awkward` and `hype` — so this is visible in Simulation mode (watch
the `pack:` field in the HUD).

## Audio

Clip audio plays through an `AudioSource` (via the clip's own track or a separate audio file),
scaled by `masterVolume` and mutable globally (config `enableAudio`, or the **Audio** button
in the debug HUD). No audio is captured or uploaded.

## Text fallback

If a clip can't be resolved or fails to load within `mediaLoadTimeoutSeconds`, the reaction
degrades to its **dialogue text card** (caption + footer) — so a reaction always appears, even
offline or on a missing file.

## Known follow-ups

- Android reads `StreamingAssets` from inside the APK; a production Android build should stream
  pack files via `UnityWebRequest`. The manifest/parser are already platform-independent.
- True world-space (mesh) billboards with depth occlusion; today clips render in the
  screen-space overlay, world-anchored ones projected to screen each frame.
