# UI Creation (Phase 15)

## Overview
Two commands for creating Unity UI: `create_ui` and `set_rect`. Work standalone and in batch.

## Commands

### create_ui
Creates UI elements with smart defaults.

| Param | Req | Description |
|-------|-----|-------------|
| type | yes | Canvas, Panel, Button, Text, Image |
| name | no | GO name (default = type) |
| parent | no | path to parent |
| anchor | no | preset name |
| pos | no | anchoredPosition (x,y) |
| size | no | sizeDelta (w,h) |
| pivot | no | pivot (x,y) |
| color | no | hex #RRGGBB or #RRGGBBAA |
| text | no | text for Text/Button |
| fontSize | no | font size |

**Type behaviors:**
- Canvas: Canvas + CanvasScaler(ScaleWithScreenSize, 1920x1080) + GraphicRaycaster + auto EventSystem
- Panel: Image, anchor=stretch by default
- Button: Button + Image + child Text, anchor=center, size=(160,30)
- Text: TMPro.TextMeshProUGUI (fallback: Text), anchor=center, size=(200,50)
- Image: Image, anchor=center, size=(100,100)

**Auto-Canvas:** If no parent specified for Panel/Button/Text/Image — finds Canvas in scene, creates if missing.

### set_rect
Fast RectTransform configuration.

| Param | Req | Description |
|-------|-----|-------------|
| path | yes | object path |
| anchor | no | preset name |
| pos | no | anchoredPosition (x,y) |
| size | no | sizeDelta (w,h) |
| pivot | no | pivot (x,y) |
| offset_min | no | (left, bottom) |
| offset_max | no | (-right, -top) |

### Anchor presets (14)
stretch, center, top-left, top-center, top-right, middle-left, middle-right,
bottom-left, bottom-center, bottom-right, top-stretch, bottom-stretch, left-stretch, right-stretch

## Architecture

### Files
- `UIHelper.cs` (~298 lines) — CreateUI, SetRect, anchor presets, auto-Canvas, TMPro detection
- `CommandRouter.cs` — 2 cases (ExecCreateUI, ExecSetRect)
- `MCPSettings.cs` — create_ui, set_rect in CoreToolNames
- `tools/ui.py` — 2 MCP tools with _RW annotation

### Dependencies
- `ValueParser.ParseVector2()` — (x,y) parsing
- `ValueParser.ParseColor()` — #hex parsing
- `ComponentSerializer.FindObject()` — object lookup
- `ComponentSerializer.GetPath()` — path generation
- `HierarchySerializer.SerializeSubtree()` — subtree output
- `ErrorHelper` — error messages

## Batch example
```
create_ui type=Canvas name=MenuCanvas
create_ui type=Panel name=BG parent=/MenuCanvas color=#000000CC anchor=stretch
create_ui type=Button name=StartBtn parent=/MenuCanvas text=START color=#4CAF50 size=(300,60)
create_ui type=Button name=ExitBtn parent=/MenuCanvas text=EXIT color=#F44336 size=(300,60)
set_rect path=/MenuCanvas/StartBtn anchor=center pos=(0,40)
set_rect path=/MenuCanvas/ExitBtn anchor=center pos=(0,-40)
```

## Tests
- C#: 15 tests in `MCPUITests.cs` (Canvas, Panel, Button, Text, Image, SetRect, errors, batch, play mode guard)
- Python: 8 tests in `test_server_ui.py` (bridge calls, args, errors)

## Related
- Knowledge: `AI/intent-tools.md` (ui_intent DSL tool for layout automation)

---

# Plugin Editor UI Layer (docs-critical-review)

Utilities for building the Biome plugin's own editor windows. Not related to game UI creation above.

## Core Helpers

### BiomeUI (`unity-plugin/Editor/BiomeUI.cs`)
Static building-block factory. All plugin windows use this instead of raw UIElements constructors.

| Method | Returns | Purpose |
|--------|---------|---------|
| `LoadCoreStyles(root, includeWizard)` | void | Attaches MCPHub.uss, MCPSettings.uss, ArcadeAnim.uss (+ optional Wizard USS) |
| `PrimaryButton(text, clicked, tooltip)` | Button | Accented CTA button (`biome-button--primary`) |
| `SecondaryButton(...)` | Button | Secondary action (`biome-button--secondary`) |
| `QuietButton(...)` | Button | Minimal / de-emphasised (`biome-button--quiet`) |
| `Section(title, out body)` | VisualElement | Labelled section container (`biome-section` / `biome-section__body`) |
| `StatusLabel(text)` | Label | Status indicator (`biome-status`) |
| `SetStatus(label, text, state)` | void | Swaps exclusive modifier — states: `neutral`, `success`, `warning`, `error` |
| `SetExclusiveClass(element, active, ...classes)` | void | Toggles one class on, all others off |
| `ShakeX(element)` | void | 5-step shake animation (~180 ms) via scheduled `translate` |

**Motion constants:** `MotionFastMs = 120`, `MotionNormalMs = 220`, `PageMotionMs = 280`.

### BiomeToggleGroup (`unity-plugin/Editor/BiomeToggleGroup.cs`)
Accessible disclosure group with a tri-state master toggle. Replaces the old Foldout+Toggle pattern.

```csharp
var group = new BiomeToggleGroup(
    category: "Scene Tools",
    items: toolNames,
    getValue: name => config.IsEnabled(name),
    setValue: (name, val) => config.SetEnabled(name, val),
    setAll: val => config.SetAllEnabled(val),
    readOnly: false,
    onChanged: SaveAndRefresh);
container.Add(group.Element);
```

- **`Refresh()`** — re-reads all toggle states from `getValue`; call after external config change.
- **`Filter(query)`** — hides rows that don't match (case-insensitive substring). Expands content automatically while filtering.
- Master toggle shows `toggle-mixed` CSS class when partially enabled.
- Does not touch Foldout's internal visual tree — safe against Unity UI Toolkit upgrades.

### BiomeParticleBurst / BiomeAmbientParticles (`unity-plugin/Editor/BiomeParticleBurst.cs`)
Two pooled particle systems, both GPU-friendly (transform/opacity only, no layout mutations).

**BiomeParticleBurst** — one-shot radial burst on user action:
```csharp
BiomeParticleBurst.Emit(hostElement); // pools internally, re-triggers if called again
```
12 pooled particles, 3 colour classes: `biome-particle--accent`, `--success`, `--warning`.

**BiomeAmbientParticles** — continuous ambient loop for active header panels:
```csharp
var ambient = BiomeAmbientParticles.Attach(headerElement, BiomeParticlePattern.DataFlow);
ambient.SetState("up"); // drives conn-up / conn-listen / conn-down CSS class
```
9 particles per host, 8 named patterns: `DataFlow`, `Tools`, `Shield`, `Chat`, `Sampling`, `Updates`, `Ecosystem`, `Timeline`. Scheduler pauses automatically when element detaches.

## USS Files

| File | Lines | Scope |
|------|-------|-------|
| `MCPHub.uss` | 803 | Hub window, cards, nav, particle classes |
| `MCPSettings.uss` | 272 | Settings pages, permission rows, toggle groups |
| `MCPStatus.uss` | 190 | Status indicators, connection state |

Class naming follows BEM: `component`, `component__part`, `component--modifier`.

## Best Practices Reference
`docs/plugins/ui-toolkit-best-practices.md` — authoritative style guide for all plugin editor UI. Key rules:
- Static presentation in USS; inline styles only for runtime data-driven values.
- Build controls through `BiomeUI` / `BiomeToggleGroup` before writing local helpers.
- `UsageHints.DynamicTransform` before animation; schedule on the animated element (not a permanent root).
- No `@keyframes` — use USS transitions or scheduled transform steps.
