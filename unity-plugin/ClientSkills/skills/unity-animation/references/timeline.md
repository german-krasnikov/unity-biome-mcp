# Timeline

Use Timeline for sequences that coordinate multiple tracks or bindings.

```text
batch(
  commands="""
timeline action=create path=/Director asset_path=Assets/Timelines/Intro.playable tracks="Animation:CharacterMotion"
timeline action=set_binding path=/Director track=CharacterMotion binding=/Character
timeline action=add_clip path=/Director track=CharacterMotion clip=Assets/Animations/Enter.anim start=0 duration=1.5
timeline action=get_bindings path=/Director
timeline action=preview path=/Director time=0.75
""",
  on_error="stop"
)
```

Verify track names, bindings, clip timing, and director assignment after each
logical group. Use `animation` instead when only one object's curve changes;
use `animator` for reusable gameplay states. Timeline creation writes an asset;
if a later command fails, inspect and remove any partial asset explicitly
instead of assuming `atomic=True` can roll back the file.
