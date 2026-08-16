# Feature: Animator Controller Management

## Overview

The consolidated `animator` MCP tool manages Unity Animator Controller parameters,
states, transitions, blend trees, layers, defaults, speed, and avatar assignment.
`AnimatorControllerSerializer` owns reads; `AnimatorControllerHelper` owns mutations.

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     animator tool              CommandRouter.MediaHandlers
                                                ExecAnimatorConsolidated
                                                         │
                                              ┌──────────┴──────────┐
                                              │                     │
                                    AnimatorController      AnimatorController
                                    Serializer (read)       Helper (write/CRUD)
```

## Tool Parameters

**Python signature:** `animator(action, path, state?, states?, params?, source?, target?, conditions?, duration?, exit_time?, has_exit_time?, type?, name?, blend_type?, param?, param_y?, children?, edit_action?, layer?, weight?, blending?, value?, avatar_path?)`

Use `resolve_tool_schema(name="animator")` when generating calls dynamically;
the source signature is authoritative.

## Tool Actions

| Action | Description | Key params |
|--------|-------------|------------|
| `get` | Read controller structure (params, states, transitions). Pass `state` to get single state detail. | `state` (optional) |
| `add_param` | Add parameters: `"Speed:float:0; Jump:trigger"` | `params` |
| `add_state` | Add states: `"Idle:Idle.anim; Walk"` | `states` |
| `add_transition` | Add transition with conditions, duration, exit_time | `source`, `target`, `conditions`, `duration`, `exit_time`, `has_exit_time` |
| `set_default` | Set default state | `state` |
| `remove` | Remove param/state/transition | `type` (param\|state\|transition), `name`, `source`, `target` |
| `add_blend_tree` | Create a blend-tree state | `state`, `blend_type`, `param`, `param_y`, `children` |
| `edit_blend_tree` | Edit children, parameters, or type | `state`, `edit_action`, blend-tree fields |
| `get_blend_tree` | Read blend-tree details | `state` |
| `add_layer` | Add a controller layer | `name`, `weight`, `blending` |
| `remove_layer` | Remove a layer by name or index | `layer` |
| `rename_layer` | Rename a layer | `layer`, `name` |
| `set_layer_weight` | Set the default layer weight | `layer`, `weight` |
| `set_layer_blending` | Set `Override` or `Additive` blending | `layer`, `blending` |
| `set_state_speed` | Set speed multiplier for a state | `state`, `value` (float) |
| `update_transition` | Modify existing transition params | `source`, `target`, `duration`, `exit_time`, `has_exit_time` |
| `set_avatar` | Assign an Avatar asset to the target Animator | `avatar_path` |
| `rename_state` | Rename an existing state | `state` (old), `name` (new) |
| `rename_param` | Rename an existing parameter | `param` (old), `name` (new) |

`add_state`, `add_transition`, `set_default`, and `update_transition` use a
zero-based `layer` index (default `0`). Layer CRUD actions accept a layer name
or index where noted. `remove` operates on the base layer for state/transition
removal; rename and blend-tree detail helpers search across layers.

## Condition Format

```
"Speed>0.1"    → Greater
"Speed<0.1"    → Less
"Type=2"       → Equals (also "Type==2")
"State!=0"     → NotEqual
"IsGrounded"   → If (bool/trigger true)
"!IsGrounded"  → IfNot (bool false)
"Param==true"  → If (bool shorthand)
"Param==false" → IfNot (bool shorthand)
```

Multiple conditions: `"Speed>0.1; IsGrounded"` (AND logic, `;` separator).
Output format uses ` & ` separator between conditions.

## Key Implementation Details

- Mutation helpers that call `GetOrCreateController(path)` auto-create an
  Animator and controller when missing; read/detail/rename actions may require
  an existing controller
- Controller saved to `Assets/Animations/{objectName}.controller`
- `source="*"` maps to `stateMachine.AddAnyStateTransition()` with `canTransitionToSelf=false`
- States auto-positioned at (300, i*80, 0) for clean layout
- Duplicate params/states are skipped with `(exists)` marker
- All mutations use `Undo.RecordObject()` for undo support
- `AssetDatabase.SaveAssets()` after each write operation
- Clip lookup: exact path → Assets/Animations/ → FindAssets search

## Files

| File | Role |
|------|------|
| `unity-plugin/Editor/AnimatorControllerSerializer.cs` | Controller reads and text serialization |
| `unity-plugin/Editor/AnimatorControllerHelper.cs` | Controller mutations |
| `unity-plugin/Editor/CommandRouter.MediaHandlers.cs` | C# action dispatch |
| `server/src/unity_mcp/tools/animation.py` | Public `animator` wrapper |
| `server/src/unity_mcp/tools/animator_intent_tool.py` | Intent DSL parsing and batch construction |
| `server/tests/test_server_animator.py` | Python contract tests |
| `unity-plugin/Editor/Tests/SerializerTests.cs` and `BlendTreeTests.cs` | C# serializer and blend-tree tests |

## Text Output Format

### Overview (action=get, no state param)
```
AnimatorController: Player | 1 layer | 3 params | 4 states
---
params:
  Speed : float = 0
  IsGrounded : bool = true
  Jump : trigger
---
states [Base Layer]:
  * Idle | Idle.anim | 1x
  Walk | Walk.anim | 1x
---
transitions:
  Idle → Walk | Speed>0.1 | 0.15s
  Walk → Idle | Speed<0.1 | 0.15s
  [Any] → Jump | Jump & IsGrounded | 0.1s
```

Note: transitions also show `exit:X` when `hasExitTime` is true. States show `tag:X` if tagged.

### State detail (action=get, state="Idle")
```
state: Idle | Idle | speed:1
---
transitions:
  → Walk | Speed>0.1 | 0.15s
```

## `animator_intent` Tool

Separate NL-to-DSL tool that converts natural-language intent into `animator`
batch commands through the configured sampling service.

**Python signature:** `animator_intent(target, intent, dry_run=False)`

**DSL keywords:**
```
PARAM <name> <type> <default>    (types: float|int|bool|trigger)
STATE <name> <clip.anim>
DEFAULT <state>
TRANS <src> -> <dst> dur=<float> [if <Param><op><value>]
```

Pipeline: intent → configured sampling backend generates DSL → parse and validate
(including undeclared state/parameter checks) → build batch lines → execute through
`batch`. `dry_run=True` returns the plan without executing it.

## Related

- [`AI/intent-tools.md`](intent-tools.md) — shared intent pipeline and failure semantics
- `unity-plugin/ClientSkills/skills/unity-animation/SKILL.md` — consumer workflow
- [`AI/testing.md`](testing.md) — current verification policy
