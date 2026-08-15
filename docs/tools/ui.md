# UI Tools

Two UI systems available in Unity. Use **uGUI** for Canvas-based UI; use **UI Toolkit** for UXML/USS panels.

---

## uGUI (Canvas-based UI)

### create_ui

Create UI elements with smart defaults. Automatically creates Canvas if needed.

**Parameters:**
- `type` (string) — "Canvas" | "Panel" | "Button" | "Text" | "Image" | "Toggle" | "Slider" | "InputField" | "ScrollView"
- `name` (string, optional) — Element name
- `parent` (string, optional) — Parent path (default: new Canvas root)
- `anchor` (string, optional) — Anchor preset: "stretch" | "center" | "top-left" | "top-right" | "bottom-left" | "bottom-right"
- `pos` (string, optional) — Position (x,y)
- `size` (string, optional) — Size (width,height)
- `pivot` (string, optional) — Pivot point (x,y)
- `color` (string, optional) — Color (hex #RRGGBB or named)
- `text` (string, optional) — Text content (for Text/Button/Toggle/InputField)
- `font_size` (string, optional) — Font size (points)
- `render_mode` (string, optional) — Canvas render mode: "SSO" (ScreenSpaceOverlay, default) | "SSC" (ScreenSpaceCamera) | "WorldSpace"
- `font_min` (string, optional) — TextMeshPro minimum font size (enables auto-sizing for Text)
- `font_max` (string, optional) — TextMeshPro maximum font size (enables auto-sizing for Text)

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

### set_rect

Configure RectTransform anchor, position, size, and offsets. Fine-tune UI element layout.

**Parameters:**
- `path` (string) — Scene path to UI element
- `anchor` (string, optional) — Anchor preset: "stretch" | "center" | "top-left" | "top-right" | "bottom-left" | "bottom-right" | etc.
- `pos` (string, optional) — Position (x,y)
- `pos3` (string, optional) — Position with depth (x,y,z) — useful for WorldSpace Canvas
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

### lint_ugui

Validate uGUI Canvas structure, RectTransform consistency, and persistent event listeners.

**Parameters:**
- `root` (string, optional) — Root path to scan for Canvas elements (default: "/" scans whole scene)

**Output:** Report of missing EventSystem, broken Button listeners, constraint mismatches, or "OK" if clean.

**Example:**

```python
# Validate all Canvas in scene
issues = await lint_ugui()

# Check specific Canvas subtree
issues = await lint_ugui(root="/MenuCanvas")
```

---

### list_events

List all UnityEvent persistent listeners on a component in the scene.

**Parameters:**
- `path` (string) — Scene path to GameObject
- `component` (string) — Component type (e.g., "Button", "Toggle", "MyScript")
- `event` (string) — Event name (e.g., "onClick", "onValueChanged")

**Output:** Array of persistent listeners with target GameObject, method, and parameters.

**Example:**

```python
# List Button click listeners
listeners = await list_events(path="/Canvas/PlayButton", component="Button", event="onClick")

# List Toggle change listeners
listeners = await list_events(path="/Canvas/SoundToggle", component="Toggle", event="onValueChanged")
```

---

### ui_intent

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
result = await ui_intent(intent="Confirmation dialog with message and OK button", parent="Canvas")
```

---

## UI Toolkit (UXML/USS panels)

For UXML/USS authoring and UIDocument inspection. Enable the `UITOOLKIT` tool category when using these tools.

### inspect_uitk

Inspect the VisualElement tree of a UIDocument panel. Returns compact text tree with element references.

**Parameters:**
- `path` (string, optional) — Scene path to UIDocument GameObject (e.g., "/HUD"), or "scene" to list all UIDocuments
- `depth` (int, optional) — Max traversal depth (default: 4)
- `selector` (string, optional) — Start tree from first matching element (name, .class, TypeName, or ~refid)
- `filter` (string, optional) — Show only elements whose name or classes contain this substring
- `show_unity_private` (bool, optional) — Include #unity-* prefixed elements (normally hidden)
- `show_style` (bool, optional) — Include computed style values per element

**Output:** Compact text tree with refid markers (~N) for use with uitk_element.

**Example:**

```python
# Inspect HUD UIDocument
tree = await inspect_uitk(path="/HUD", depth=5)

# List all UIDocuments in scene
tree = await inspect_uitk(path="scene")

# Focus on a subtree by selector
tree = await inspect_uitk(path="/HUD", selector=".panel__header")
```

---

### lint_uitk

Validate UXML/USS file structure and references.

**Parameters:**
- `path` (string) — Assets/ path to UXML or USS file
- `fix` (bool, optional) — Auto-remove unsupported CSS properties and normalize format

**Checks:** Well-formed XML, broken `<Style src>` references, missing `<Template src>` dependencies, CamelCase class names, star selectors, duplicate CSS variables.

**Example:**

```python
# Validate UXML layout file
issues = await lint_uitk(path="Assets/UI/HUD.uxml")

# Validate stylesheet
issues = await lint_uitk(path="Assets/UI/styles.uss", fix=True)
```

---

### uitk_element

Query or mutate a VisualElement in a UIDocument.

**Parameters:**
- `action` (string) — "query" | "get" | "set_style" | "add_class" | "remove_class" | "get_style" | "enable" | "disable"
- `path` (string, optional) — Scene path to UIDocument GameObject (e.g., "/HUD")
- `ref` (string, optional) — ~N refid from inspect_uitk (highest priority, stale after re-inspect)
- `selector` (string, optional) — CSS selector: .class-name, TypeName, or element name
- `name` (string, optional) — Element name (equivalent to bare name in selector)
- `value` (string, optional) — Value to write (for set_style/add_class)
- `property` (string, optional) — CSS property name for set_style/get_style
- `class_name` (string, optional) — USS class name for add_class/remove_class (no leading dot)

**Actions:**

| Action | Purpose |
|--------|---------|
| query | Find elements matching selector |
| get | Read text or value of element |
| set_style | Write CSS property value |
| add_class | Add USS class to element |
| remove_class | Remove USS class from element |
| get_style | Read computed CSS property |
| enable | Show element |
| disable | Hide element |

**Example:**

```python
# Query health label
result = await uitk_element(action="query", path="/HUD", selector=".health-label")

# Get health text value
value = await uitk_element(action="get", path="/HUD", ref="~3")

# Set style property
await uitk_element(action="set_style", path="/HUD", selector=".health-bar",
                   property="width", value="50%")

# Add critical state class
await uitk_element(action="add_class", path="/HUD", name="health-bar",
                   class_name="health-bar--critical")
```

---

### attach_uitk

Attach a UIDocument (UXML) to a GameObject at runtime or in Edit Mode.

**Parameters:**
- `path` (string) — Scene path to target GameObject
- `uxml_path` (string) — Assets/ path to UXML file
- `sort_order` (int, optional) — Sort order for panel layering (default: 0)

**Example:**

```python
# Attach HUD panel to camera GameObject
await attach_uitk(path="/Main Camera", uxml_path="Assets/UI/HUD.uxml", sort_order=10)
```

---

### uitk_file

Create or modify UXML/USS files programmatically.

**Parameters:**
- `action` (string) — "create_uxml" | "create_uss" | "create_style_sheet" | "add_element" | "add_style"
- `path` (string) — Assets/ destination path
- `name` (string, optional) — Element or class name
- `template` (string, optional) — Template name (for create actions)

**Example:**

```python
# Create new UXML file
await uitk_file(action="create_uxml", path="Assets/UI/NewPanel.uxml")

# Create new stylesheet
await uitk_file(action="create_uss", path="Assets/UI/panel.uss")
```

---

### uitk_intent

Natural language → UXML/USS code. Convert NL descriptions into UI Toolkit panel definitions.

**Parameters:**
- `intent` (string) — Natural language description
- `style` (bool, optional) — Include generated USS stylesheet
- `dry_run` (bool, optional) — Return code without writing files

**Example:**

```python
# Generate health bar panel from description
result = await uitk_intent(intent="Create a health bar with label and background")
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

## Common Patterns

**uGUI (Canvas-based):**

| Task | Tools | Example |
|------|-------|---------|
| Create simple button | create_ui | `await create_ui(type="Button", text="Play", anchor="center")` |
| Create HUD layout | create_ui (multiple) | Create Canvas, then Panel, then Image/Text children |
| Fine-tune UI position | set_rect | `await set_rect(path="Canvas/Button", anchor="top-left", pos="10,-10")` |
| Generate UI from description | ui_intent | `await ui_intent(intent="Health bar and score counter")` |
| Validate Canvas setup | lint_ugui | `await lint_ugui()` |
| Check event listeners | list_events | `await list_events(path="/Button", component="Button", event="onClick")` |

**UI Toolkit (UXML/USS):**

| Task | Tools | Example |
|------|-------|---------|
| Inspect UIDocument tree | inspect_uitk | `await inspect_uitk(path="/HUD")` |
| Validate UXML/USS file | lint_uitk | `await lint_uitk(path="Assets/UI/HUD.uxml")` |
| Query or mutate element | uitk_element | `await uitk_element(action="get", path="/HUD", selector=".health-label")` |
| Attach UXML at runtime | attach_uitk | `await attach_uitk(path="/Camera", uxml_path="Assets/UI/HUD.uxml")` |
| Create UXML/USS file | uitk_file | `await uitk_file(action="create_uxml", path="Assets/UI/Panel.uxml")` |
| Generate UI from description | uitk_intent | `await uitk_intent(intent="Create a health bar panel")` |

---

**See also:** [Scene Tools](scene.md) for screenshot with UI, [Spatial Tools](spatial.md) for collision and trigger analysis, [Objects Tools](objects.md) for component management.
