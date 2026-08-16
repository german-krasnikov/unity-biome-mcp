---
name: unity-ugui-authoring
description: Use when creating or validating Canvas (uGUI) UI, RectTransforms, controls, persistent UI events, or visual-regression baselines.
---

# Unity uGUI Authoring

For UXML, USS, `UIDocument`, or `VisualElement` work, use
`unity-uitoolkit-authoring` instead.

Read `.claude/skills/unity-mcp-operations/SKILL.md` once if it is not already
loaded. Enable `UGUI` when these tools are gated.

## Tool Map

| Tool | Use |
|---|---|
| `create_ui` | Create a Canvas, Panel, Button, Text, Image, Toggle, Slider, InputField, or ScrollView |
| `set_rect` | Set anchors, position, size, pivot, offsets, or World Space depth |
| `lint_ugui` | Check EventSystem and Canvas GraphicRaycaster requirements |
| `ui_intent` | Draft a uGUI hierarchy from a template or natural-language intent |
| `list_events` | Inspect a persistent `UnityEvent` listener after wiring |

`list_events` is shared with component authoring. Resolve the live schema before
using an unfamiliar parameter.

## Deterministic Workflow

1. Inspect the existing Canvas and hierarchy.
2. Mark the console.
3. Create controls with stable names.
4. Set anchors and constraints before pixel offsets.
5. Inspect the resulting `RectTransform` fields.
6. Run `lint_ugui`, verify event listeners, and check the console delta.
7. Use a screenshot only for layout and appearance acceptance.

```text
batch(
  commands="""
create_ui type=Canvas name=HUD render_mode=SSO
create_ui type=Panel name=StatusPanel parent=/HUD anchor=top-right size=(320,120) color=#202226E6
create_ui type=Text name=Status parent=/HUD/StatusPanel anchor=stretch text=READY font_size=24 font_min=14 font_max=28
set_rect path=/HUD/StatusPanel/Status anchor=stretch offset_min=(16,12) offset_max=(-16,-12)
""",
  on_error="stop",
  atomic=True
)
get_component(path="/HUD/StatusPanel/Status", type="RectTransform")
lint_ugui(root="/HUD")
```

`render_mode` accepts `SSO` (Screen Space Overlay, the default), `SSC` (Screen
Space Camera), or `WorldSpace`. For a World Space Canvas, use `set_rect.pos3` to
set `anchoredPosition3D`; it takes precedence over `pos`. `font_min` and
`font_max` enable TextMeshPro autosizing for a Text element.

## Intent Drafts

Use a built-in `hud`, `menu`, `dialog`, or `grid` template when it matches the
request. Preview first:

```text
ui_intent(
  intent="A compact pause menu with resume and quit actions",
  parent="/HUD",
  template="menu",
  dry_run=True
)
```

Inspect the returned commands. Run the precise typed calls yourself when names,
layout values, or event wiring must be exact. An intent result does not prove
that the layout is usable.

## Persistent Events

`wire_event` connects a serialized `UnityEvent` on a uGUI control or custom
`MonoBehaviour`. Verify the exact listener with `list_events`:

```text
wire_event(
  path="/HUD/StartButton",
  component="Button",
  event="onClick",
  target="/GameManager",
  method="OnStartClicked"
)
list_events(
  path="/HUD/StartButton",
  component="Button",
  event="onClick"
)
```

Use `unwire_event` with an explicit listener index unless clearing the entire
event is intended. If pointer input fails, `lint_ugui` can identify a missing
EventSystem or GraphicRaycaster.

## Rules

- Keep touch targets, text wrapping, and narrow viewports in the acceptance
  criteria.
- Inspect interactability, references, and listeners as data; screenshots do
  not prove them.
- Establish a visual baseline only after deterministic state checks pass.
- Do not send uGUI events to a UI Toolkit `VisualElement`. UI Toolkit uses its
  panel event system and runtime C# callbacks.
- Do not mix `RectTransform` coordinates with UI Toolkit `worldBound` panel
  coordinates.
