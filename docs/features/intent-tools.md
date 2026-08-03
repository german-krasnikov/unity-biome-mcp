# Intent Tools Guide

Convert natural language requests into scene operations automatically.

## Overview

Intent tools use Haiku (fast LLM) to translate plain English into structured operations. Costs ~$0.0001 per intent call.

**Note:** Intent tools are configurable via **MCP → Settings → LLM Sampling** in the Unity Editor. They are `direct_only` tools and cannot be used in `batch` commands.

| Tool | Purpose | Example |
|------|---------|---------|
| `do` | Create/modify scene objects | "create a cube at position 5,0,0" |
| `ask` | Query scene state (read-only) | "how many enemies are alive?" |
| `animator_intent` | Setup animation controller | "movement with idle/walk transition" |
| `vfx_intent` | Configure particle effects | "fire explosion effect" |
| `ui_intent` | Create UI layouts | "health bar at top-left" |

## do(intent, dry_run=False)

Create or modify scene objects from natural language.

```python
# Execute immediately
await do("create a cube at position 5,0,0 named Player")

# Preview plan without executing
plan = await do("create a cube", dry_run=True)
# → Returns DSL plan; review before running
```

**How it works:**
1. Describe what you want in plain English
2. Haiku generates batch commands (create_object, set_property, etc.)
3. Commands are validated and executed
4. Returns success or error details

**Errors:** Returns "INVALID PLAN" if validation fails (e.g., component type not found, path doesn't exist).

## ask(question)

Query scene state without modifying anything.

```python
await ask("how many enemies have health > 50?")
await ask("what components does the Player have?")
await ask("where is the nearest collectible?")
```

**Returns:** Text answer summarized by Haiku if needed. Read-only; no mutations allowed.

**Rejection:** Mutating questions like "add health to player" are rejected — use `do()` instead.

## animator_intent(target, intent, dry_run=False) {#animator_intent}

Setup Animator Controller parameters, states, and transitions from natural language.

```python
await animator_intent("Player", "movement with idle/walk transition at speed 0.1")

# Preview first
await animator_intent("Enemy", "attack animation", dry_run=True)
```

**Creates:**
- Float parameters (e.g., Speed)
- Animation states with clip assignments
- Transitions with conditions (Speed > 0.1, etc.)
- Default state

**Supported transitions:** Speed comparisons (`>`, `<`), bool checks, custom conditions.

## vfx_intent(target, intent, kind="auto", dry_run=False) {#vfx_intent}

Configure particle systems with presets or custom properties.

```python
# Use built-in preset
await vfx_intent("Explosion", "fire_explosion")

# Use preset by name
await vfx_intent("Enemy", intent="magic spell effect", kind="magic_burst")

# Custom properties (Haiku generates)
await vfx_intent("Dust", "small dust particles, slow fade")
```

**5 Built-in Presets (no cost):**

| Preset | Effect | Use Case |
|--------|--------|----------|
| `fire_explosion` | Red→orange, size 0.5–2.0, speed 3–8 m/s | Explosions, impacts |
| `magic_burst` | Purple, size 0.2–0.8, speed 2–5 m/s | Magic spells |
| `dissolve` | White, size 0.1–0.3, fade out | Fade-to-black effects |
| `glow_outline` | Yellow, size 0.05–0.15, static position | Highlights, auras |
| `smoke_trail` | Gray, size 0.3–1.0, fade out | Smoke, dust clouds |

**Customization:** If preset name not recognized, Haiku designs custom particle properties (colorOverLifetime, sizeOverLifetime, speed curves).

## ui_intent(intent, parent=None, template=None, dry_run=False) {#ui_intent}

Create UI hierarchies (Canvas, panels, buttons) from natural language.

```python
# Use built-in template
await ui_intent("create a health bar", template="hud")

# Custom UI
await ui_intent("create a pause menu with Resume/Quit buttons")

# Specific parent
await ui_intent("add score display", parent="UI/HUD")
```

**4 Built-in Templates (no cost):**

| Template | Layout | Contains |
|----------|--------|----------|
| `hud` | Top corners | HealthBar (top-left), Score (top-right) |
| `menu` | Centered | Play, Settings, Quit buttons (vertical) |
| `dialog` | Center | Message text, OK button |
| `grid` | Grid layout | Configurable cell grid |

**Anchors supported:** `top-left`, `top-right`, `bottom-left`, `bottom-right`, `center`, `stretch`, `top-center`, `bottom-center`.

## budget_status()

Check daily Haiku usage.

```python
status = await budget_status()
# → sess=$0.0450/0.50 (9%) day=$0.1200 skipped=animator_intent:3 vfx_intent:1
```

Budget tracking is enabled by default with a `$0.50` session cap and a `$5.00` daily cap. Override them with `UNITY_MCP_HAIKU_BUDGET` and `UNITY_MCP_HAIKU_DAY_CAP`, or disable tracking with `UNITY_MCP_BUDGET=0`.

## Common Workflow

```python
# 1. Ask about scene
await ask("what enemies are in the level?")

# 2. Do bulk creation
await do("create 3 platforms in a line, spaced 5 units apart")

# 3. Setup animation
await animator_intent("Player", "walk/run with speed control")

# 4. Add effects
await vfx_intent("Explosion", "fire_explosion")

# 5. Create UI
await ui_intent("health bar at top-left", template="hud")
```

## Cost Estimate

| Operation | Cost | Notes |
|-----------|------|-------|
| do() | ~$0.0001 | 1 Haiku call |
| ask() | ~$0.0001 | 1 Haiku call (if summary needed) |
| animator_intent() | ~$0.0001 | 1 Haiku call |
| vfx_intent() preset | Free | Uses built-in (magic_burst, fire_explosion, etc.) |
| vfx_intent() custom | ~$0.0001 | 1 Haiku call |
| ui_intent() template | Free | Uses built-in (hud, menu, dialog, grid) |
| ui_intent() custom | ~$0.0001 | 1 Haiku call |

---

**See also:** [Batch Reference](../tools/batch.md) for batch operations, [Getting Started](../getting-started/index.md) for scene hierarchy.
