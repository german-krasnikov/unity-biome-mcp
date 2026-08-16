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
- Any of `var `, `new `, `GameObject`, `//`, `;`, or `using ` →
  **kind=csharp**
- Otherwise → **kind=batch**. This is a deliberately small heuristic, not a
  language parser.

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

## save_template(name: str, code: str)

**Write.** Store scene creation template (`.claude/templates/{name}.cs`).

```python
await save_template(
    name="spawn_room",
    code="""
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

**Returns:** The underlying `execute_code` result, or a missing-template error.

---

## list_templates()

**Read-only.** Show all saved templates.

```python
await list_templates()
# →
# boss_arena
# spawn_room
```

---

## fingerprint()

**Read-only.** Compute hash of current scene state (hierarchy + component values).

```python
fp = await fingerprint()
# → "fp:1A2B3C4D"
# Can be compared later for regression detection
```

**Use case:** Quick "did scene change?" check without full snapshot.

---

## scene_diff()

**Read-only.** Compare the current scene hierarchy with the previous `scene_diff()` snapshot. The first call stores a snapshot; later calls report added and removed hierarchy lines.

```python
await scene_diff()
# → SNAPSHOT SAVED (first call — no diff yet)

# ... perform edits ...

diff = await scene_diff()
# →
# DIFF: +1 -1
# + Enemy ...
# - OldSpawn ...
```

**Output:** Added/removed hierarchy lines or an unchanged result. Use `fingerprint()` separately for a compact state hash.

---

## get_changes(clear: bool = True)

**Read tool with a consuming default.** Retrieve logged editor events since the
last clear (hierarchy, undo/redo, play/stop, selection, and explicit MCP
mutations).

```python
# Get all changes, clear log
changes = await get_changes(clear=True)
# →
# 10:12:03 HIERARCHY_CHANGED
# 10:12:04 SELECTED:Enemy
# 10:12:05 PLAY_MODE:EnteredPlayMode

# Next call returns NO_CHANGES (log cleared)
```

**Event types tracked:**
- `HIERARCHY_CHANGED`: object created, deleted, or reparented
- `SELECTED:<name>`: active GameObject selection changed
- `PLAY_MODE:<state>`: Unity play-mode state changed
- `UNDO_REDO`: Undo or Redo executed
- `SCENE_OPENED:<name>`: scene loaded
- `SCENE_SAVED:<name>`: scene saved
- `MCP_<COMMAND>` / `MCP_BATCH_<COMMAND>`: a routed mutation was recorded

**clear=False:**
```python
changes = await get_changes(clear=False)  # Read but don't clear log
```

---

## save_session()

**Write.** Snapshot current scene hierarchy to `.claude/session-context.json` for cold-start recovery.

```python
await save_session()
# → Session saved to <project>/.claude/session-context.json
```

**File format:**
```text
<Unix timestamp>
=== hierarchy ===
<get_hierarchy(summary=True) output>
```

**Use case:** Recover after MCP disconnect or PC crash.

---

## load_session()

**Read-only.** Load the previous session context and show it beside the current hierarchy.

```python
await load_session()
# →
# Previous (2024-06-24 10:30:45):
# Scene (Root)
#   Player (active)
# 
# Current:
# Scene (Root)
#   Player (inactive)
#   Enemy (active)
```

**Returns:** Two sections: the timestamped previous snapshot and the current hierarchy. It does not compute diff markers.

**If no previous session:** "No previous session found."

---

## screenshot_baseline(name: str = "default", width: int = 640, height: int = 480, camera: str | None = None)

**Write.** Save screenshot as baseline for visual regression (`.claude/baselines/{name}.png`).

```python
await screenshot_baseline("menu_screen", width=1280, height=720, camera="UICamera")
# → Baseline saved: <project>/.claude/baselines/menu_screen.png
```

**Multi-baseline workflow:**
```python
await screenshot_baseline("tutorial_start", camera="MainCamera")
await screenshot_baseline("tutorial_end", camera="MainCamera")
# → Create 2 golden reference images
```

---

## screenshot_compare(name: str = "default", width: int = 640, height: int = 480, camera: str | None = None, mode: str = "auto", question: str | None = None)

**Write / optional sampling.** After finding the saved baseline, capture a fresh
project-local PNG and compare the two images. The capture remains under
`ScreenShots/`, so the tool is write-classified even in `pixel` mode. Semantic
modes degrade to pixel evidence when sampling is unavailable.

```python
# Auto mode: pixel diff first, escalate to structural on changes
await screenshot_compare("menu_screen", mode="auto")

# Pixel-only (fast, free)
await screenshot_compare("menu_screen", mode="pixel")

# Structural semantic diff through the configured sampling profile
await screenshot_compare("menu_screen", mode="structural")

# Specialized semantic prompts
await screenshot_compare("menu_screen", mode="ui_layout")
await screenshot_compare("menu_screen", mode="animation")
await screenshot_compare("menu_screen", mode="color")
await screenshot_compare("menu_screen", mode="position")
await screenshot_compare("menu_screen", mode="regression")

# Custom question
await screenshot_compare(
    "game_scene",
    mode="targeted",
    question="Did the enemy AI health bar move?"
)
```

**Modes:**

| Mode | Purpose |
|------|---------|
| auto | Pixel diff first; escalate to structural sampling only when needed |
| pixel | Local pixel comparison; no sampling |
| structural | General two-image semantic comparison |
| targeted | Custom semantic question; requires `question` |
| ui_layout | Specialized alignment and spacing prompt |
| animation | Specialized motion/timing prompt |
| color | Specialized palette prompt |
| position | Specialized object-placement prompt |
| regression | PASS/FAIL-oriented visual-regression prompt |

**Caching:** Semantic results are cached in memory by image and prompt hashes
for five minutes (bounded to 64 entries). Pixel analysis still runs before the
semantic-cache lookup.

**Returns:** A pixel verdict, a combined pixel/semantic result, or an explicit
degraded result when semantic sampling is unavailable.

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
    code='new GameObject("CombatArena");'
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
# Log: "HIERARCHY_CHANGED", "SELECTED:...", "MCP_...", etc.
```

---

**See also:** `AI/tools-reference.md` (SYSTEM ownership), `AI/testing.md`
(repository evidence),
`unity-plugin/ClientSkills/skills/unity-mcp-operations/references/session-and-reuse.md`,
and `unity-plugin/ClientSkills/skills/unity-testing-verification/SKILL.md`.
