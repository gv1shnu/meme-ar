# Assumptions

Explicit assumptions made while building this MVP.

## Environment / tooling
- **Unity 2022.3 LTS** is the target editor (developed against `2022.3.40f1`). AR Foundation
  5.1.x aligns with this LTS. Opening in a different major Unity version may prompt package
  upgrades.
- The repository was **empty** at the start, so the project was initialized from scratch as a
  Unity + AR Foundation project (the preferred stack in the brief).
- `ProjectSettings/*` beyond `ProjectVersion.txt` are intentionally left for Unity to
  generate on first open. Platform-specific player settings (bundle id, camera usage
  description, min SDK, XR providers) are documented in `README.md` / `DEVICE_TESTING.md`
  rather than committed as hand-authored YAML, to avoid shipping a broken settings file.
  Tunable *runtime* parameters live in the `MemeArConfig` ScriptableObject instead.
- Unity creates `.meta` files on import; they are not committed for source that has not yet
  been opened in an editor. Asmdef references use assembly **names** so they resolve without
  pre-existing GUIDs.

## Scope of the MVP
- **No on-device ML** is implemented. Perception interfaces exist; Simulation mode provides
  observations. In LiveAR mode the scene contains camera pose + planes only (no people/
  objects) until real detectors are added.
- **No networking / no cloud AI.** The remote schema is defined and validated but not wired
  to any transport. The MVP is fully functional offline.
- **Placeholder reaction content only** — original text/color cards generated in code, to
  avoid any copyright-uncertain third-party meme assets.
- Reaction rendering uses a **screen-space overlay**; world anchors are projected to screen
  each frame. True world-space billboards are a later milestone.

## Behavioral defaults
- Observation rate defaults to 30 Hz; frame memory ~1 s; event memory ~20 s.
- Comedic delay ranges default to Instant 0–80 ms, ShortBeat 100–300 ms, Delayed 300–800 ms.
- Global meme cooldown 4 s, duplicate-event cooldown 8 s, max 3 simultaneous overlays.
- Randomness is **seeded** by default (`randomSeed` in config) so runtime behavior and tests
  are reproducible; set the seed to 0 for time-based (non-deterministic) behavior.
- The runtime **bootstrap** auto-creates a Simulation rig only when a scene has no
  `AppController`, so authored scenes are never overridden.

## Privacy
- No facial recognition, no persistent identity, no sensitive-trait inference. Ephemeral
  session-local IDs only. Camera frames are never persisted or uploaded.
