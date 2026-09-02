# Device Validation Checklist

The cloud/CI environment cannot exercise ARKit/ARCore, the camera, or real on-device
performance. Everything downstream of `SceneSnapshot` is covered by Simulation mode and the
EditMode tests; the items below must be validated on physical hardware.

## Building a LiveAR scene

Simulation mode runs with zero setup. For LiveAR on a device, build a scene with:

1. `AR Session` (GameObject → XR → AR Session).
2. `XR Origin (AR)` with an `AR Camera` child.
3. `AR Plane Manager` on the XR Origin (assign a plane prefab or leave default).
4. `ARFoundationProvider` component on the AR Camera; assign the `AR Session` and
   `AR Plane Manager` references (they are also auto-discovered).
5. An `AppController` (any GameObject) with `mode = LiveAR`, `arCamera = AR Camera`, and
   `arProvider = ARFoundationProvider`. Add a `MemeRenderer`, `ScreenshotService`, and
   `DebugHud` (or let the runtime bootstrap add renderer/HUD if none exist).
6. Enable **XR Plug-in Management** providers: **ARKit** (iOS) and **ARCore** (Android).

## iOS (ARKit)

- [ ] Camera permission prompt appears on first launch; denial is handled gracefully.
- [ ] `Camera Usage Description` is set (build fails/rejects without it).
- [ ] ARKit supported-device check; unsupported devices show a clear message.
- [ ] AR session reaches tracking; camera feed renders behind overlays.
- [ ] Plane detection finds horizontal/vertical planes.
- [ ] Portrait orientation correct (anchors, overlay positions, projection).
- [ ] Landscape orientation (if enabled) correct.
- [ ] World anchors stay put as the device moves.
- [ ] Screenshot captures camera feed + meme overlays composited.
- [ ] Frame rate holds (target 30–60 FPS) during active reactions.
- [ ] Thermal behavior acceptable over a 5–10 min session.
- [ ] Memory use stable (no growth from meme pooling / temporal buffers).
- [ ] Session interruptions (call, control center) recover.
- [ ] Background → foreground resumes the session cleanly.
- [ ] Meme trigger latency feels immediate (see performance metrics).

## Android (ARCore)

Repeat all of the above using ARCore:

- [ ] Camera permission (added by ARCore) prompts and is handled.
- [ ] ARCore availability / "Google Play Services for AR" install flow.
- [ ] Min API level 24+, ARM64 IL2CPP build runs.
- [ ] Session tracking, plane detection, world anchoring.
- [ ] Orientation handling, screenshot, FPS, thermal, memory.
- [ ] Interruptions and background/foreground.
- [ ] Meme trigger latency.

## Performance metrics to capture

Measure on device (the debug HUD shows live values; log for percentiles):

- [ ] Camera-to-scene latency (frame available → `SceneSnapshot` ready).
- [ ] Event detection latency.
- [ ] Trigger-to-visible latency (target < 100 ms) — **excludes** the deliberate comedic delay.
- [ ] p95 and p99 of trigger-to-visible.
- [ ] FPS average and minimum.
- [ ] Frame drops during reactions.
- [ ] Per-stage compute times (scene update, event, retrieval, ranking, timing, placement,
      render activation) from the HUD's stage profiler.

## Notes

- The MVP's LiveAR scene has no people/objects until on-device perception modules are added,
  so meme triggers in LiveAR will be sparse (camera pose + planes only). Use Simulation mode
  to validate the full trigger pipeline, and LiveAR to validate AR session/tracking/render
  and latency of the render path itself.
