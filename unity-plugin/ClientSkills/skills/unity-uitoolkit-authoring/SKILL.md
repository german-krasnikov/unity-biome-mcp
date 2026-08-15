---
name: unity-uitoolkit-authoring
description: Use when inspecting, authoring, or validating UI Toolkit UXML/USS panels, VisualElements, or custom controls in Unity 6.
---

# Unity UI Toolkit Authoring

> **UI Toolkit skill.** For Canvas-based uGUI → `unity-ugui-authoring`.

Read `.claude/skills/unity-mcp-operations/SKILL.md` once if not already loaded.
Enable `UITOOLKIT` when `inspect_uitk` or `lint_uitk` are gated.

## Available Tools

| Tool | When |
|------|------|
| `inspect_uitk` | Read the VisualElement tree of a UIDocument component |
| `lint_uitk` | Check structural validity of a UXML/USS asset |
| `discover_tools("uitoolkit")` | Load the full UITOOLKIT schema |

Enable with: `discover_tools("uitoolkit")`.

## Workflow

1. Run `inspect_uitk(path="/GameObject")` to read the live VE tree.
2. Use compact `~N` refs from the output for targeted follow-up queries.
3. Lint an asset with `lint_uitk(path="Assets/UI/HUD.uss")` before writing.
4. In Playtest DSL, declare the UIDocument once as a VAL alias:

```text
VAL $hud /Player/HUD|UIDocument
ASSERT $hud|health-label|text == "75 HP"
WAIT_UNTIL $hud|score|value > 10 TIMEOUT 5
```

## USS Supported Subset (Unity 6)

Unity 6's USS parser supports a subset of CSS. These are silently ignored or
cause import errors — do not generate them:

| Not supported | Use instead |
|---------------|-------------|
| `display: grid` | flex layout |
| `@media` queries | C# code |
| `calc()` | fixed values |
| `@keyframes` | USS transitions |
| `:nth-child` | class-based selectors |
| `box-shadow` | no direct equivalent |
| CSS gradients | `Texture2D` background |
| `::before` / `::after` | child `VisualElement` |

Supported: `display: flex`, `flex-direction`, `flex-wrap`, `align-items`,
`justify-content`, `position: absolute/relative`, `margin`, `padding`,
`border-*`, `width`, `height`, `min-*`, `max-*`, USS transitions.

## BEM Naming Convention

Use BEM in USS selectors: `.block__element--modifier`, all kebab-case.

```uss
.hud__health-bar { flex-direction: row; }
.hud__health-bar--critical { background-color: red; }
```

## System Color Variables

Use Unity's CSS custom properties (`--unity-colors-*`) for theme-aware colors.
Reference them via the USS `var` keyword in stylesheets:

```uss
.my-panel {
    background-color: #282828;  /* or --unity-colors-window-background */
    color: #c8c8c8;             /* or --unity-colors-default-text */
}
```

Key variables: `--unity-colors-window-background`, `--unity-colors-default-text`,
`--unity-colors-highlight-background`, `--unity-colors-input-field-background`.

## Custom Controls (Unity 6)

Use `[UxmlElement]` and `[UxmlAttribute]` — not the deprecated
`UxmlFactory`/`UxmlTraits` pattern:

```csharp
[UxmlElement]
public partial class HealthBar : VisualElement
{
    [UxmlAttribute] public float value { get; set; }
}
```

`UxmlFactory` and `UxmlTraits` are deprecated since Unity 2023.2. Never
generate them for new controls.

## wire_event and UI Toolkit

`wire_event` **cannot** target `VisualElement` or `UIDocument` directly —
those types have no serialized `UnityEvent` fields.

`wire_event` **can** target a MonoBehaviour controller that exposes a
`public UnityEvent` field. Bridge pattern:

```csharp
// MonoBehaviour on the same or parent GameObject:
public UnityEvent onStartClicked;
private System.Action _clickHandler;

void OnEnable()
{
    _clickHandler = () => onStartClicked.Invoke();
    GetComponent<UIDocument>().rootVisualElement
        .Q<Button>("startBtn").clicked += _clickHandler;
}

void OnDisable()
{
    GetComponent<UIDocument>().rootVisualElement
        .Q<Button>("startBtn").clicked -= _clickHandler;
}
```

After the controller exists, wire logic normally:

```text
wire_event(
  path="/HUD",
  component="HUDController",
  event="onStartClicked",
  target="/GameManager",
  method="StartGame"
)
```

If `wire_event` returns `err: '<component>' has no serialized UnityEvent field`,
add the bridge controller first. Do not attempt to wire directly to
`UIDocument` — it will always fail.

## Error Reference

| Situation | Action |
|-----------|--------|
| `rootVisualElement` null in Edit Mode | Enable `RunInEditMode` on UIDocument, or enter Play Mode |
| Selector matches nothing | Call `inspect_uitk` to list available names and classes |
| Stale `~N` ref after re-inspect | Call `inspect_uitk` again — refs reset each call |
| `uitk_*` not available | Run `discover_tools("uitoolkit")` first |
| `wire_event` on UIDocument/VisualElement | Add a MonoBehaviour controller with `public UnityEvent` field |

## Deadly Traps

- `ExecuteEvents` on a UI Toolkit panel does **nothing** — no error, just silence.
- UI Toolkit `Button.onClick` does not exist. Subscribe with `.clicked +=` from C#.
- uGUI coordinate system (`RectTransform`) is incompatible with UI Toolkit
  (`worldBound`, y-down, `RuntimePanelUtils.PanelToScreen`). Never mix them.
- There is no `EventSystem` in UI Toolkit — the panel dispatches events itself.
  Do not add `GraphicRaycaster` to a UI Toolkit panel.
- `wire_event` has no direct equivalent for `VisualElement` events — those are
  runtime-only C# subscriptions.
- Bubble-up propagation: call `evt.StopPropagation()` in inner handlers when
  outer handlers must not fire.
