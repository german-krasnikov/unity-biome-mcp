---
name: unity-ugui-authoring
description: Use when creating or validating Canvas (uGUI) UI, RectTransforms, UI layout, controls, or visual-regression baselines.
---

# Unity uGUI Authoring

> **uGUI skill.** For UI Toolkit (UXML/USS/VisualElement) → `unity-uitoolkit-authoring`.

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. This skill covers
Canvas-based (uGUI) UI exposed by the current MCP tools. It does not cover
UI Toolkit UXML or USS.

## Workflow

1. Inspect the existing Canvas and hierarchy.
2. Enable `UGUI` when UI tools are gated.
3. Create semantic controls with `create_ui`.
4. Set stable anchors, size, pivot, and offsets with `set_rect`.
5. Inspect `RectTransform` values.
6. Validate layout and capture a visual result at the required viewport.

```text
batch(
  commands="""
create_ui type=Canvas name=HUD
create_ui type=Panel name=StatusPanel parent=/HUD anchor=top-right size=(320,120) color=#202226E6
create_ui type=Text name=Status parent=/HUD/StatusPanel anchor=stretch text=READY font_size=24
set_rect path=/HUD/StatusPanel/Status anchor=stretch offset_min=(16,12) offset_max=(-16,-12)
get_component path=/HUD/StatusPanel/Status type=RectTransform
""",
  on_error="stop",
  atomic=True
)
```

## wire_event for uGUI

`wire_event` is the canonical way to connect persistent listeners to uGUI
controls. Target any serialized `UnityEvent` field on a `Button`, `Toggle`, or
custom `MonoBehaviour`:

```text
wire_event(
  path="/HUD/StartButton",
  component="Button",
  event="onClick",
  target="/GameManager",
  method="OnStartClicked"
)
```

Verify the connection with `get_component` and confirm the persistent listener
is present. `wire_event` requires `EventSystem` in the scene — run `lint_ugui`
if clicks do not register.

## Rules

- Use anchors and constraints before absolute offsets.
- Verify long text and narrow viewports.
- Keep touch targets and dense toolbars appropriate for their audience.
- Use data inspection for interactability and references; use screenshots for
  layout and appearance.
- Establish a baseline only after the intended state is stable.
- A pixel difference proves that pixels changed, not that behavior is correct.

Bad:

```text
# INVALID: validate_layout uses root, not path.
validate_layout(path="/HUD")
```

Good:

```text
validate_layout(root="/HUD", min_distance=3)
```

## Deadly Traps

- `ExecuteEvents` on a UI Toolkit panel does nothing — no error, just silence.
  Use `ExecuteEvents` only with uGUI objects.
- uGUI coordinate system is `RectTransform`. UI Toolkit uses `worldBound`
  (panel-space, y-down) + `RuntimePanelUtils.PanelToScreen`. Do not mix them.
- `wire_event` works only with serialized `UnityEvent` fields (uGUI/MonoBehaviour).
  There is no direct UI Toolkit equivalent — use `unity-uitoolkit-authoring` for
  the bridge pattern.
