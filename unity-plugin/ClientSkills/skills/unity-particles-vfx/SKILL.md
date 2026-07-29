---
name: unity-particles-vfx
description: Use for ParticleSystem creation, modules, presets, VFX intent, playback, or particle overdraw checks.
---

# Unity Particles And VFX

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Enable `MEDIA`. Use
`particle` for deterministic settings and `vfx_intent` for an initial draft
that is inspected before acceptance.

```text
batch(
  commands="""
particle action=create path=/Effects name=Impact preset=sparks
particle action=set path=/Effects/Impact module=main prop=startLifetime value=0.6
particle action=set path=/Effects/Impact module=noise prop=enabled value=true
particle action=play path=/Effects/Impact
particle action=get path=/Effects/Impact
""",
  on_error="stop",
  atomic=True
)
```

```text
vfx_intent(
  target="/Effects/Impact",
  intent="Create a short green energy burst with mild turbulence",
  kind="particle",
  dry_run=True
)
```

## Rules

- Inspect current modules before changing them.
- Use exact module/property/value calls for production tuning.
- Preview an intent request before execution.
- Verify playback state and module values from data.
- Use frame captures to assess motion only; do not use them to prove gameplay.
- Check overdraw and material cost for dense effects.
- Stop or remove temporary preview effects after verification.

Bad: `vfx_intent(instruction="...")`.

Good: `vfx_intent(target="...", intent="...", dry_run=True)`.
