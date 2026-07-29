---
name: unity-animation
description: Use for AnimationClip keyframes and events, Animator controllers and blend trees, or Timeline cinematic sequences.
---

# Unity Animation

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Enable `MEDIA`, then choose
the model before editing:

| Need | Use |
|---|---|
| Curves or events on one object | `animation` |
| Reusable state machine or blend tree | `animator` |
| Multi-track cinematic sequence | `timeline` |
| Draft Animator changes from natural language | `animator_intent` with dry run |

## AnimationClip

```text
animation(
  action="create",
  path="/Door",
  clip_name="DoorOpen",
  property="localEulerAnglesRaw.y",
  keys="t:0 v:0; t:1 v:90",
  tangent="smooth"
)
animation(action="get", path="/Door", clip="DoorOpen")
animation(action="preview", path="/Door", clip="DoorOpen", time=0.5)
```

Keyframe data belongs in `keys`; do not invent a standalone `value` argument.

## Animator

Read [animator.md](references/animator.md) for parameters, states,
transitions, layers, and blend trees.

## Timeline

Read [timeline.md](references/timeline.md) for tracks, clips, bindings, timing,
markers, and preview.

## Rules

- Inspect existing clips, controllers, or tracks before mutation.
- Use precise tools for exact values; use intent tools for drafts and inspect
  the dry-run result before execution.
- Group only batch-compatible low-level commands.
- Verify names, bindings, transitions, and timing from data.
- Preview representative times, then use screenshots only for visual quality.
