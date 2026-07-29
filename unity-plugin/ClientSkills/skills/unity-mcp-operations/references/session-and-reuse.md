# Session, Change Detection, And Reuse

Use cheap change detection before repeating broad reads.

## Incremental Scene Reads

```text
fingerprint(path="/Environment", depth=3)
```

Store the returned fingerprint in the current reasoning context. If a later
fingerprint is unchanged, do not reread that subtree.

`scene_diff()` keeps its own previous snapshot:

```text
scene_diff()  # establish baseline
# perform the mutation
scene_diff()  # return added and removed lines
```

It does not accept two fingerprint arguments.

Use `get_changes(clear=False)` to inspect Editor events without clearing them,
or the default `clear=True` after consuming the result.

## Session Recovery

- `save_session()` writes a compact hierarchy context for a later cold start.
- `load_session()` returns that saved context beside the current hierarchy.
- Reconfirm connection, scene, mode, and changed targets after loading; saved
  context is historical evidence, not current state.

## Visual Baselines

Use a named `screenshot_baseline(...)`, make one scoped change, then call
`screenshot_compare(...)` with the same dimensions and camera settings.
Pixel differences establish visual change, not behavioral correctness.

## Learned Skills And Templates

Call `list_skills()` or `list_templates()` before recreating a known workflow.
Use learned skills for stable batch-compatible command sequences and templates
for stable parameterized C# scene scaffolds. See
[batching.md](batching.md) for storage and safety rules.
