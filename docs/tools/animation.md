# Animation Tools

Manage AnimationClips, Timeline sequences, Animator Controllers, and Particle Systems. Use these tools for keyframe animation, cinematic authoring, state machine setup, and particle effects.

## animation

Read or author keyframe animation on AnimationClips. Use this for per-object AnimationClips, not for Animator state machine control (use `animator` for that).

**Parameters:**
- `action` (string) — "get" | "create" | "edit" | "preview" | "add_event" | "remove_event" | "get_events" | "set_wrap" | "set_framerate" | "get_clip_path"
- `path` (string) — Scene path to target GameObject
- `clip` (string, optional) — AnimationClip name (required for edit/preview)
- `clip_name` (string, optional) — New clip name (used with create)
- `property` (string, optional) — Property to animate (e.g., "localPosition.x", "scale.y", "m_Color.a")
- `keys` (string, optional) — Keyframe data: `t:0 v:(0,0,0); t:1 v:(0,2,0)` (time in seconds, value). Also used for set_wrap ("loop"|"once"|"pingpong"|"clamp") and set_framerate ("30")
- `time` (float, optional) — Time position for preview/add_event (seconds)
- `component_type` (string, optional) — Unity component to animate (default: Transform). Examples: Light, Camera, Rigidbody
- `binding_path` (string, optional) — Sub-object path for EditorCurveBinding (e.g., "Head/Jaw"). Default "" = root
- `tangent` (string, optional) — Tangent mode for keyframes: "auto" (default) | "smooth" | "linear" | "constant"
- `function_name` (string, optional) — Method name for add_event
- `int_param` (int, optional) — Integer parameter for add_event
- `float_param` (float, optional) — Float parameter for add_event
- `string_param` (string, optional) — String parameter for add_event

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| get | List clips and keys | path | `animation("get", path="Player")` |
| create | New AnimationClip on object | path, clip_name | `animation("create", path="Player", clip_name="Walk")` |
| edit | Add/replace keyframes | path, clip, property, keys | `animation("edit", path="Player", clip="Walk", property="localPosition.x", keys="t:0 v:0; t:1 v:5")` |
| preview | Scrub to time | path, clip, time | `animation("preview", path="Player", clip="Walk", time=0.5)` |
| add_event | Add animation event | path, clip, time, function_name | `animation("add_event", path="Player", clip="Walk", time=0.5, function_name="OnStep")` |
| remove_event | Remove animation event | path, clip, time | `animation("remove_event", path="Player", clip="Walk", time=0.5)` |
| get_events | List animation events | path, clip | `animation("get_events", path="Player", clip="Walk")` |
| set_wrap | Set wrap mode | path, clip, keys | `animation("set_wrap", path="Player", clip="Walk", keys="loop")` |
| set_framerate | Set clip framerate | path, clip, keys | `animation("set_framerate", path="Player", clip="Walk", keys="30")` |
| get_clip_path | Get asset path of clip | path, clip | `animation("get_clip_path", path="Player", clip="Walk")` |

**Example:**

```python
# Create animation clip
await animation("create", path="Player", clip_name="Jump")

# Add keyframes (0→1 second, position 0→10 on X-axis)
await animation("edit", path="Player", clip="Jump", property="localPosition.x",
                keys="t:0 v:0; t:1 v:10")

# Preview at 0.5 seconds
await animation("preview", path="Player", clip="Jump", time=0.5)
```

---

## timeline

Manage Unity Timeline (PlayableDirector / TimelineAsset) for multi-track cinematic sequences. Use for mixing animation, audio, activation, and custom tracks.

**Parameters:**
- `path` (string) — Scene path to GameObject with PlayableDirector
- `action` (string) — "get" | "create" | "add_track" | "remove_track" | "add_clip" | "remove_clip" | "set_binding" | "set_timing" | "mute" | "unmute" | "lock" | "unlock" | "rename_track" | "reorder_track" | "duplicate_clip" | "add_marker" | "remove_marker" | "set_track_offset" | "set_duration" | "add_sub_track" | "set_clip_in" | "get_bindings" | "preview"
- `track` (string, optional) — Track name for targeting specific track
- `track_type` (string, optional) — Track type: "Animation" | "Audio" | "Activation" | "Signal" | "Control" | "Group"
- `clip` (string, optional) — AnimationClip name
- `binding` (string, optional) — Scene object path to bind track
- `start` (float, optional) — Clip start time (seconds)
- `duration` (float, optional) — Clip duration (seconds)
- `blend_in` (float, optional) — Blend-in duration (seconds)
- `blend_out` (float, optional) — Blend-out duration (seconds)
- `asset_path` (string, optional) — TimelineAsset path (Assets/...)
- `director_path` (string, optional) — PlayableDirector path
- `tracks` (string, optional) — Track list (get action)
- `time` (float, optional) — Scrub to time (seconds)
- `name` (string, optional) — New name for rename_track, or marker name
- `clip_in` (float, optional) — Clip-in time for set_clip_in
- `index` (int, optional) — Target position for reorder_track
- `offset` (float, optional) — Time shift for duplicate_clip
- `value` (string, optional) — Offset mode for set_track_offset: "auto" | "transform" | "scene"

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| get | Inspect tracks and clips | `timeline(path="Cutscene", action="get")` |
| create | New TimelineAsset | `timeline(path="Cutscene", action="create", asset_path="Assets/Cinematics/Intro.playable")` |
| add_track | Create track | `timeline(path="Cutscene", action="add_track", track="AnimTrack1", track_type="Animation")` |
| remove_track | Delete track | `timeline(path="Cutscene", action="remove_track", track="AnimTrack1")` |
| add_clip | Place clip on track | `timeline(path="Cutscene", action="add_clip", track="AnimTrack1", clip="Walk", start=0, duration=2)` |
| remove_clip | Remove clip from track | `timeline(path="Cutscene", action="remove_clip", track="AnimTrack1", clip="Walk")` |
| set_binding | Bind track to object | `timeline(path="Cutscene", action="set_binding", track="AnimTrack1", binding="Player")` |
| get_bindings | List all track bindings | `timeline(path="Cutscene", action="get_bindings")` |
| set_timing | Set clip start/duration | `timeline(path="Cutscene", action="set_timing", track="AnimTrack1", clip="Walk", start=1, duration=3)` |
| mute | Mute track | `timeline(path="Cutscene", action="mute", track="AnimTrack1")` |
| unmute | Unmute track | `timeline(path="Cutscene", action="unmute", track="AnimTrack1")` |
| lock | Lock track | `timeline(path="Cutscene", action="lock", track="AnimTrack1")` |
| unlock | Unlock track | `timeline(path="Cutscene", action="unlock", track="AnimTrack1")` |
| rename_track | Rename track | `timeline(path="Cutscene", action="rename_track", track="AnimTrack1", name="PlayerAnim")` |
| reorder_track | Move track to position | `timeline(path="Cutscene", action="reorder_track", track="AnimTrack1", index=0)` |
| duplicate_clip | Copy clip with offset | `timeline(path="Cutscene", action="duplicate_clip", track="AnimTrack1", clip="Walk", offset=2.0)` |
| add_marker | Add timeline marker | `timeline(path="Cutscene", action="add_marker", time=1.0, name="CuePoint")` |
| remove_marker | Remove marker | `timeline(path="Cutscene", action="remove_marker", name="CuePoint")` |
| set_track_offset | Set track offset mode | `timeline(path="Cutscene", action="set_track_offset", track="AnimTrack1", value="auto")` |
| set_duration | Set timeline duration | `timeline(path="Cutscene", action="set_duration", duration=10.0)` |
| add_sub_track | Add sub-track to group | `timeline(path="Cutscene", action="add_sub_track", track="Group1", track_type="Animation")` |
| set_clip_in | Set clip-in time | `timeline(path="Cutscene", action="set_clip_in", track="AnimTrack1", clip="Walk", clip_in=0.5)` |
| preview | Scrub to time | `timeline(path="Cutscene", action="preview", time=1.5)` |

**Example:**

```python
# Create new timeline
await timeline(path="Director", action="create", asset_path="Assets/Intro.playable")

# Add animation track
await timeline(path="Director", action="add_track", track="PlayerAnim", track_type="Animation")

# Bind track to Player
await timeline(path="Director", action="set_binding", track="PlayerAnim", binding="Player")

# Place animation clip
await timeline(path="Director", action="add_clip", track="PlayerAnim", 
               clip="Walk", start=0, duration=2)

# Preview at 1 second
await timeline(path="Director", action="preview", time=1.0)
```

---

## animator

Manage Animator Controller state machines. Add states, parameters, and transitions.

**Parameters:**
- `action` (string) — "get" | "add_param" | "add_state" | "add_transition" | "set_default" | "remove" | "add_blend_tree" | "edit_blend_tree" | "get_blend_tree" | "add_layer" | "remove_layer" | "rename_layer" | "set_layer_weight" | "set_layer_blending" | "set_state_speed" | "update_transition" | "set_avatar" | "rename_state" | "rename_param"
- `path` (string) — Scene path to GameObject with Animator
- `state` (string, optional) — State name
- `states` (string, optional) — State definitions: "Idle:Idle.anim; Walk:Walk.anim; Run"
- `params` (string, optional) — Parameters: "Speed:float:0; Jump:trigger; IsGrounded:bool:false"
- `source` (string, optional) — Transition source state (use "*" for AnyState)
- `target` (string, optional) — Transition target state
- `conditions` (string, optional) — Transition conditions: "Speed>0.1; IsGrounded"
- `duration` (float, optional) — Transition duration (seconds)
- `exit_time` (float, optional) — Exit time threshold (0-1)
- `has_exit_time` (bool, optional) — Whether transition has exit time
- `type` (string, optional) — Parameter type (float|bool|int|trigger)
- `name` (string, optional) — Parameter or state name
- `blend_type` (string, optional) — Blend tree type: "1d" | "2d_simple" | "2d_freeform" | "2d_cartesian" | "direct"
- `param` (string, optional) — Blend parameter (auto-created as float if missing)
- `param_y` (string, optional) — Second blend parameter (for 2D blend trees)
- `children` (string, optional) — Blend tree children: "(1D) Idle:0; Walk:0.5; Run:1" or "(2D) Idle:0,0; Walk:0,1"
- `edit_action` (string, optional) — Blend tree edit: "add_child" | "remove_child" | "set_thresholds" | "set_param" | "set_type"
- `layer` (int or string, optional) — Layer index for add_state/add_transition/set_default, or name/index for CRUD ops
- `weight` (float, optional) — Default weight for add_layer/set_layer_weight (0.0-1.0)
- `blending` (string, optional) — Layer blending: "Override" | "Additive" (for add_layer/set_layer_blending)
- `value` (string, optional) — Speed multiplier for set_state_speed
- `avatar_path` (string, optional) — Asset path for set_avatar

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| get | Inspect states, params, transitions | `animator("get", path="Player")` |
| add_param | Create parameter | `animator("add_param", path="Player", type="float", name="Speed")` |
| add_state | Create state | `animator("add_state", path="Player", state="Walk")` |
| add_transition | Create transition | `animator("add_transition", path="Player", source="Idle", target="Walk", conditions="Speed>0.1", duration=0.2)` |
| set_default | Set default state | `animator("set_default", path="Player", state="Idle")` |
| remove | Remove state/param/transition | `animator("remove", path="Player", state="Walk")` |
| add_blend_tree | Create blend tree state | `animator("add_blend_tree", path="Player", state="Locomotion", blend_type="1d", param="Speed", children="Idle:0; Walk:0.5; Run:1")` |
| edit_blend_tree | Modify existing blend tree | `animator("edit_blend_tree", path="Player", state="Locomotion", edit_action="add_child", children="Sprint:2")` |
| get_blend_tree | Inspect blend tree | `animator("get_blend_tree", path="Player", state="Locomotion")` |
| add_layer | Add animator layer | `animator("add_layer", path="Player", name="UpperBody", weight=1.0, blending="Override")` |
| remove_layer | Remove layer | `animator("remove_layer", path="Player", layer="UpperBody")` |
| rename_layer | Rename layer | `animator("rename_layer", path="Player", layer="UpperBody", name="Arms")` |
| set_layer_weight | Set layer weight | `animator("set_layer_weight", path="Player", layer=1, weight=0.5)` |
| set_layer_blending | Set layer blend mode | `animator("set_layer_blending", path="Player", layer=1, blending="Additive")` |
| set_state_speed | Set state speed multiplier | `animator("set_state_speed", path="Player", state="Walk", value="1.5")` |
| update_transition | Update existing transition | `animator("update_transition", path="Player", source="Idle", target="Walk", duration=0.3)` |
| set_avatar | Set animator avatar | `animator("set_avatar", path="Player", avatar_path="Assets/Models/PlayerAvatar.asset")` |
| rename_state | Rename state | `animator("rename_state", path="Player", state="Walk", name="Walking")` |
| rename_param | Rename parameter | `animator("rename_param", path="Player", name="Speed", value="MoveSpeed")` |

**Example:**

```python
# Create parameters
await animator("add_param", path="Player", type="float", name="Speed")
await animator("add_param", path="Player", type="bool", name="IsGrounded")

# Add states
await animator("add_state", path="Player", state="Idle")
await animator("add_state", path="Player", state="Walk")
await animator("add_state", path="Player", state="Run")

# Add transitions
await animator("add_transition", path="Player", source="Idle", target="Walk",
              conditions="Speed>0.1", duration=0.2)
await animator("add_transition", path="Player", source="Walk", target="Run",
              conditions="Speed>2.0", duration=0.3)

# Set default state
await animator("set_default", path="Player", state="Idle")
```

---

## particle

Create and configure Particle Systems with preset or custom modules.

**Parameters:**
- `action` (string) — "get" | "create" | "set" | "apply" | "play" | "stop" | "pause"
- `path` (string) — Scene path to target GameObject
- `name` (string, optional) — Particle system name
- `module` (string, optional) — Module: "main" | "emission" | "shape" | "colorOverLifetime" | "sizeOverLifetime" | "velocityOverLifetime" | "noise" | "renderer" | "trails" | "collision" | "rotationOverLifetime"
- `prop` (string, optional) — Module property name
- `value` (string, optional) — Property value
- `preset` (string, optional) — Preset type: "fire" | "smoke" | "sparks" | "rain" | "snow" | "explosion" | "magic" | "dust" | "blood" | "trail"

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| get | Inspect particle system | `particle("get", path="Effects/Fire")` |
| create | New ParticleSystem | `particle("create", path="Enemy", name="Explosion", preset="explosion")` |
| set | Change module property | `particle("set", path="Effects/Fire", module="emission", prop="rateOverTime", value="50")` |
| apply | Apply a preset to an existing system | `particle("apply", path="Effects/Fire", preset="fire")` |
| play | Start playback | `particle("play", path="Effects/Fire")` |
| stop | Stop playback | `particle("stop", path="Effects/Fire")` |
| pause | Pause playback | `particle("pause", path="Effects/Fire")` |

**Example:**

```python
# Create particle system with preset
await particle("create", path="Effects", name="ExplosionFX", preset="explosion")

# Customize emission
await particle("set", path="Effects/ExplosionFX", module="emission", 
              prop="rateOverTime", value="100")

# Customize renderer
await particle("set", path="Effects/ExplosionFX", module="renderer",
              prop="maxParticleSize", value="10")

# Reset the system to the named preset when needed
await particle("apply", path="Effects/ExplosionFX", preset="explosion")
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Create looping animation | animation → editor(play) | `await animation("create", path="Player", clip_name="Idle"); await animation("edit", path="Player", clip="Idle", property="localPosition.x", keys="t:0 v:0; t:1 v:0")` |
| Build animator state machine | animator (add_param → add_state → add_transition) | Add all parameters first, then states, then transitions |
| Create cinematic sequence | timeline (add_track → set_binding → add_clip → preview) | Use multiple tracks for layered sequences |
| Particle effect with animation | particle(preset) → timeline(add_clip) | Add particle system to Timeline for synchronized effects |

---

**See also:** [Scene Tools](scene.md) for playback control, [Runtime Tools](runtime.md) for Play Mode state inspection.
