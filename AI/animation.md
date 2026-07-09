# Feature: Animation Support (Phase 7)

## Overview

1 consolidated MCP tool `animation` with 4 actions (get, create, edit, preview) for reading, creating, editing, and previewing animations in Unity Editor. Integrates with AnimationSerializer (reads clips/curves) and AnimationHelper (creates/edits/previews), following the pattern: Python tool → bridge.send(cmd) → CommandRouter → handler → text response.

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     animation tool (1)          CommandRouter (1 case)
                     4 actions                   ExecAnimationConsolidated (switch)
                                                         │
                                              ┌──────────┴──────────┐
                                              │                     │
                                    AnimationSerializer     AnimationHelper
                                    (read: clips → text)    (write/preview)
```

## Implementation Notes

### Data Storage
- Animation clips live in `.anim` asset files (created in `Assets/Animations/`)
- Curves stored in AnimationClip via EditorCurveBinding
- Keyframes hold (time, value) with interpolation info

### Constraints
- Editor-only: AnimationMode API (sampling, preview)
- Property names map to internal bindings: `localPosition` → `m_LocalPosition.x/.y/.z`
- Vector3 properties require 3 float curves (one per axis)
- Keyframe output limited to 50 per curve (shows `,...+N more` if exceeded)

### Edge Cases
- No Animator/Animation component: return clear error
- AnimationMode already active: check `InAnimationMode()` before starting
- Legacy Animation vs Animator: support both (check Animator first, then Animation)
- Invalid component_type (v0.57+): "Component type not found: {typeName}" — check ResolveComponentType logic (searches UnityEngine.*, custom assemblies)
- Custom component not found: verify type name matches Assembly-CSharp definition exactly (e.g., "Health", not "MyGame.Health" for Assembly-CSharp types)

### Sub-Actions Flattening (Bug Fix)
- Phase 16 fix: `animation action=edit` was broken — sub-actions (add_key, remove_key, etc.) were passed as separate args but re-extracted, losing the actual sub-action
- **Solution:** Sub-actions now routed as top-level cases in CommandRouter.ExecAnimationConsolidated() switch statement
- **New pattern:** `action` param contains the sub-action directly (add_key|remove_key|remove_curve|set_keys|set_loop)
- See `unity-plugin/Editor/CommandRouter.cs` ExecAnimationConsolidated() for implementation

## Code Locations

- Python tool: `server/src/unity_mcp/tools/animation.py` (animation, timeline, animator, particle)
- Serializer: `unity-plugin/Editor/AnimationSerializer.cs`
- Helper: `unity-plugin/Editor/AnimationHelper.cs`
- Commands: `unity-plugin/Editor/CommandRouter.cs` (ExecAnimationConsolidated case)
- Python tests: `server/tests/test_server.py` + `test_server_edge_cases.py` + `test_server_animation.py` (13 tests for M11–M14)
- C# tests: `unity-test-project/Assets/Tests/Editor/MCPPluginTests.cs`

## MCP Tools

### `animation` (single consolidated tool)

**Parameters:** `action` (required), `path` (required), `clip` (optional), `clip_name` (optional), `property` (optional), `keys` (optional), `time` (optional), `component_type` (optional, v0.57+)

#### action=get — List clips or show details
```
# List all clips on object
animation(action="get", path="/Player")
→ Animator: Idle, Walk, Jump
  ---
  Idle | 1.0s | 3 curves
  Walk | 0.8s | 6 curves | loop

# Show clip detail (value@time format, comma-separated)
animation(action="get", path="/Player", clip="Walk")
→ clip: Walk | 0.8s | loop
  ---
  m_LocalPosition.x: 0@0,1.5@0.4,0@0.8

# Sample at time
animation(action="get", path="/Player", clip="Walk", time=0.4)
→ sample: Walk @ 0.40s
  ---
  m_LocalPosition.x: 1.5
  m_LocalPosition.y: 0.5
```

#### action=create — Create new AnimationClip
**Params:** `path`, `clip_name`, `property` (default="localPosition"), `keys` (keyframe string), `component_type` (optional, default="Transform")

Creates new AnimationClip with curves, saves to `Assets/Animations/{clip_name}.anim`, attaches to object's Animator.

Key format: `t:<time> v:<value>` separated by `;`
- Vector3: `t:0 v:(0,0,0); t:1 v:(0,2,0)`
- Float: `t:0 v:0; t:0.5 v:1; t:1 v:0`

**component_type** (v0.57+): Specifies the component containing the property. Defaults to Transform (for localPosition, rotation, scale). Other examples:
- `Light` — animate intensity, color, range
- `Camera` — animate fieldOfView, nearClipPlane, farClipPlane
- `Rigidbody` — animate mass, drag, velocity
- Custom components — full type name or short name if in Assembly-CSharp
- Error handling: non-existent component type returns "Component type not found" error

#### action=edit (or sub-action directly: add_key|remove_key|remove_curve|set_keys|set_loop|set_wrap|set_framerate|get_clip_path)
**Params:** `path`, `clip`, `action`, `property` (optional), `keys` (optional), `component_type` (optional, default="Transform")

Modify existing clip. Sub-actions passed as `action` value:
- `add_key` — insert keyframes (property + keys required); Color properties accept hex (`#FF0000`) as value
- `remove_key` — delete keyframe at time (property + `t:0.5` required)
- `remove_curve` — delete entire curve (property required)
- `set_keys` — replace all keyframes (property + keys required)
- `set_loop` — toggle clip looping (keys="false" to disable, anything else to enable)
- `set_wrap` — set clip wrap mode: `loop`, `once`, `pingpong`, `clamp` (clip required)
- `set_framerate` — set clip sample rate in frames per second (value required)
- `get_clip_path` — return asset path for the specified clip (clip required)

**component_type** (v0.57+): Required when editing non-Transform properties (e.g., Light.intensity). Must match the component type used when creating the clip. See create section for full list of examples.

#### action=preview — Preview in Edit Mode
**Params:** `path`, `clip`, `time` (optional, default=0.0)

The `action` value is one of: `sample` (default on C# side), `start`, `stop`.
- `sample` — pose object at time, return sampled values
- `start` — enter AnimationMode
- `stop` — exit AnimationMode, restore original pose

#### Example: Animate Custom Component (v0.57+)
```
# Animate Light intensity from 0.5 to 2.0 over 2 seconds
animation(
  action="create",
  path="/MyLight",
  clip_name="LightFade",
  property="intensity",
  component_type="Light",
  keys="t:0 v:0.5; t:2 v:2.0"
)
→ created: LightFade | 2.0s | 1 curves | saved: Assets/Animations/LightFade.anim

# Edit the clip: add keyframe at 1 second (midpoint)
animation(
  action="add_key",
  path="/MyLight",
  clip="LightFade",
  component_type="Light",
  property="intensity",
  keys="t:1 v:1.25"
)
→ edited: LightFade | add_key intensity
```

**Non-existent component error:**
```
animation(
  action="create",
  path="/Player",
  clip_name="BadClip",
  component_type="NonExistentComponent",
  property="someField"
)
→ [error] Component type not found: NonExistentComponent
```

## TDD Scenarios

### Red Phase
1. **test_get_animation_calls_bridge**: path only → sends correct command
2. **test_get_animation_with_clip**: with clip name → sends clip arg
3. **test_get_animation_with_time**: with time → sends time arg
4. **test_get_animation_error**: error response → formatted error string
5. **test_create_animation_calls_bridge**: creates clip → sends all args
6. **test_edit_animation_calls_bridge**: edits clip → sends correct action
7. **test_preview_animation_calls_bridge**: preview with time → sends correct args
8. **test_preview_animation_defaults**: action/time defaults applied
9. **test_animation_component_type_arg** (v0.57+): component_type param passed to bridge
10. **test_animation_component_type_defaults** (v0.57+): component_type=None by default

C# tests (8 total):
1. **CreateAnimation_CreatesClipWithKeyframes**: create → get_animation → verify clip listed
2. **GetAnimation_ListsAllClips**: list clips → verify names in output
3. **GetAnimation_ClipDetail_ShowsCurvesAndKeyframes**: clip detail → verify curves + keyframes
4. **EditAnimation_AddKey_InsertsKeyframe**: add_key → get_animation → verify keyframe added
5. **EditAnimation_RemoveCurve_DeletesCurve**: remove_curve → verify count reduced
6. **PreviewAnimation_Sample_ReturnsSampledValues**: sample at time → verify interpolated values
7. **CreateAnimation_CustomComponent_Light** (v0.57+): create with Light.intensity → verify binding uses Light type
8. **EditAnimation_CustomComponent_InvalidType_ReturnsError** (v0.57+): non-existent component_type → "Component type not found" error

### Green Phase
- Python: 1 tool function (`animation`) with 8 params (v0.57: +1 component_type) + 10 tests (v0.57: +2)
- C#: AnimationSerializer (Serialize, SerializeClipList, SerializeClipDetail, SerializeClipAtTime)
- C#: AnimationHelper (CreateClip, EditClip, Preview, SetCurvesFromKeys + v0.57: ResolveComponentType helper)
- CommandRouter: 1 registered command (`animation`) → ExecAnimationConsolidated switch → 4 Exec methods
- v0.57 addition: ResolveComponentType(typeName) resolves UnityEngine.* types, custom Assembly-CSharp types, or throws "Component type not found"

## Review Checklist

- [x] Security: no path traversal (GameObject.Find validates), API calls safe
- [x] Performance: keyframe limit 50/curve prevents token bloat, no unnecessary sampling
- [x] Token efficiency: text format ~5x smaller than JSON equivalent
- [x] Edge cases: no Animator → error, AnimationMode → checked before start, vector expansion → handled

## Related

- Tool: `animator_intent` — NL intent tool for animation (See `AI/intent-tools.md`)
- Skill: `.claude/skills/csharp-unity.md` (Editor API)
- Knowledge: `AI/architecture.md`
