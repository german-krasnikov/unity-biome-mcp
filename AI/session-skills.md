# Session Skills, Templates & Snapshots

Persistent reusable code library, scene templates, session recovery, visual regression testing, change tracking.

## save_skill(name: str, description: str, code: str)

**Write.** Store reusable C# code or batch commands (`.claude/skills/learned/{name}.json`).

```python
await save_skill(
    name="damage_enemy",
    description="Damage closest enemy by 10 HP",
    code="""
    var enemies = FindObjectsOfType<Enemy>();
    if (enemies.Length > 0) {
        var closest = enemies[0];
        closest.TakeDamage(10);
    }
    """
)
# → Skill saved: damage_enemy — Damage closest enemy by 10 HP
```

**Auto-detection:**
- C# keywords detected: `var`, `new`, `GameObject`, `//`, `;`, `using` → **kind=csharp**
- Batch keywords: `echo`, `if`, `for`, etc. → **kind=batch**

**Metadata stored:**
- `name`, `description`, `code`, `kind`, `created` (timestamp), `used_count` (incremented on use)

**Use case:** Capture complex editing sequences for replay.

---

## use_skill(name: str, params: str | None = None)

**Write.** Execute saved skill with optional parameter substitution.

```python
# No params
await use_skill("damage_enemy")

# With params (key=value CSV)
await save_skill(
    name="damage_by_amount",
    description="Damage enemy by N HP",
    code="target.TakeDamage(${amount});"
)
await use_skill("damage_by_amount", params="amount=25")
# → Substitutes ${amount} → 25 in code before execution
```

**Parameter syntax:**
- Define: `${key}` in skill code
- Pass: `key1=value1,key2=value2`
- Whitespace: trimmed around `=` and `,`

**Returns:** Result of underlying `execute_code()` or `batch()` call.

---

## list_skills()

**Read-only.** Show all saved skills with descriptions and usage counts.

```python
await list_skills()
# → 
# damage_enemy [csharp]: Damage closest enemy by 10 HP (used 3x)
# heal_player [csharp]: Restore 20 HP to Player (used 1x)
# setup_level [batch]: Spawn platforms and enemies (used 12x)
```

**Output format:** `{name} [{kind}]: {description} (used {count}x)`

---

## save_template(name: str, description: str, template_code: str)

**Write.** Store scene creation template (`.claude/templates/{name}.cs`).

```python
await save_template(
    name="spawn_room",
    description="Spawn dungeon room with platform pattern",
    template_code="""
    var room = new GameObject("Room");
    for (int x = 0; x < 3; x++) {
        var platform = Instantiate(platformPrefab, new Vector3(x * 2, 0, 0), Quaternion.identity);
        platform.transform.parent = room.transform;
    }
    return room;
    """
)
```

---

## apply_template(name: str, params: str | None = None)

**Write.** Instantiate scene from template with parameter substitution.

```python
# Basic instantiation
await apply_template("spawn_room")

# With parameters
await apply_template(
    "spawn_room",
    params="platform_count=5,spacing=3.0,height=2.0"
)
# → Substitutes ${platform_count}, ${spacing}, ${height} in template
```

**Returns:** Scene hierarchy summary or error.

---

## list_templates()

**Read-only.** Show all saved templates.

```python
await list_templates()
# →
# spawn_room: Spawn dungeon room with platform pattern
# boss_arena: Large circular arena with traps
```

---

## fingerprint()

**Read-only.** Compute hash of current scene state (hierarchy + component values).

```python
fp = await fingerprint()
# → "scene_v0_hash=abc123def456..."
# Can be compared later for regression detection
```

**Use case:** Quick "did scene change?" check without full snapshot.

---

## scene_diff(fp1: str, fp2: str)

**Read-only.** Compare two fingerprints; report differences.

```python
fp_before = await fingerprint()
# ... perform edits ...
fp_after = await fingerprint()

diff = await scene_diff(fp_before, fp_after)
# →
# Changes:
#   Player/Health: 100 → 50
#   Enemy count: 3 → 5
#   Door/isOpen: false → true
```

**Output:** Structured list of property deltas or "IDENTICAL".

---

## get_changes(clear: bool = True)

**Write-idempotent.** Retrieve logged editor events since last call (hierarchy, undo/redo, play/stop, selection).

```python
# Get all changes, clear log
changes = await get_changes(clear=True)
# →
# [timestamp] HierarchyChanged: Player spawned
# [timestamp] SelectionChanged: Enemy selected
# [timestamp] PlayModeChanged: entered PlayMode

# Next call returns NO_CHANGES (log cleared)
```

**Event types tracked:**
- `HierarchyChanged`: Object created/deleted/reparented
- `SelectionChanged`: User selected object
- `PlayModeChanged`: Entered/exited Play Mode
- `UndoRedoPerformed`: Undo/Redo executed
- `SceneOpened`: Scene loaded
- `SceneSaved`: Scene saved

**clear=False:**
```python
changes = await get_changes(clear=False)  # Read but don't clear log
```

---

## save_session()

**Write.** Snapshot current scene hierarchy to `.claude/session-context.json` for cold-start recovery.

```python
await save_session()
# → Session saved to /Users/german/Work/python/unity-kiss-mcp/.claude/session-context.json
```

**File format:**
```json
1234567890.0
=== hierarchy ===
Scene (Root)
  Player (active)
    Collider (component)
    Rigidbody (component)
  Enemy (active)
    ...
```

**Use case:** Recover after MCP disconnect or PC crash.

---

## load_session()

**Read-only.** Load previous session context; show diff vs current hierarchy.

```python
await load_session()
# →
# Previous (2024-06-24 10:30:45):
# Scene (Root)
#   Player (active)
# 
# Current:
# Scene (Root)
#   Player (inactive)  ← changed
#   Enemy (active)     ← added
```

**Returns:** 2-part output: previous snapshot + current state + diff markers.

**If no previous session:** "No previous session found."

---

## screenshot_baseline(name: str = "default", width: int = 640, height: int = 480, camera: str | None = None)

**Write.** Save screenshot as baseline for visual regression (`.claude/baselines/{name}.png`).

```python
await screenshot_baseline("menu_screen", width=1280, height=720, camera="UICamera")
# → Baseline saved: /Users/german/.../baselines/menu_screen.png
```

**Multi-baseline workflow:**
```python
await screenshot_baseline("tutorial_start", camera="MainCamera")
await screenshot_baseline("tutorial_end", camera="MainCamera")
# → Create 2 golden reference images
```

---

## screenshot_compare(name: str = "default", width: int = 640, height: int = 480, camera: str | None = None, mode: str = "auto", question: str | None = None)

**Read-only / LLM.** Compare current screenshot with saved baseline; detect visual regressions.

```python
# Auto mode: pixel diff first, escalate to structural on changes
await screenshot_compare("menu_screen", mode="auto")

# Pixel-only (fast, free)
await screenshot_compare("menu_screen", mode="pixel")

# Structural diff (Haiku model, ~$0.005)
await screenshot_compare("menu_screen", mode="structural")

# Specialized modes (very low cost)
await screenshot_compare("menu_screen", mode="ui_layout")
await screenshot_compare("menu_screen", mode="animation")
await screenshot_compare("menu_screen", mode="color")
await screenshot_compare("menu_screen", mode="position")

# Custom question
await screenshot_compare(
    "game_scene",
    mode="targeted",
    question="Did the enemy AI health bar move?"
)
```

**Modes:**

| Mode | Cost | Purpose |
|------|------|---------|
| auto | $0-0.005 | Pixel diff first; escalate to structural if diff found |
| pixel | $0 | Free: exact RGB comparison (fails on minor antialiasing) |
| structural | ~$0.005 | Haiku full-image analysis (general, catches layout shifts) |
| targeted | ~$0.001 | Haiku with custom question (pinpoint specific changes) |
| ui_layout | ~$0.001 | Specialized: button positions, alignment, spacing |
| animation | ~$0.001 | Specialized: motion, timing changes |
| color | ~$0.001 | Specialized: color palette shifts |
| position | ~$0.001 | Specialized: object placement (e.g., enemy spawn point) |

**Caching:** Results cached by image hash; same baseline + current = cached result (no LLM cost).

**Returns:** "IDENTICAL" or structured diff (pixel deltas + LLM analysis).

---

## Integration Patterns

### Skill → Template → Session Workflow

```python
# Save reusable skill
await save_skill(
    name="setup_combat",
    description="Spawn player + enemy + setup combat state",
    code="..."
)

# Save template using that skill
await save_template(
    name="combat_arena",
    description="Combat ready scene",
    template_code="use_skill('setup_combat')"
)

# Session recovery
await load_session()  # shows previous state
await apply_template("combat_arena")  # recreate known good state
```

### Regression Testing with Baselines

```python
# Golden reference (run once)
await screenshot_baseline("boss_arena")

# Every subsequent run: compare
result = await screenshot_compare("boss_arena", mode="auto")
# IDENTICAL → green
# DIFFERENT → red (render bug? layout shift?)
```

### Change Tracking for Playtest

```python
# Before: capture editor state
before = await get_changes(clear=True)
await run_playtest(script="...")  # run scenario
# After: see what changed
after = await get_changes(clear=False)
# Log: "HierarchyChanged", "SelectionChanged", etc.
```

---

**See also:** AI/tools-reference.md (SYSTEM category — session tools), `.claude/skills/playmode-verification.md` (regression patterns), CLAUDE.md § Verification Gates.
