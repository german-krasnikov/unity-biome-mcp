---
name: unity-uitoolkit-authoring
description: Use when inspecting, authoring, attaching, or validating UI Toolkit UXML, USS, UIDocument panels, VisualElements, or custom controls in Unity 6.
---

# Unity UI Toolkit Authoring

For Canvas-based controls and `RectTransform` layout, use
`unity-ugui-authoring` instead.

Read `.claude/skills/unity-mcp-operations/SKILL.md` once if it is not already
loaded. Enable `UITOOLKIT` when these tools are gated.

## Tool Map

| Tool | Use |
|---|---|
| `inspect_uitk` | Inspect a live `UIDocument` tree and obtain compact `~N` element refs |
| `lint_uitk` | Validate one UXML or USS asset without changing it |
| `uitk_element` | Query or change one live `VisualElement` instance |
| `attach_uitk` | Add and configure `UIDocument` on a GameObject |
| `uitk_file` | Read or edit a UXML or USS asset |
| `uitk_intent` | Draft a UXML/USS pair from a template or natural-language intent |

Use `discover_tools("uitoolkit")` to enable the category, then resolve all
uncertain schemas in one `resolve_tool_schema` call.

## Inspect And Query

Start from the live tree:

```text
inspect_uitk(path="/HUD", depth=5, show_style=True)
uitk_element(action="get", path="/HUD", name="health-label", property="text")
uitk_element(action="query", path="/HUD", selector=".inventory__slot")
```

`inspect_uitk` accepts a GameObject path containing `UIDocument`; omit the path
or use `scene` to list open documents. A `~N` ref is valid only for the current
inspection table. Re-inspection or domain reload can make it stale.

`uitk_element` supports:

- `query`, `get`, and `get_style` for reads;
- `set_style`, `add_class`, `remove_class`, `enable`, and `disable` for live
  instance changes.

Element addressing priority is `ref`, then `name`, then `selector`. Use
`property` for `get`, `get_style`, or `set_style`, and `class_name` for class
operations. Live element changes are not edits to the UXML or USS source; use
`uitk_file` for persistent asset changes.

## Author Assets

`uitk_file` supports these actions:

```text
read
write
create_uxml
create_uss
set-attr
add-class
remove-class
add-element
remove-element
set-rule
remove-rule
revert
```

Paths must be `.uxml` or `.uss` assets below `Assets/`; `Library/` and
`Packages/` are rejected. `read` returns the file's UTF-8 content verbatim and
is permitted on a read-only worker; every other or unknown action is a write.
`revert` is a single-level in-memory recovery and is cleared by domain reload;
it is not version control.

Example:

```text
uitk_file(
  path="Assets/UI/HUD.uxml",
  action="add-element",
  parent="root",
  tag="ui:Label",
  attrs="name=score-label text=0"
)
uitk_file(
  path="Assets/UI/HUD.uss",
  action="set-rule",
  selector=".hud__score",
  prop="color",
  value="#ffffff"
)
lint_uitk(path="Assets/UI/HUD.uxml", fix=False)
lint_uitk(path="Assets/UI/HUD.uss", fix=False)
```

`lint_uitk` checks malformed UXML, missing Style and Template references,
unnamed interactive elements, duplicate USS selectors, and empty USS rules.
`fix=True` is unsupported and returns an error; the tool never auto-formats or
modifies the file.

## Attach A Panel

Resolve every asset path before mutating the scene:

```text
attach_uitk(
  path="/HUD",
  uxml="Assets/UI/HUD.uxml",
  panel_settings="Assets/UI/HUDPanelSettings.asset",
  sort_order=10
)
inspect_uitk(path="/HUD")
```

`path` is required. `uxml` and `panel_settings` are optional, but supplied
assets must exist. Omitting `panel_settings` does not create one automatically.
The tool refuses a GameObject that already has `UIDocument` and validates all
supplied assets before its single Undo-recorded scene mutation.

## Intent Drafts

`uitk_intent` requires `intent` and `name`. The optional template values are
`hud`, `menu`, `dialog`, `settings`, and `editor_window`.

```text
uitk_intent(
  intent="A compact inventory panel with a filter field and item list",
  name="InventoryPanel",
  path="Assets/UI",
  attach_to="/HUD",
  dry_run=True
)
```

Inspect dry-run output before writing. A non-dry run creates USS first, then
UXML, and optionally calls `attach_uitk`. It stops at the first reported tool
error. Treat every path in the failure report as possibly changed: Unity cleans
up a brand-new asset rejected by its own import validation, but earlier writes
and transport-uncertain attempts are not rolled back across tools. Inspect the
reported paths and clean up only artifacts owned by that run.

## Playtest Interaction

Address a UI Toolkit element as
`GameObject|UIDocument|element-name` in Playtest DSL:

```text
CLICK /HUD|UIDocument|submit-button
FILL /HUD|UIDocument|player-name Player1
FOCUS /HUD|UIDocument|player-name
ASSERT /HUD|UIDocument|status-label|text == "Ready"
```

`CLICK` also supports ordinary uGUI object paths. `FILL` and `FOCUS` require
the `UIDocument|element-name` form. Inspect the live tree when a selector fails.

## Rules

- Use stable, kebab-case element names and USS classes. Prefer BEM-style class
  names for reusable components.
- Use flex layout; do not assume browser-only CSS such as grid, media queries,
  `calc()`, keyframes, pseudo-elements, or `box-shadow` is available in USS.
- New custom controls use `[UxmlElement]` and `[UxmlAttribute]`, not deprecated
  `UxmlFactory` and `UxmlTraits` declarations.
- `wire_event` cannot target a `VisualElement` or `UIDocument` directly. Route a
  runtime callback through a `MonoBehaviour` with a serialized `UnityEvent`
  only when persistent scene wiring is required.
- Do not add an EventSystem or GraphicRaycaster to a UI Toolkit panel; panel
  events are separate from uGUI.
- Do not mix `RectTransform` coordinates with UI Toolkit `worldBound` panel
  coordinates.
