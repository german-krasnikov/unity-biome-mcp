# Intent Tools

NL-to-DSL meta-tools that convert natural language to structured domain-specific languages (DSLs), then batch-execute them.

## Architecture

```
Intent Input
    ↓
Haiku sampling (prompt + context)
    ↓
Parse DSL (regex + state machine)
    ↓
Validate (symbol resolution, range checks)
    ↓
Build batch commands
    ↓
Execute via batch(commands)
```

**Cost Model:** Each intent tool makes 1 Haiku call (~500 tokens input, 200-300 tokens output). Budget tracked by `budget_status()`.

## do(intent, dry_run=False)

**Purpose:** NL intent → Haiku plan → batch DSL → execute.

**Flow:**
1. Fetch scene hierarchy summary (get_hierarchy with summary=true)
2. Haiku generates batch DSL (command-per-line: create_object, set_property, etc.)
3. Validate plan: all /paths exist, component types valid
4. Execute via batch(commands) or return dry-run plan

**Dry-run:** Returns plan text; caller must manually execute if needed.

**Errors:** Returns "INVALID PLAN" if validation fails. Haiku unavailable → "ERROR: Haiku unavailable".

**Example:**
```python
await do("create a cube at position 5,0,0 named Player", dry_run=True)
# → "DRY RUN plan:\ncreate_object type=GameObject name=Player parent=/\nset_property ..."
```

## ask(question) [read-only]

**Purpose:** Answer scene questions via tool routing + optional Haiku summarization.

**Flow:**
1. Route question to deterministic tool plans (find by component type, query field, etc.)
2. Execute plan (multi-tool snapshot)
3. If results complex → Haiku summarize(question, results)
4. Return answer

**Rejection:** Mutating questions → "ask is read-only".

**No context match:** "ask is for scene questions only" → fallback to other tools.

## animator_intent(target, intent, dry_run=False)

**Purpose:** Setup Animator Controller (parameters, states, transitions) via DSL.

**DSL:**
```
PARAM Speed float 0                     # name type default
STATE Idle Idle.anim
STATE Walk Walk.anim
DEFAULT Idle
TRANS Idle -> Walk dur=0.15 if Speed>0.1
TRANS Walk -> Idle dur=0.15 if Speed<0.1
```

**Validation:** All state names declared; conditions reference declared params.

**Batch Output:** add_param, add_state, set_default, add_transition commands.

**Example:**
```python
await animator_intent("/Player", "movement with idle/walk transition at speed 0.1", dry_run=True)
```

## vfx_intent(target, intent, kind="auto", dry_run=False)

**Purpose:** Configure particle system properties (color, size, speed, modules).

**5 Built-in Presets** (bypass Haiku):
- `fire_explosion`: red→orange, 0.5-2.0 size, 3-8 speed, colorOverLifetime + sizeOverLifetime
- `magic_burst`: purple, 0.2-0.8 size, 2-5 speed
- `dissolve`: white, 0.1-0.3 size, sizeOverLifetime
- `glow_outline`: yellow, 0.05-0.15 size, static position
- `smoke_trail`: gray, 0.3-1.0 size, colorOverLifetime + sizeOverLifetime

**DSL** (if not preset):
```
SET startColor = #FF2200
SET startSize = 0.5,1.0
MODULE colorOverLifetime ENABLED
GRADIENT color = #FF8800@0;#FF2200@1
```

**Batch Output:** particle set/module/gradient commands.

**Kind:** "particle" only (shader DSL not yet implemented).

## ui_intent(intent, parent=None, template=None, dry_run=False)

**Purpose:** Create UI hierarchies (Canvas, panels, buttons, layouts).

**4 Built-in Templates** (bypass Haiku):
- `hud`: HealthBar + Score readout (top-left, top-right anchors)
- `menu`: Play/Settings/Quit buttons (vertical layout, centered)
- `dialog`: Message text + OK button (center anchor)
- `grid`: Grid of cells (GridLayoutGroup)

**DSL**:
```
canvas Canvas
  panel HUD anchor=stretch
    image HealthBar anchor=top-left pos=20,-20 size=200,30 color=#c33
    text Score anchor=top-right pos=-20,-20 size=120,40 text="0" fontSize=24
```

**Anchors:** top-left, top-right, bottom-left, bottom-right, center, stretch, top-center, bottom-center.

**Batch Output:** create_ui (per element) + set_rect (anchor/pos/size) + manage_component for layouts + set_property for spacing.

**Example:**
```python
await ui_intent("create a health bar at top-left", template="hud")
```

## budget_status() [read-only]

**Purpose:** Report session Haiku cost snapshot.

**Output Format** (text):
```
sess=$0.0450/0.50 (9%) day=$0.0450 skipped=animator_intent:3 vfx_intent:1
```

**Enabled by default.** Set `UNITY_MCP_BUDGET=0` to disable it; set `UNITY_MCP_BUDGET=1` to enable it explicitly.

**Default caps:** `$0.50` per session and `$5.00` per day. Override them with `UNITY_MCP_HAIKU_BUDGET` and `UNITY_MCP_HAIKU_DAY_CAP`.

**Disabled response:** `"budget tracking disabled (set UNITY_MCP_BUDGET=1)"`.

## Common Patterns

| Pattern | Tool | Why |
|---------|------|-----|
| "create enemy at pos" | do() | Batch-able; Haiku plan likely correct |
| "how many health items?" | ask() | Read-only; Haiku summarize optional |
| "setup movement" | animator_intent() | Specialized DSL; condition validation |
| "fire effect" | vfx_intent() | 5 presets; most intents covered |
| "create menu UI" | ui_intent() | 4 templates; avoid Haiku fallback if possible |

## Failure Modes

| Error | Root Cause | Fix |
|-------|-----------|-----|
| "INVALID PLAN: Undeclared state" | animator_intent DSL missing STATE | Manually set_property or retry intent |
| "ROSLYN UNAVAILABLE" | Roslyn DLLs not loaded (Phase B) | Use read + grep (Phase A fallback) |
| "[Haiku timeout]" | UNITY_MCP_VISUAL_VERIFY=0 | Set env var; retry with sampling enabled |
| "ask is read-only" | Intent detected as mutation | Use do(), set_property, or manage_component |

---

**Related:** `AI/batch.md` (batch DSL syntax), `AI/architecture.md` (Haiku planning internals), `.claude/skills/token-optimization.md` (cost minimization).
