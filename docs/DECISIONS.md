# Engineering Decision Log

Concise record of the architectural choices behind the MemeAR MVP.

## 1. Unity + AR Foundation
Chosen for genuine cross-platform (iOS/Android) AR from one C# codebase. AR Foundation
provides a single API over ARKit and ARCore, so tracking, plane detection, and anchors are
written once. Unity 2022.3 LTS + AR Foundation 5.1 is a mature, well-supported combination
with stable ARKit/ARCore providers. Alternatives (native ARKit + native ARCore, or
web/WebXR) were rejected: native means two codebases; web cannot meet the on-device,
low-latency perception/trigger/render requirement. The requirement is explicitly a mobile,
on-device app.

## 2. Cloud reasoning is outside the critical timing path
Any network/model round-trip has unbounded, variable latency. Putting it in the
trigger→render path would make the punchline feel laggy and, worse, tie comedic timing to
infrastructure latency. Instead the design is: **slow semantic intelligence** continuously
updates context and pre-warms candidate reactions; a **fast local reflex** detects the
trigger and renders an already-prepared reaction. Remote intelligence can only *prepare*,
never *gate*, the punchline.

## 3. SceneSnapshot is the central boundary
A single, strongly-typed, engine-agnostic scene representation sits between perception and
everything else. Perception (real or simulated) produces it; temporal memory, prediction,
events, comedy, retrieval, timing, placement, and rendering consume only it. This is the
seam that makes ML/VLM models substitutable without touching downstream code, and it keeps
AR Foundation types from leaking into meme logic.

## 4. Prediction / prefetch ("ammunition cache")
Retrieval and ranking, and later any remote call, cost time. By turning anticipation
signals into `PotentialEvent`s and preparing ranked candidates *before* an event confirms,
the confirmed-trigger path only has to pick a cached candidate and schedule it. This keeps
trigger-to-render low and models the real product's "load the joke, then fire it" behavior.

## 5. Comedic delay is independent of compute latency
Two separate concepts: computational latency (minimized, measured per stage) and comedic
delay (a deliberate creative parameter). Only `ComedyTimingPolicy` introduces delay, from
configured ranges, and it schedules against a timestamp rather than sleeping/blocking. This
guarantees that if compute gets faster or slower, comedic timing is unaffected — the joke
lands when it is *funniest*, not when the CPU happens to be done.

## 6. Remote intelligence returns declarative instructions
The future cloud schema (`Remote/RemoteReactionSchema.cs`) is pure data: it may only
*reference* a bundled meme by id and supply bounded parameters (caption, target, timing,
animation, duration). It is validated (`RenderInstructionValidator`) and translated locally.
It can never carry code, assets, or unbounded values, so there is no path to arbitrary
remote code execution. The same `MemeRenderInstruction` type is produced locally and from
remote, so the renderer treats both identically.

## 7. Identity recognition is excluded
The product only needs observable signals (pose, movement, spatial relations, smile-like
facial cues, head direction) to be funny. Face recognition or persistent identity would add
serious privacy risk for no comedic benefit. People are tracked with ephemeral,
session-local IDs (P1, P2…) that never persist. Facial cues are probabilistic and named as
*cues/observations*, never as objective emotion or sensitive traits.

## 8. Simulation and live perception share the same downstream pipeline
The only difference between demo and live is the observation provider; both emit
`SceneSnapshot`. There is deliberately no separate "fake" rendering or trigger path. This
means Simulation mode validates the exact code that will run on device (relation
extraction, events, prediction, scoring, timing, placement, rendering), and CI can exercise
the whole pipeline headlessly without ARKit/ARCore.

## 9. Single runtime assembly, folder + namespace modularity
Code is organized by responsibility into folders and `MemeAR.<Module>` namespaces, but
compiled as one runtime assembly (`MemeAR.Runtime`) plus a test assembly. This keeps the
module boundaries clear (enforced by dependency discipline and interfaces) while avoiding a
fragile web of assembly-definition references. AR Foundation usage is confined to a single
file behind interfaces, so the coupling concern is addressed at the code level, not by
assembly split.

## 10. Screen-space overlay rendering for the MVP
Reaction cards render into one robust `ScreenSpaceOverlay` canvas; world-anchored reactions
are projected to screen each frame via the spatial provider. This avoids world-canvas /
billboard / occlusion complexity for the first MVP while still tracking AR world anchors.
True world-space billboards with depth occlusion are a documented next milestone.

## 11. Pure-C# orchestrator for testability
`MemePipeline` is a plain C# class (no MonoBehaviour). The Unity layer (`AppController`)
only drives it and owns providers/renderer. This lets the entire pipeline run in headless
EditMode tests with a mock renderer and a manual clock, which is essential because the cloud
build environment cannot run AR.
