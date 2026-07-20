# Unity Animation (MCP)

Keyframe animation on GameObjects via AnimationClip. For state machines use `animator`, for cinematics use `timeline`.

**Gated:** `discover_tools category="MEDIA"`.

## Actions

```
animation action="get" path="/Character"                    # list clips & keys
animation action="create" path="/Door" clip_name="DoorOpen" property="localEulerAngles.y" keys="t:0 v:0; t:1 v:90"
animation action="edit" path="/Door" clip="DoorOpen" property="localEulerAngles.y" keys="t:0 v:0; t:0.5 v:45; t:1 v:90"
animation action="add_key" path="/Door" clip="DoorOpen" property="localEulerAngles.y" keys="t:0.75 v:60"   # alias of edit
animation action="remove_key" path="/Door" clip="DoorOpen" property="localEulerAngles.y" keys="t:0.75"     # time only
animation action="remove_curve" path="/Door" clip="DoorOpen" property="localEulerAngles.y"
animation action="set_keys" path="/Door" clip="DoorOpen" property="localEulerAngles.y" keys="t:0 v:0; t:1 v:90"  # replaces whole curve
animation action="set_loop" path="/Door" clip="DoorOpen" keys="true"
animation action="set_wrap" path="/Door" clip="DoorOpen" keys="pingpong"          # loop|once|pingpong|clamp
animation action="set_framerate" path="/Door" clip="DoorOpen" keys="30"
animation action="get_clip_path" path="/Door" clip="DoorOpen"                     # -> asset path
animation action="preview" path="/Door" clip="DoorOpen" time=0.5
animation action="create" path="/Lamp" clip_name="Flicker" property="intensity" keys="t:0 v:1; t:0.5 v:0; t:1 v:1" component_type="Light"
```

## Events

```
animation action="add_event" path="/Character" clip="Attack" time=0.4 function_name="OnHit" int_param=10
animation action="add_event" path="/Character" clip="Attack" time=0.9 function_name="PlaySound" string_param="swing.wav"
animation action="get_events" path="/Character" clip="Attack"
animation action="remove_event" path="/Character" clip="Attack" time=0.4
```

## Parameters

| Param | Description |
|-------|-------------|
| `action` | `get` / `create` / `edit` (=`add_key`) / `remove_key` / `remove_curve` / `set_keys` / `set_loop` / `set_wrap` / `set_framerate` / `preview` / `get_clip_path` / `get_events` / `add_event` / `remove_event` |
| `path` | Path to GameObject |
| `clip_name` | Name for new clip (`create`) |
| `clip` | Name of existing clip (all actions except `create`) |
| `property` | `localPosition.y`, `localEulerAngles.y`, `intensity`, etc. |
| `keys` | `t:0 v:0; t:1 v:90` (float) or `t:0 v:(0,0,0); t:1 v:(0,2,0)` (Vector3). Reused as scalar payload: `set_loop` (`true`/anything-but-`false`), `set_wrap` (`loop`\|`once`\|`pingpong`\|`clamp`), `set_framerate` (`30`), `remove_key` (`t:0.5`, time only) |
| `time` | Scrub time in seconds (`preview`); event time in seconds (`add_event`/`remove_event`) |
| `component_type` | Component to animate (default: Transform). Light, Camera, Rigidbody |
| `binding_path` | Sub-object path for EditorCurveBinding, e.g. `Head/Jaw` (default `""` = root) |
| `tangent` | Keyframe tangent mode: `auto` (default) / `smooth` / `linear` / `constant` |
| `function_name` | Method name invoked by `add_event` |
| `int_param` / `float_param` / `string_param` | Event payload for `add_event` |

## Batch

```
# Multi-keyframe in one call (saves 3 round-trips)
batch(commands="""
animation action=add_key path=/Character clip=Walk property=localPosition.x time=0 value=0
animation action=add_key path=/Character clip=Walk property=localPosition.x time=0.5 value=1
animation action=add_key path=/Character clip=Walk property=localPosition.x time=1 value=2
""")
```

## Verification

```
animation action="get" path="/Door"    # confirm clip exists, keys correct
animation action="get_events" path="/Character" clip="Attack"    # confirm event added
```

---

# C# Editor API Reference

## Reading Animation Data

```csharp
var animator = go.GetComponent<Animator>();
var clips = animator.runtimeAnimatorController.animationClips;

EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
Keyframe[] keys = curve.keys;

AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
bool isLooping = settings.loopTime;
```

## Creating AnimationClip Programmatically

```csharp
var clip = new AnimationClip { name = "MyClip", frameRate = 60 };
var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 2f), new Keyframe(1f, 0f));
AnimationUtility.SetEditorCurve(clip, binding, curve);
AssetDatabase.CreateAsset(clip, "Assets/Animations/MyClip.anim");
AssetDatabase.SaveAssets();
```

## Property Name Mapping

| Friendly Name | Internal Binding |
|--------------|-----------------|
| localPosition.x | m_LocalPosition.x |
| localRotation.x / localEulerAngles.x | localEulerAnglesRaw.x |
| localScale.x | m_LocalScale.x |
| m_IsActive | m_IsActive |

## Preview in Edit Mode (AnimationMode)

```csharp
AnimationMode.StartAnimationMode();
AnimationMode.BeginSampling();
AnimationMode.SampleAnimationClip(gameObject, clip, time);
AnimationMode.EndSampling();
bool active = AnimationMode.InAnimationMode();
AnimationMode.StopAnimationMode();  // restores original pose
```

## Undo Support

```csharp
Undo.RecordObject(clip, "Edit Animation Keyframe");
AnimationUtility.SetEditorCurve(clip, binding, curve);
EditorUtility.SetDirty(clip);  // always mark dirty after edits
```

## Common Pitfalls

| Issue | Solution |
|-------|----------|
| SetCurve doesn't work on non-legacy | Use AnimationUtility.SetEditorCurve instead |
| Clip not persisted | Must call AssetDatabase.CreateAsset + SaveAssets |
| Quaternion rotation issues | Use localEulerAnglesRaw instead of localRotation |
| Animator needs controller | Create AnimatorController asset, add clip to it |

## Notes

**`create` is atomic (v0.92):** if any step after saving the `.anim` asset fails (adding Animator component, creating `.controller`), the orphaned assets are auto-deleted via `AssetDatabase.DeleteAsset`. No cleanup needed — either the full setup succeeds or nothing is left behind.

## Sources

- [AnimationClip](https://docs.unity3d.com/ScriptReference/AnimationClip.html)
- [AnimationUtility](https://docs.unity3d.com/ScriptReference/AnimationUtility.html)
- [EditorCurveBinding](https://docs.unity3d.com/ScriptReference/EditorCurveBinding.html)
- [AnimationMode](https://docs.unity3d.com/ScriptReference/AnimationMode.html)
- [AnimationCurve](https://docs.unity3d.com/ScriptReference/AnimationCurve.html)
