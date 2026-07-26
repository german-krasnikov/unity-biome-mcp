# UI Tools

Create and configure Unity UI (Canvas, Panels, Buttons, Text, Images) with simple declarative syntax. Use these tools for HUD, menus, dialogs, and layout authoring.

## create_ui

Create UI elements with smart defaults. Automatically creates Canvas if needed.

**Parameters:**
- `type` (string) — "Canvas" | "Panel" | "Button" | "Text" | "Image"
- `name` (string, optional) — Element name
- `parent` (string, optional) — Parent path (default: new Canvas root)
- `anchor` (string, optional) — Anchor preset: "stretch" | "center" | "top-left" | "top-right" | "bottom-left" | "bottom-right"
- `pos` (string, optional) — Position (x,y)
- `size` (string, optional) — Size (width,height)
- `pivot` (string, optional) — Pivot point (x,y)
- `color` (string, optional) — Color (hex #RRGGBB or named)
- `text` (string, optional) — Text content (for Text/Button elements)
- `font_size` (string, optional) — Font size (points)

**Example:**

```python
# Create button in new Canvas
await create_ui(type="Button", name="PlayButton", 
               anchor="center", size="200,60", text="Play", font_size="32")

# Create text in existing hierarchy
await create_ui(type="Text", name="Score", parent="Canvas/HUD",
               anchor="top-right", pos="20,-20", size="120,40",
               text="0", font_size="24")

# Create image panel
await create_ui(type="Image", name="HealthBar", parent="Canvas/HUD",
               anchor="top-left", pos="20,-20", size="200,30", color="#cc3333")
```

---

## set_rect

Configure RectTransform anchor, position, size, and offsets. Fine-tune UI element layout.

**Parameters:**
- `path` (string) — Scene path to UI element
- `anchor` (string, optional) — Anchor preset: "stretch" | "center" | "top-left" | "top-right" | "bottom-left" | "bottom-right" | etc.
- `pos` (string, optional) — Position (x,y)
- `size` (string, optional) — Size (width,height)
- `pivot` (string, optional) — Pivot (x,y)
- `offset_min` (string, optional) — Min corner offset (x,y)
- `offset_max` (string, optional) — Max corner offset (x,y)

**Example:**

```python
# Set button to center of screen, 200x60
await set_rect(path="Canvas/PlayButton",
              anchor="center", size="200,60")

# Top-left HUD element with padding
await set_rect(path="Canvas/HUD/HealthBar",
              anchor="top-left", pos="20,-20", size="200,30")

# Stretch to fill parent with margins
await set_rect(path="Canvas/Background",
              anchor="stretch", offset_min="10,10", offset_max="-10,-10")
```

---

## menu

Execute or list Unity Editor menu items. Access editor menus programmatically.

**Parameters:**
- `action` (string) — "execute" | "list"
- `path` (string, optional) — Menu path (e.g., "File/Save Scene"). Required for execute.

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| list | Show all menu items (or sub-items) | `menu("list")` or `menu("list", path="File")` |
| execute | Run menu item | `menu("execute", path="File/Save")` |

**Menu Hierarchy Example:**
```
File/
  New
  Open
  Save
  Save Scene As...
Edit/
  Undo
  Redo
Tools/
  Profiler
  Debugger
Assets/
  Create
  Import Package
```

**Note:** Edit/ menu items are NOT supported by Unity API (restrictions by Unity).

**Example:**

```python
# List top-level menus
menus = await menu("list")

# List File menu items
file_items = await menu("list", path="File")

# Save the scene
await menu("execute", path="File/Save")

# Create new scene
await menu("execute", path="File/New")

# Open Profiler
await menu("execute", path="Tools/Profiler")
```

**Use Cases:**
- Programmatically save scenes
- Trigger import/export operations
- Launch editor tools
- Automate repetitive menu actions

---

## ui_intent

Natural language → UI DSL → batch create_ui commands. Convert NL descriptions into complete UI hierarchies.

**Parameters:**
- `intent` (string) — Natural language description (e.g., "Create a health bar at top-left, score at top-right")
- `parent` (string, optional) — Parent path (default: new Canvas)
- `template` (string, optional) — Preset: "hud" | "menu" | "dialog" | "grid"
- `dry_run` (bool, optional) — If True, return DSL without executing (default: False)

**Templates:**

| Template | Usage |
|----------|-------|
| hud | Health bar + score display |
| menu | Button menu with title |
| dialog | Message box with OK button |
| grid | Grid of image cells |

**Example:**

```python
# Create HUD from description
result = await ui_intent(intent="Create a health bar at the top-left showing red, and score counter at top-right showing white text")

# Use preset template
result = await ui_intent(intent="Create HUD", template="hud", parent="Canvas")

# Create menu from intent
result = await ui_intent(intent="Main menu with Play, Settings, Quit buttons centered on screen")

# Create dialog
result = await ui_intent(intent="Confirmation dialog with message and OK button",
                        parent="Canvas")
```

**Intent Syntax Tips:**
- Positions: "top-left", "top-right", "bottom-left", "bottom-right", "center"
- Colors: hex (#FF0000) or named (red, blue, green)
- Elements: text, button, image, panel, layout
- Describe anchor, size, color, text content

---

## get_spatial_context

Analyze collider configuration around an object. Returns collider info, approach vectors, and nearby objects within radius.

**Parameters:**
- `path` (string) — Scene path to target object
- `radius` (float, default=5.0) — Search radius in meters

**Output:** Collider bounds, approach vectors, nearby objects within radius.

**Example:**

```python
# Check spatial context around player
context = await get_spatial_context(path="Player", radius=5.0)

# Verify enemy has clear approach
context = await get_spatial_context(path="Enemy", radius=10.0)
```

---

## validate_layout

Check for trigger overlaps. Warns if triggers are closer than minimum distance.

**Parameters:**
- `root` (string, default="/") — Root path to scan (default: whole scene)
- `min_distance` (float, default=3.0) — Minimum distance between triggers in meters

**Output:** List of trigger overlaps or warnings.

**Example:**

```python
# Validate whole scene triggers (3m minimum spacing)
result = await validate_layout()

# Check specific subtree with 5m minimum
result = await validate_layout(root="Dungeon", min_distance=5.0)
```

**Use Cases:**
- Verify trigger zones don't overlap
- Validate level design trigger placement
- Pre-playtest sanity check

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Create simple button | create_ui | `await create_ui(type="Button", text="Play", anchor="center")` |
| Create HUD layout | create_ui (multiple) | Create Canvas, then Panel, then Image/Text children |
| Fine-tune UI position | set_rect | `await set_rect(path="Canvas/Button", anchor="top-left", pos="10,-10")` |
| Generate UI from description | ui_intent | `await ui_intent(intent="Health bar and score counter")` |
| Verify trigger spacing | validate_layout | `await validate_layout(min_distance=3.0)` |
| Check object colliders | get_spatial_context | `await get_spatial_context(path="Enemy", radius=5.0)` |

---

**See also:** [Scene Tools](scene.md) for screenshot with UI, [Objects Tools](objects.md) for component management.
