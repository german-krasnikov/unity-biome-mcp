---
name: unity-session
description: Unity Biome MCP session tools — fingerprint, scene_diff, get_changes, save/load_session, screenshot_baseline/compare. Change detection, visual regression, session recovery.
user-invocable: false
---

# Unity Session & Change Detection (MCP)

## Tool Tiers

| Tool | Tier | Notes |
|------|------|-------|
| mcp_status | TIER1 SYSTEM | Always visible |
| reconnect_unity | TIER1 SYSTEM | Always visible |
| fingerprint | Tier2 SYSTEM | `discover_tools(category="SYSTEM")` |
| get_changes | Tier2 SYSTEM | `discover_tools(category="SYSTEM")` |
| save_session / load_session | Tier2 SYSTEM | `discover_tools(category="SYSTEM")` |
| save_skill / use_skill / list_skills | Tier2 SYSTEM | `discover_tools(category="SYSTEM")` |
| save_template / apply_template / list_templates | Tier2 SYSTEM | `discover_tools(category="SYSTEM")` |
| scene_diff | Tier2 SCENE | `discover_tools(category="SCENE")` |
| screenshot_baseline / screenshot_compare | Tier2 MEDIA | `discover_tools(category="MEDIA")` |

**direct_only** (cannot use inside `batch`): `mcp_status`, `discover_tools`, `list_skills`, `list_templates`, `screenshot_baseline`, `screenshot_compare`.

## discover_tools (v0.92)

Canonical categories: **SCENE, COMPONENTS, ASSETS, MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM**

```
discover_tools category="SCENE"                    # tools in SCENE category
discover_tools category="SCENE" structured=True    # includes per-tool surface/mutability info
discover_tools                                     # all enabled tools
```

- `include_legacy=False` — default; legacy category aliases excluded
- `structured=True` — returns per-tool `surface` (edit_mode/play_mode/any) and `mutates` (bool)

## Core Session Tools

### mcp_status (TIER1)
```
mcp_status    # compact scene/compile/play-mode/alias status snapshot
```

### reconnect_unity (TIER1)
```
reconnect_unity    # reconnect TCP socket (auto-discovers port)
```

### fingerprint (Tier2 SYSTEM, read-only, ~10s timeout)
Scene state hash. Returns `fp:XXXXXXXX`. ~5 tokens.
```
fingerprint                          # whole scene, depth=3
fingerprint path="/Player" depth=2   # subtree only
```

### scene_diff (Tier2 SCENE, read-only)
Compare two fingerprints; report property deltas.
```
fp1 = fingerprint()      # → fp:A1B2C3D4
# ... mutations ...
fp2 = fingerprint()      # → fp:E5F6G7H8
scene_diff fp1=<hash1> fp2=<hash2>   # → property deltas or "IDENTICAL"
```

### get_changes (Tier2 SYSTEM)
Editor events since last call: hierarchy, undo/redo, play mode, scene, selection.
```
get_changes              # returns events, clears buffer
get_changes clear=false  # peek without clearing
```

### save_session / load_session (Tier2 SYSTEM)
Cold-start recovery. Saves hierarchy to `.claude/session-context.json`.
```
save_session   # snapshot to disk
load_session   # shows previous vs current hierarchy diff
```

### save_skill / use_skill / list_skills (Tier2 SYSTEM)
`list_skills` is **direct_only**.
```
save_skill name="spawn_enemy" description="..." code="..."
use_skill name="spawn_enemy" params="health=100"
list_skills    # direct_only — not inside batch
```

### save_template / apply_template / list_templates (Tier2 SYSTEM)
`list_templates` is **direct_only**. `${key}` placeholder substitution.
```
save_template name="level_setup" code="GameObject go = new GameObject(\"${name}\"); ..."
apply_template name="level_setup" params="name=Enemy"
list_templates    # direct_only — not inside batch
```

### screenshot_baseline / screenshot_compare (Tier2 MEDIA, direct_only)
Both **direct_only** — cannot be used inside `batch`.
```
screenshot_baseline name="ui_main" width=1280 height=720
screenshot_baseline name="scene" camera="multi_view"
screenshot_compare name="ui_main"
screenshot_compare name="ui_main" mode="targeted" question="Did buttons move?"
```
Modes: `auto` (free→escalate), `pixel` (free), `structural|targeted|ui_layout|animation|color|position` (~$0.005).

## Workflows

### Flying Probe (3+ mutations)
```
discover_tools category="SYSTEM"
fingerprint                        # → fp:A1B2C3D4
# ... mutations ...
fingerprint                        # → fp:E5F6G7H8 (changed = ok)
fingerprint path="/OtherObject"    # verify neighbors unchanged
```
Unchanged after mutation = something wrong. Changed on untouched object = side-effect.

### Visual Regression
```
discover_tools category="MEDIA"
screenshot_baseline name="before" camera="multi_view"
# ... changes ...
screenshot_compare name="before"
screenshot_compare name="before" mode="targeted" question="Are buttons visible?"
```

### Session Recovery
```
discover_tools category="SYSTEM"
save_session                 # end of session
load_session                 # next session — see what changed
```

### File-Based Playtests
```
run_playtest path="Playtests/farm_pipeline.playtest"    # load DSL from disk
run_playtest_suite paths="Playtests/*.playtest"         # multiple files (direct_only)
```

## When to Use

| Situation | Tool |
|-----------|------|
| 3+ mutations | `fingerprint` before/after |
| Risky op (delete, reparent) | `fingerprint` + `scene_diff` |
| UI/visual changes | `screenshot_baseline` + `screenshot_compare` |
| Long task (5+ steps) | `get_changes` between steps |
| Session end / resume | `save_session` / `load_session` |
| Server status check | `mcp_status` |
| Playtest regression | `run_playtest(path=...)` |

## Anti-patterns

- `fingerprint` after every single op — use `get_component` for 1 op
- `screenshot_compare` without prior `screenshot_baseline` — error
- `scene_diff` with one fingerprint — takes `fp1` + `fp2` as explicit params
- `screenshot_baseline` inside `batch` — direct_only, will fail
- `list_skills` / `list_templates` inside `batch` — direct_only, will fail
- `mode="structural"` when pixel is enough — use `auto` (default, escalates only if needed)
- `get_changes` twice — first clears buffer, second returns NO_CHANGES; use `clear=false` to peek
