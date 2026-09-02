# Future Cloud Response Schema

Remote intelligence (a VLM/LLM running off-device) is treated as a source of **declarative
instructions**, never code. It is defined here and stubbed in `Assets/App/Remote/`, but
**no networking is implemented in the MVP** and remote reasoning is never in the punchline
critical path (see `DECISIONS.md` §2, §6).

## Contract

A remote response is a single JSON object:

```json
{
  "action": "spawn_meme",
  "meme_id": "reaction_side_eye",
  "caption": "…really, P2?",
  "target": { "type": "person", "id": "P2", "anchor": "above_head" },
  "timing": { "mode": "short_beat", "delay_ms": 200 },
  "animation": "pop",
  "duration_ms": 5000,
  "confidence": 0.87
}
```

| Field         | Meaning                                                                 |
|---------------|-------------------------------------------------------------------------|
| `action`      | Only `"spawn_meme"` is accepted.                                         |
| `meme_id`     | Must reference a **bundled** meme. Unknown ids are rejected.             |
| `caption`     | Optional caption text; falls back to the meme title.                    |
| `target.type` | `person` \| `object` \| `world` \| `screen`.                            |
| `target.id`   | Ephemeral session id (`P2`, `O1`) or empty.                             |
| `target.anchor` | `above_head` \| `near` \| `screen`.                                   |
| `timing.mode` | `instant` \| `short_beat` \| `delayed`.                                 |
| `timing.delay_ms` | Comedic delay, clamped to 0–2000 ms locally.                        |
| `animation`   | `pop` \| `slide` \| `fade` \| `bounce`.                                 |
| `duration_ms` | Visible duration, clamped to the local valid range.                    |
| `confidence`  | Advisory only.                                                          |

## Safety model

- The remote side may only **reference** bundled memes and supply **bounded** parameters.
  It cannot deliver assets, code, or unbounded values.
- Every instruction is validated by `RenderInstructionValidator` and translated by
  `RemoteInstructionTranslator` into the same `MemeRenderInstruction` the local pipeline
  produces. The renderer cannot tell the two apart and treats both as untrusted data.
- Anything unknown, malformed, or out of range is rejected — there is **no path to
  arbitrary remote code execution**.
- Remote reasoning may only *pre-warm* candidate reactions ahead of time; it never gates or
  blocks the local trigger→render path.

## Intended usage

1. The slow path streams periodic scene context to a remote model.
2. The model returns candidate reactions (as above) that are validated and cached locally.
3. The fast local reflex detects the confirmed trigger and renders a prepared candidate
   immediately — with no dependency on the remote round-trip completing in time.
