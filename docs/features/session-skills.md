# Session Skills & Templates Guide

Reuse automation scripts and scene templates across sessions.

## Overview

- **Skills:** Parameterizable C# or batch code snippets you save and replay
- **Templates:** Pre-built scene layouts you instantiate with parameters
- **Sessions:** Snapshots of scene state for recovery after disconnect

## Skills: Save & Reuse

### Saving a Skill

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
# → Skill saved: damage_enemy
```

**Auto-detection:**
- C# keywords (`var`, `new`, `GameObject`) → kind=csharp
- Batch keywords (`echo`, `if`) → kind=batch

### Using a Skill

```python
# No parameters
await use_skill("damage_enemy")

# With parameters
await save_skill(
    name="damage_by_amount",
    description="Damage closest enemy",
    code="FindObjectsOfType<Enemy>()[0].TakeDamage(${amount});"
)
await use_skill("damage_by_amount", params="amount=25")
```

**Parameter syntax:**
- Define: `${key}` in code
- Pass: `key1=value1,key2=value2`

### List All Skills

```python
await list_skills()
# → damage_enemy [csharp]: Damage closest enemy (used 3x)
#   heal_player [csharp]: Restore 20 HP (used 1x)
#   spawn_platforms [batch]: Create platform grid (used 12x)
```

## Templates: Scene Layouts

### Creating a Template

```python
await save_template(
    name="spawn_room",
    code="""
    var room = new GameObject("Room");
    for (int x = 0; x < 3; x++) {
        var platform = Instantiate(platformPrefab, 
                                   new Vector3(x * 2, 0, 0), 
                                   Quaternion.identity);
        platform.transform.parent = room.transform;
    }
    return room;
    """
)
```

### Applying a Template

```python
# Basic
await apply_template("spawn_room")

# With parameters
await apply_template(
    "spawn_room",
    params="platform_count=5,spacing=3.0,height=2.0"
)
```

### List Templates

```python
await list_templates()
# → spawn_room: Spawn dungeon room with platforms
#   boss_arena: Large circular arena
```

## Session Management

### Save Current State

```python
await save_session()
# → Session saved to .claude/session-context.json
```

**Saved info:**
- Hierarchy snapshot
- Active GameObjects
- Component list
- Timestamp

### Load Previous Session

```python
await load_session()
# → Previous (2024-06-24 10:30:45):
#   Scene (Root)
#     Player (active)
#   
#   Current:
#   Scene (Root)
#     Player (inactive)  ← changed
```

**Use case:** Recover after MCP disconnect or PC crash.

## Tracking Changes

### Get Editor Events Since Last Call

```python
changes = await get_changes(clear=True)
# → [timestamp] HierarchyChanged: Player spawned
#   [timestamp] SelectionChanged: Enemy selected
#   [timestamp] PlayModeChanged: entered PlayMode

# Next call returns NO_CHANGES (log cleared)
```

**Event types:**
- `HierarchyChanged` — Object created/deleted/reparented
- `SelectionChanged` — User selected object
- `PlayModeChanged` — Entered/exited Play Mode
- `UndoRedoPerformed` — Undo/Redo executed
- `SceneOpened` — Scene loaded
- `SceneSaved` — Scene saved

### Keep Log Without Clearing

```python
changes = await get_changes(clear=False)  # Read but don't clear
```

## Fingerprint & Regression Testing

### Get Scene Hash

```python
fp = await fingerprint()
# → "fp:A1B2C3D4"
```

**Use case:** Quick "did scene change?" check without full snapshot.

### Compare Two States

```python
# First call saves a snapshot of the current scene state
await scene_diff()
# → "Snapshot saved."

# ... perform edits ...

# Second call compares current state against the saved snapshot
diff = await scene_diff()
# → Changes:
#   Player/Health: 100 → 50
#   Enemy count: 3 → 5
```

`scene_diff()` takes no arguments. The first call saves an internal snapshot; subsequent calls diff against it.

## Complete Workflow

```python
# 1. Save skill for future use
await save_skill(
    name="setup_combat",
    description="Spawn player + enemy",
    code="/* setup code */"
)

# 2. Save template
await save_template(
    name="combat_arena",
    code="use_skill('setup_combat')"
)

# 3. Snapshot current state
await fingerprint()  # quick hash check
await scene_diff()   # save snapshot for later comparison
await save_session()

# 4. Later: Recover state
await load_session()
diff = await scene_diff()  # compare against saved snapshot

# 5. Recreate from template
await apply_template("combat_arena")
```

## Visual Regression Testing

### Create Baseline

```python
await screenshot_baseline("menu_screen", width=1280, height=720)
# → Baseline saved: /Users/german/.../baselines/menu_screen.png
```

### Compare Current vs Baseline

```python
result = await screenshot_compare("menu_screen", mode="auto")
# → IDENTICAL (pixel diff only)
# or
# → DIFFERENT: button layout shifted 10px left
```

**Modes:**

| Mode | Cost | Purpose |
|------|------|---------|
| pixel | Free | Fast exact RGB match |
| auto | $0–0.005 | Pixel first; escalate to LLM if diff |
| structural | ~$0.005 | Full-image LLM analysis |
| targeted | ~$0.001 | Custom question (pinpoint changes) |

## Storage Locations

| Item | Location | Survives Reload? |
|------|----------|-----------------|
| Skills | `.claude/skills/learned/` | Yes |
| Templates | `.claude/templates/` | Yes |
| Session | `.claude/session-context.json` | Yes |
| Baselines | `.claude/baselines/` | Yes |

---

**See also:** [Getting Started](../getting-started/index.md) for initial setup, [Batch Reference](../tools/batch.md) for efficient batching.
