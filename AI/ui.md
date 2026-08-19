# UI Systems

This document describes the implementation contracts for the two game-UI
stacks exposed by the server. Public workflows belong in `docs/tools/ui.md`;
exact MCP argument schemas are generated in `docs/tools-schema/index.md`.

## Ownership

| Stack | Public tools | Primary implementation |
|---|---|---|
| uGUI | `create_ui`, `set_rect`, `lint_ugui`, `ui_intent` | `server/src/unity_mcp/tools/ui.py`, `ui_intent_tool.py`, `unity-plugin/Editor/UIHelper.cs` |
| UI Toolkit | `inspect_uitk`, `lint_uitk`, `uitk_element`, `attach_uitk`, `uitk_file`, `uitk_intent` | `server/src/unity_mcp/tools/uitk.py`, `uitk_intent_tool.py`, `unity-plugin/Editor/UIHelper.UIToolkit.cs` |

Event inspection and persistent-listener mutation are owned by the component
tools (`list_events`, `wire_event`, and `unwire_event`), not by either UI module.

## uGUI

### `create_ui`

Creates a Canvas or one of the supported uGUI elements: `Panel`, `Button`,
`Text`, `Image`, `Toggle`, `Slider`, `InputField`, or `ScrollView`. When no
parent is supplied, the Unity handler finds or creates a Canvas. Canvas creation
also ensures an EventSystem exists.

Important implementation details:

- `render_mode` accepts `SSO`, `SSC`, or `WorldSpace`.
- `font_min` and `font_max` enable TextMeshPro auto-sizing for `Text`.
- Colors use `#RRGGBB` or `#RRGGBBAA`.
- The handler records normal Unity Undo operations; persistence still requires
  the caller to save the scene.

### `set_rect`

Updates a `RectTransform` using anchor presets plus `pos`, `size`, `pivot`, and
offset values. `pos3` writes `anchoredPosition3D` for world-space canvases and
wins when both `pos` and `pos3` are present.

The canonical presets are implemented in `UIHelper.cs`. Do not copy the list
into new documents or validators; use the generated tool schema and handler
tests as the contract.

### `lint_ugui`

Performs a read-only scan for missing EventSystem/GraphicRaycaster
infrastructure. `root` scopes the scan; omission scans all loaded scenes. It
does not repair the scene.

### `ui_intent`

Converts either a deterministic template or a sampled natural-language result
into the indent-based uGUI DSL, then compiles the DSL to public batch commands.
Templates (`hud`, `menu`, `dialog`, and `grid`) bypass sampling. `dry_run=True`
returns the generated batch DSL without applying it.

The builder deliberately emits ordinary `create_ui`, `set_rect`,
`manage_component`, and `set_property` calls. Keep those public contracts in
sync when extending the intent DSL.

```text
canvas Canvas
  panel HUD anchor=stretch
    text Score anchor=top-right pos=-20,-20 size=120,40 text="0" fontSize=24
```

## UI Toolkit

### Live panel inspection

`inspect_uitk` serializes the live `VisualElement` tree of a `UIDocument`. The
returned `~N` references are compact session-local addresses. They may become
stale after another inspection, a panel rebuild, or a domain reload.

Element lookup supports a UIDocument GameObject `path`, a `~N` `ref`, an
element `name`, or a selector. `uitk_element` uses the priority
`ref -> name -> selector` and supports only these actions:

- `query`, `get`, and `get_style` for reads;
- `set_style`, `add_class`, `remove_class`, `enable`, and `disable` for live
  mutations.

Live element mutations are not serialized back to UXML/USS and do not survive
Play Mode or a panel rebuild. Persisted authoring goes through `uitk_file`.

### File linting

`lint_uitk(path, fix=False)` validates UXML/USS assets. Current checks cover
malformed UXML, broken `Style` and `Template` references, unnamed interactive
elements, duplicate USS selectors, and empty USS rules.

`fix=True` is intentionally unsupported. The Python wrapper raises a
`ToolError` before sending a command, so this tool never changes a file.

### File authoring

`uitk_file` accepts an `Assets/` path ending in `.uxml` or `.uss`; `Library/`
and `Packages/` are rejected. Its current action set is:

```text
read, write, create_uxml, create_uss,
set-attr, add-class, remove-class,
add-element, remove-element,
set-rule, remove-rule, revert
```

`read` returns the UTF-8 file text verbatim. `write` replaces a complete file.
The structural actions mutate a named UXML element or an exact USS selector.
`revert` is single-level, in-memory recovery and is cleared by domain reload;
it is not a durable version-control mechanism.

### `attach_uitk`

`attach_uitk(path, uxml=None, panel_settings=None, sort_order=None)` adds a
`UIDocument` to an existing GameObject and optionally assigns a
`VisualTreeAsset` and `PanelSettings` asset.

- `uxml` is optional. Omission creates the component without a visual tree.
- `panel_settings` is optional. Omission leaves the serialized field unset; the
  handler does not silently create an asset.
- An existing `UIDocument` is an error. The tool does not overwrite or partially
  reconfigure it.
- Asset assignment is all-or-nothing: invalid supplied assets do not leave a
  partially configured component.

**Unity 6.4+ PanelRenderer:** On Unity 6000.4 and later, the handler may add a
`PanelRenderer` component instead of `UIDocument` depending on the target
GameObject configuration. This detail is transparent to the user — the tool
succeeds when a compatible UI host is present. The actual component type is
determined at runtime by `UIPanelHost.CreateHost()` based on the active Unity
version.

### `uitk_intent`

The signature is:

```python
await uitk_intent(
    intent="A compact inventory panel",
    name="InventoryPanel",
    path="Assets/UI",
    attach_to="/UIRoot",
    template=None,
    dry_run=False,
)
```

Deterministic templates are `hud`, `menu`, `dialog`, `settings`, and
`editor_window`; they bypass sampling. Otherwise the configured sampling
profile generates an intermediate `=TREE=` / `=STYLE=` DSL. The tool validates
unsupported USS features and retries sampling once.

For a mutating call, the operation order is USS creation, UXML creation, then
optional attachment. These are separate operations, not a transaction. An
error report names the completed operations and files already processed; it
does not claim rollback. `dry_run=True` writes and attaches nothing.

## Playtest addressing

The playtest DSL can address a `UIDocument` (or `PanelRenderer` on Unity 6.4+)
and then an element and field with the normal pipe syntax:

```text
VAL $hud /Player/HUD|UIDocument
FILL $hud|input-name "Player1"
FOCUS $hud|input-name
ASSERT $hud|score-label|text == "0"
```

The parser normalizes both `|PanelRenderer|` and `|UI|` tokens to the internal
`|UIDocument|` representation at parse time. This allows the DSL to reference
either component type consistently:

```text
# Both work on Unity 6.4+:
CLICK /UIRoot|PanelRenderer|start-button
CLICK /UIRoot|UI|settings-button
# Normalized to UIDocument internally:
CLICK /UIRoot|UIDocument|start-button
CLICK /UIRoot|UIDocument|settings-button
```

The parser/executor contract is documented in `AI/playtest-dsl.md`; UI code
must not introduce a second path grammar.

## Tests

- Python: `server/tests/test_uitk.py`, `test_uitk_element.py`,
  `test_uitk_file_tool.py`, `test_uitk_intent.py`, and the uGUI server tests.
- C#: `unity-plugin/Editor/Tests/UIToolkit/` and the UI helper fixtures under
  `unity-plugin/Editor/Tests/`.
- Repository test policy: `AI/testing.md`.

## Plugin Editor UI

The Biome plugin's own Editor windows are separate from the game-UI tools. Use
the shared `BiomeUI`, `BiomeToggleGroup`, animation, and particle primitives
before adding a window-local abstraction. Static presentation belongs in USS;
inline styles are reserved for runtime-derived values.

The canonical implementation rules are in `AI/ui-style.md`. `AI/animation.md`
and `AI/particles.md` describe the shared motion primitives.

## Related

- `docs/tools/ui.md` — user workflows
- `AI/intent-tools.md` — shared intent pipeline
- `unity-plugin/ClientSkills/skills/unity-ugui-authoring/SKILL.md`
- `unity-plugin/ClientSkills/skills/unity-uitoolkit-authoring/SKILL.md`
