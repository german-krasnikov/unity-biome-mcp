# Animation Tools

Choose the tool by the asset or system you need to change:

| Goal | Tool |
|---|---|
| Author keyframes in an `AnimationClip` | `animation` |
| Build an Animator Controller state machine | `animator` |
| Sequence animation, audio, activation, or signals | `timeline` |
| Create or configure a Particle System | `particle` |

These tools can modify scene objects and project assets. Inspect first, make one
focused change, and inspect again. Keep animation assets under source control
before destructive state, track, clip, or curve operations.

## `animation` {#animation}

Use `animation` for keyframes on one GameObject. Creating a clip saves it under
`Assets/Animations`, adds an Animator when needed, and assigns an Animator
Controller containing the new motion.

```python
created = await animation(
    action="create",
    path="/Player",
    clip_name="Jump",
    property="localPosition",
    keys="t:0 v:(0,0,0); t:0.5 v:(0,2,0); t:1 v:(0,0,0)",
    tangent="smooth",
)
```

`keys` uses semicolon-separated `t:<seconds> v:<value>` entries. Values can be
numbers, vectors, or colors. `component_type` defaults to `Transform`, while
`binding_path` targets a child relative to the animated root.

Read a clip before editing it:

```python
current = await animation(action="get", path="/Player", clip="Jump")

await animation(
    action="set_keys",
    path="/Player",
    clip="Jump",
    property="localPosition.y",
    keys="t:0 v:0; t:0.4 v:2.2; t:1 v:0",
    tangent="smooth",
)

verified = await animation(action="get", path="/Player", clip="Jump")
```

`edit` and `add_key` add keys; use `set_keys` when the supplied keys should
replace a curve. `remove_key` removes the key at the time encoded in `keys`, and
`remove_curve` removes the selected binding. `set_wrap` and `set_framerate` use
`keys` for the new value, for example `keys="loop"` or `keys="30"`.

Animation events use the same scene path and clip identity:

```python
await animation(
    action="add_event",
    path="/Player",
    clip="Walk",
    time=0.35,
    function_name="OnFootstep",
)
events = await animation(action="get_events", path="/Player", clip="Walk")
```

`preview` samples a clip at one time in the Editor; it does not run the whole
gameplay sequence. Use `get_clip_path` when a later tool needs the saved asset
path.

## `animator` {#animator}

Use `animator` for parameters, states, transitions, layers, and blend trees. The
tool adds an Animator and creates `Assets/Animations/<object>.controller` when an
authoring action needs a controller and the object has none.

Build parameters before transitions, and add states before setting the default:

```python
await animator(
    action="add_param",
    path="/Player",
    params="Speed:float:0; IsGrounded:bool:true; Jump:trigger",
)
await animator(
    action="add_state",
    path="/Player",
    states=(
        "Idle:Assets/Animations/Idle.anim; "
        "Walk:Assets/Animations/Walk.anim"
    ),
)
await animator(action="set_default", path="/Player", state="Idle")
await animator(
    action="add_transition",
    path="/Player",
    source="Idle",
    target="Walk",
    conditions="Speed>0.1; IsGrounded",
    duration=0.15,
)

controller = await animator(action="get", path="/Player")
```

Use `source="*"` for an Any State transition. Conditions support numeric
comparisons, boolean parameters, triggers, and `!BoolName`. If conditions are
provided without an explicit exit-time setting, the new transition does not wait
for exit time.

Blend trees are created as Animator states:

```python
await animator(
    action="add_blend_tree",
    path="/Player",
    state="Locomotion",
    blend_type="1d",
    param="Speed",
    children="Idle:0; Walk:0.5; Run:1",
)
tree = await animator(
    action="get_blend_tree",
    path="/Player",
    state="Locomotion",
)
```

The blend parameters are created as floats when missing. Layer-sensitive state,
transition, and default-state actions use `layer`; layer management accepts a
layer name or index where documented.

For renaming, pass the old identity separately from the new name:

```python
await animator(
    action="rename_param",
    path="/Player",
    param="Speed",
    name="MoveSpeed",
)
await animator(
    action="rename_state",
    path="/Player",
    state="Walk",
    name="Walking",
)
```

Inspect the controller after removals, renames, and transition updates; these
operations mutate the shared Animator Controller asset.

## `timeline` {#timeline}

Use `timeline` for a multi-track sequence. Create the Timeline asset and attach it
to a GameObject with a PlayableDirector:

```python
await timeline(
    path="/Cutscene",
    action="create",
    asset_path="Assets/Cinematics/Intro.playable",
)
```

If `/Cutscene` does not exist, creation makes that GameObject and adds a
PlayableDirector. Add a track, bind it, then add a clip:

```python
await timeline(
    path="/Cutscene",
    action="add_track",
    track="PlayerAnimation",
    track_type="Animation",
)
await timeline(
    path="/Cutscene",
    action="set_binding",
    track="PlayerAnimation",
    binding="/Player",
)
await timeline(
    path="/Cutscene",
    action="add_clip",
    track="PlayerAnimation",
    clip="Assets/Animations/Walk.anim",
    start=0,
    duration=2,
)

sequence = await timeline(path="/Cutscene", action="get")
bindings = await timeline(path="/Cutscene", action="get_bindings")
```

Supported root track types are `Animation`, `Audio`, `Activation`, `Signal`,
`Control`, and `Group`. `set_binding` requires a PlayableDirector GameObject path,
not only a `.playable` asset path.

Markers belong to a track and use `start` for their time:

```python
await timeline(
    path="/Cutscene",
    action="add_marker",
    track="Signals",
    start=1.25,
    name="OpenDoor",
)
```

For removal, select a marker by `name`, `start`, or both, and still provide the
track. Timeline write actions are rejected in Play Mode; `get`, `get_bindings`,
and `preview` remain available. `preview` samples the PlayableDirector at `time`.

## `particle` {#particle}

Create a Particle System under an existing parent, optionally with a deterministic
preset:

```python
await particle(
    action="create",
    path="/Effects",
    name="ExplosionFX",
    preset="explosion",
)
```

Available creation/application presets are `fire`, `smoke`, `sparks`, `rain`,
`snow`, `explosion`, `magic`, `dust`, `blood`, and `trail`.

Inspect the relevant module, change one property, and read it again:

```python
before = await particle(
    action="get",
    path="/Effects/ExplosionFX",
    module="emission",
)
await particle(
    action="set",
    path="/Effects/ExplosionFX",
    module="emission",
    prop="rateOverTime",
    value="100",
)
after = await particle(
    action="get",
    path="/Effects/ExplosionFX",
    module="emission",
)
```

`apply` resets an existing system to a named preset; `play`, `pause`, and `stop`
control its preview. Use [`vfx_intent`](../features/intent-tools.md#vfx_intent)
when the desired particle result is easier to describe than to configure module
by module.

## Verification checklist

1. Read the clip, controller, Timeline, or Particle System before editing it.
2. Keep asset paths and scene-object paths distinct.
3. Apply one focused mutation and inspect the same target again.
4. Check the Unity Console for import, binding, or serialization errors.
5. Enter Play Mode only when runtime behavior is part of acceptance.

See [Runtime Tools](runtime.md) for live Animator and physics inspection,
[Playtest](../features/playtest.md) for deterministic behavioral checks, and the
[Generated Tool Schema](../tools-schema/index.md) for exhaustive signatures and
defaults.
