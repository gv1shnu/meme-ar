# MemePacks

Drop user-provided meme packs here, one folder per pack:

```
MemePacks/
  <packname>/
    pack.json      # manifest (see ../../../docs/MEDIA_PACKS.md)
    clip1.mp4
    clip2.mp4
```

Packs are discovered and merged into the reaction catalog at startup. See
`docs/MEDIA_PACKS.md` for the manifest schema, blend modes, and the copyright note:
the footer attribution is credit, **not** a content license — provision clips you have the
right to use.

`example/pack.json.example` shows the format. Rename it to `pack.json` (and add the referenced
clips) to activate it.
