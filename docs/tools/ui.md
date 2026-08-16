# UI tools

Unity Biome MCP supports both Unity UI systems:

- Use **uGUI** (`UGUI`) for Canvas, RectTransform, and GameObject-based UI.
- Use **UI Toolkit** (`UITOOLKIT`) for UXML, USS, `UIDocument`, and live
  `VisualElement` trees.

Enable the category that matches the project. The systems use different
hierarchies and tools; a `VisualElement` is not a scene GameObject.

```python
await discover_tools(category="UGUI", enable=True)
# or
await discover_tools(category="UITOOLKIT", enable=True)
```

The [generated schema](../tools-schema/index.md) is the exhaustive parameter
reference. This page owns the authoring workflow, persistence rules, and
recovery guidance.

## uGUI workflow

### Create a Canvas hierarchy

`create_ui` creates `Canvas`, `Panel`, `Button`, `Text`, `Image`, `Toggle`,
`Slider`, `InputField`, or `ScrollView` objects. It creates a Canvas when a
non-Canvas element has no suitable parent.

```python
await create_ui(type="Canvas", name="HUD", render_mode="SSO")
await create_ui(
    type="Text",
    name="Score",
    parent="/HUD",
    anchor="top-right",
    pos="-24,-24",
    size="160,48",
    text="Score: 0",
    font_size="24",
    font_min="18",
    font_max="30",
)
await create_ui(
    type="Button",
    name="PauseButton",
    parent="/HUD",
    anchor="top-left",
    pos="24,-24",
    size="160,48",
    text="Pause",
)
await create_ui(
    type="ScrollView",
    name="ItemList",
    parent="/HUD",
    anchor="stretch",
    pos="0,0",
    size="0,0",
)
```

Canvas render modes are `SSO` (Screen Space Overlay), `SSC` (Screen Space
Camera), and `WorldSpace`. Camera assignment and project-specific components
can be configured afterward with the normal component tools. `font_min` and
`font_max` enable TextMeshPro auto-sizing; the legacy `Text` fallback ignores
them when TextMeshPro is unavailable.

`ScrollView` creates a canonical hierarchy: full-stretch Viewport with Mask,
top-left-anchored Content with ContentSizeFitter for automatic sizing, and a
root Image for visual background. Omit `color` to skip the root Image coloring.

### Refine RectTransform layout

Use `set_rect` for an existing uGUI object. `pos3` sets
`anchoredPosition3D` and wins when both `pos` and `pos3` are provided.

```python
await set_rect(
    path="/HUD/PauseButton",
    anchor="top-left",
    pos="24,-24",
    size="160,48",
    pivot="0,1",
)

# Fill the parent with a 12-pixel inset.
await set_rect(
    path="/HUD/Background",
    anchor="stretch",
    offset_min="12,12",
    offset_max="-12,-12",
)
```

Use `pos3` for the depth of a World Space Canvas element:

```python
await set_rect(path="/WorldCanvas/Label", pos3="0,0,0.02")
```

### Validate interaction infrastructure

`lint_ugui` performs eight structural checks:

**ScrollRect checks (S1–S5):**
- [S1] Viewport missing or not full-stretch (0,0)→(1,1)
- [S2] Content cannot grow (missing ContentSizeFitter, LayoutGroup, or nonzero sizeDelta)
- [S3] Masks on both root and Viewport (keep only Viewport)
- [S4] Scrollbar in children but not wired to horizontalScrollbar or verticalScrollbar
- [S5] Content is null

**General layout checks (G1–G3):**
- [G1] Active RectTransform with point anchor and zero size
- [G2] Image without sprite + raycastTarget=true with no interactable ancestor (blocks raycasts invisibly)
- [G3] LayoutGroup with no active children

```python
report = await lint_ugui(root="/HUD")
```

An `ok: 0 issues` result means all structural checks passed. Use
[`list_events`](components.md#list_events) to inspect persistent UnityEvent
listeners and the screenshot workflow to verify the rendered result.

<span id="ui_intent"></span>

### Generate a uGUI layout from intent

`ui_intent` converts a description into `create_ui`, `set_rect`, and component
operations. Preview first when the hierarchy matters:

```python
plan = await ui_intent(
    intent="A compact pause menu with Resume, Settings, and Quit buttons",
    parent="/HUD",
    dry_run=True,
)
```

Templates `hud`, `menu`, `dialog`, and `grid` are deterministic and do not use
sampling:

```python
result = await ui_intent(
    intent="Create the standard HUD",
    template="hud",
    dry_run=False,
)
```

Review the created hierarchy, run `lint_ugui`, and capture a screenshot. For
general intent-tool behavior, see [Intent Tools](../features/intent-tools.md).

## UI Toolkit workflow

### Author UXML and USS

`uitk_file` reads and edits project-local `.uxml` and `.uss` files under
`Assets/`. It rejects `Packages/`, `Library/`, and other file types.

```python
await uitk_file(
    action="create_uss",
    path="Assets/UI/PauseMenu.uss",
    content=".pause-menu { padding: 24px; }",
)
await uitk_file(
    action="create_uxml",
    path="Assets/UI/PauseMenu.uxml",
    content=(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<ui:UXML xmlns:ui="UnityEngine.UIElements">\n'
        '    <Style src="PauseMenu.uss" />\n'
        '    <ui:Button name="resume" text="Resume" class="pause-menu" />\n'
        '</ui:UXML>'
    ),
)
```

Supported actions are:

- whole-file `read` and `write`;
- `create_uxml` and `create_uss`;
- UXML `set-attr`, `add-class`, `remove-class`, `add-element`, and
  `remove-element`;
- USS `set-rule` and `remove-rule`;
- single-level `revert` for a previously existing file, available only until
  the Unity domain reloads. A newly created file has no prior content to restore
  and must be removed with an asset/file workflow instead.

`read` returns the UTF-8 source verbatim and remains available on a read-only
worker. Every other or unknown action is treated as a write. Mutating actions
import the asset. Prefer the structural actions for small edits and `write`
when replacing the whole file is deliberate.

```python
source = await uitk_file(action="read", path="Assets/UI/PauseMenu.uxml")
await uitk_file(
    action="set-rule",
    path="Assets/UI/PauseMenu.uss",
    selector=".pause-menu",
    prop="margin-top",
    value="12px",
)
```

### Lint source files

`lint_uitk` is validation-only. It performs structural UXML and USS validation:

- malformed UXML;
- broken `<Style src>` references;
- missing `<Template src>` references;
- unnamed interactive elements;
- duplicate USS selectors;
- empty USS rules.

```python
report = await lint_uitk(path="Assets/UI/PauseMenu.uxml")
```

The compatibility parameter `fix` is reserved. `fix=True` fails explicitly and
does not change the file; apply a reported correction with `uitk_file`, then
lint again.

### Attach a UIDocument

`attach_uitk` adds one `UIDocument` component to a scene GameObject and can
assign existing UXML and `PanelSettings` assets:

```python
await attach_uitk(
    path="/UIRoot",
    uxml="Assets/UI/PauseMenu.uxml",
    panel_settings="Assets/UI/GamePanel.asset",
    sort_order=10,
)
```

All supplied assets are validated before the component is added. Omitting
`uxml` or `panel_settings` leaves that field unassigned; the tool does not
create a `PanelSettings` asset. It refuses to add a second `UIDocument` to the
same GameObject.

### Inspect and change the live tree

`inspect_uitk` serializes a live `UIDocument.rootVisualElement` tree and assigns
compact `~N` references:

```python
tree = await inspect_uitk(path="/UIRoot", depth=5)
focused = await inspect_uitk(path="/UIRoot", selector=".pause-menu")
all_documents = await inspect_uitk(path="scene")
```

If the live root is unavailable in Edit Mode, enter Play Mode or inspect the
UXML source with `uitk_file`. References expire after another inspect or a
domain reload; on `err: stale ref`, inspect again.

Use `uitk_element` to query controls or make live changes:

```python
matches = await uitk_element(
    action="query",
    path="/UIRoot",
    selector=".pause-menu",
)
text = await uitk_element(action="get", ref="~3", property="text")
await uitk_element(action="add_class", ref="~3", class_name="is-paused")
await uitk_element(action="set_style", ref="~3", property="opacity", value="0.5")
```

Addressing priority is `ref`, then `name`, then `selector`. `get` supports
`text`, `value`, `visible`, `name`, and `enabled`. Inline `set_style` supports
only `display`, `opacity`, and `visibility`; edit other properties in USS with
`uitk_file`. These actions change the current live tree and never edit UXML or
USS. Treat them as transient—Play Mode mutations also return an explicit
non-persistence warning—and use `uitk_file` for a durable source change.

<span id="uitk_intent"></span>

### Generate a panel from intent

`uitk_intent` requires a base `name` and writes a paired `.uss` then `.uxml`.
Templates `hud`, `menu`, `dialog`, `settings`, and `editor_window` bypass
sampling.

```python
preview = await uitk_intent(
    intent="A settings panel with sound and music toggles",
    name="AudioSettings",
    template="settings",
    dry_run=True,
)

result = await uitk_intent(
    intent="A settings panel with sound and music toggles",
    name="AudioSettings",
    path="Assets/UI",
    template="settings",
    attach_to="/UIRoot",
)
```

The generated-file steps are sequential, not a cross-tool transaction. If a
file or attach step fails, the result separates completed operations from paths
that may have been created or modified before the failure. Unity removes a
brand-new asset when its own post-import validation fails, but a transport
failure can leave the outcome uncertain and earlier successful writes remain.
Inspect every reported path before retrying.

## Verification checklist

For either UI system:

1. Inspect the created hierarchy or live tree.
2. Run `lint_ugui` or `lint_uitk` as appropriate.
3. Inspect persistent events for interactive uGUI controls.
4. Capture the target view and compare it with the intended layout.
5. Save the scene only after structural and visual verification pass.

See [Screenshots and visual diffs](screenshots.md) for the visual step and
[System and orchestration](system.md) for synchronization and recovery tools.
