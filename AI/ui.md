# UI Systems (Phase 15+)

## Overview
Three UI subsystems available:

1. **uGUI (Canvas-based)** — Traditional UI: `create_ui`, `set_rect`, `lint_ugui`, `list_events`, `ui_intent`
2. **UI Toolkit (UXML/USS)** — Modern declarative UI: `inspect_uitk`, `lint_uitk`, `uitk_element`, `attach_uitk`, `uitk_file`, `uitk_intent`
3. **Editor Menus** — Editor API: `menu` (list/execute)

## uGUI (Canvas-based UI)

### Commands

#### create_ui
Creates Canvas or UI elements with smart defaults.

| Param | Req | Description |
|-------|-----|-------------|
| type | yes | Canvas, Panel, Button, Text, Image, Toggle, Slider, InputField, ScrollView |
| name | no | GO name (default = type) |
| parent | no | path to parent |
| anchor | no | preset name (14 options) |
| pos | no | anchoredPosition (x,y) |
| size | no | sizeDelta (w,h) |
| pivot | no | pivot (x,y) |
| color | no | hex #RRGGBB or #RRGGBBAA |
| text | no | text for Text/Button/InputField |
| font_size | no | font size (points) |
| render_mode | no | Canvas render mode: SSO (ScreenSpaceOverlay, default) \| SSC (ScreenSpaceCamera) \| WorldSpace |
| font_min | no | TextMeshPro minimum font size (enables auto-sizing for Text) |
| font_max | no | TextMeshPro maximum font size (enables auto-sizing for Text) |

**Type behaviors:**
- Canvas: CanvasScaler(1920x1080), GraphicRaycaster, auto EventSystem
- Panel: Image, anchor=stretch
- Button: Button + Image + child Text, anchor=center, size=(160,30)
- Text: TMPro.TextMeshProUGUI, anchor=center, size=(200,50)
- Image: Image, anchor=center, size=(100,100)
- Toggle: Toggle + child Text, anchor=center, size=(200,30)
- Slider: Slider, anchor=center, size=(200,30)
- InputField: InputField + child Text, anchor=center, size=(300,40)
- ScrollView: ScrollView + Viewport + Content, anchor=stretch

**Auto-Canvas:** No parent → finds Canvas in scene, creates if missing.

#### set_rect
Fast RectTransform configuration.

| Param | Req | Description |
|-------|-----|-------------|
| path | yes | object path |
| anchor | no | preset name |
| pos | no | anchoredPosition (x,y) |
| pos3 | no | position with z (x,y,z) for WorldSpace Canvas |
| size | no | sizeDelta (w,h) |
| pivot | no | pivot (x,y) |
| offset_min | no | (left, bottom) |
| offset_max | no | (-right, -top) |

**Anchor presets (14):** stretch, center, top-left, top-center, top-right, middle-left, middle-right, bottom-left, bottom-center, bottom-right, top-stretch, bottom-stretch, left-stretch, right-stretch

#### lint_ugui
Validate Canvas setup: EventSystem, Button listeners, constraint mismatches.

| Param | Req | Description |
|-------|-----|-------------|
| root | no | Root path to scan (default: "/" = whole scene) |

Returns: Issues list or "OK".

#### list_events
List UnityEvent persistent listeners on a component.

| Param | Req | Description |
|-------|-----|-------------|
| path | yes | Scene path to GameObject |
| component | yes | Component type (e.g., "Button", "Toggle") |
| event | yes | Event name (e.g., "onClick") |

Returns: Array of listeners with target, method, parameters.

#### ui_intent
Natural language → UI hierarchy. Generates batch `create_ui` commands.

| Param | Req | Description |
|-------|-----|-------------|
| intent | yes | Natural language description |
| parent | no | Parent path (default: new Canvas) |
| template | no | Preset: hud \| menu \| dialog \| grid |
| dry_run | no | Return DSL without executing (default: false) |

### Architecture

**Files:**
- `UIHelper.cs` — CreateUI, SetRect, anchor presets, auto-Canvas, TMPro detection, new type support (Toggle, Slider, etc.)
- `UIHelper.UIToolkit.cs` — UITOOLKIT-specific helpers (NEW)
- `CommandRouter.MediaHandlers.cs` (partial) → split to `CommandRouter.UIHandlers.cs`
- `MCPSettings.cs` — Tool category registration (UGUI/UITOOLKIT split)
- `tools/ui.py` — uGUI tools: create_ui, set_rect, lint_ugui, list_events, ui_intent, menu
- `tools/ui_intent_tool.py` — Intent pipeline for uGUI DSL generation

**Dependencies:**
- `ValueParser.ParseVector2()` → Parse(x,y)
- `ValueParser.ParseColor()` → Parse #hex
- `ComponentSerializer` → FindObject, GetPath
- `EventSystem.current` → EventSystem singleton

### Batch example
```
create_ui type=Canvas name=MenuCanvas render_mode=SSC
create_ui type=Panel name=BG parent=/MenuCanvas color=#000000CC anchor=stretch
create_ui type=Button name=StartBtn parent=/MenuCanvas text=START color=#4CAF50
create_ui type=Toggle name=Sound parent=/MenuCanvas text=Sound
set_rect path=/MenuCanvas/StartBtn anchor=center pos=(0,40) size=(200,60)
lint_ugui root=/MenuCanvas
list_events path=/MenuCanvas/StartBtn component=Button event=onClick
```

## UI Toolkit (UXML/USS)

### Commands

#### inspect_uitk
Inspect VisualElement tree in a UIDocument.

| Param | Req | Description |
|-------|-----|-------------|
| path | no | Scene path to UIDocument GO (e.g., /HUD), or "scene" to list all |
| depth | no | Max depth (default: 4) |
| selector | no | Start from matching element (.class, TypeName, ~refid, name) |
| filter | no | Show only elements matching substring |
| show_unity_private | no | Include #unity-* elements (default: false) |
| show_style | no | Include computed CSS values (default: false) |

Returns: Compact text tree with ~N refids.

#### lint_uitk
Validate UXML/USS file structure and references.

| Param | Req | Description |
|-------|-----|-------------|
| path | yes | Assets/ path to UXML or USS file |
| fix | no | Auto-normalize format (default: false) |

Checks: XML well-formedness, broken <Style src>, missing <Template src>, CamelCase classes (should be kebab-case), star selectors, duplicate CSS variables.

#### uitk_element
Query or mutate a VisualElement.

| Param | Req | Description |
|-------|-----|-------------|
| action | yes | query \| get \| set_style \| add_class \| remove_class \| get_style \| enable \| disable |
| path | no | Scene path to UIDocument GO |
| ref | no | ~N refid (highest priority; stale after re-inspect) |
| selector | no | CSS selector (.class, TypeName, name) |
| name | no | Element name (bare selector) |
| value | no | CSS value for set_style/add_class |
| property | no | CSS property name |
| class_name | no | USS class name (no leading dot) |

**Actions:**
- query: Find matching elements
- get: Read text/value
- set_style: Write CSS property
- add_class: Add USS class
- remove_class: Remove USS class
- get_style: Read computed CSS
- enable: Show element
- disable: Hide element

Note: Mutations in Play Mode are not persisted.

#### attach_uitk
Attach a UXML file to a GameObject.

| Param | Req | Description |
|-------|-----|-------------|
| path | yes | Scene path to target GO |
| uxml_path | yes | Assets/ path to UXML file |
| sort_order | no | Panel layer order (default: 0) |

#### uitk_file
Create or modify UXML/USS files.

| Param | Req | Description |
|-------|-----|-------------|
| action | yes | create_uxml \| create_uss \| create_style_sheet \| add_element \| add_style |
| path | yes | Assets/ destination path |
| name | no | Element/class name |
| template | no | Template name |

#### uitk_intent
Natural language → UXML/USS code.

| Param | Req | Description |
|-------|-----|-------------|
| intent | yes | Natural language description |
| style | no | Generate USS stylesheet (default: false) |
| dry_run | no | Return code without writing (default: false) |

### Architecture

**Files:**
- `UIElementSerializer.cs` — VisualElement → compact text output (NEW)
- `UIElementHelper.cs` — Element querying, styling, class manipulation (NEW)
- `UIFileHelper.cs` — UXML/USS parsing and validation (NEW)
- `UIFileHelper.Uxml.cs` — UXML-specific helpers (NEW)
- `UIFileHelper.Uss.cs` — USS-specific helpers (NEW)
- `UILinter.cs` — UXML/USS linter implementation (NEW)
- `CommandRouter.UIToolkitHandlers.cs` — Handlers for UITOOLKIT tools (NEW)
- `tools/uitk.py` — UITOOLKIT tools (NEW)
- `tools/uitk_intent_tool.py` — Intent pipeline for UXML/USS generation (NEW)

**UIDocument resolution:** 4-segment addressing (v0.89.0+):
- Path-based: `/HUD` (UIDocument GameObject)
- Refid: `~42` (compact element reference from inspect_uitk)
- Selector: `.panel__header` or `HeaderLabel` (CSS or element name)
- Hybrid in playtest DSL: `VAL $hud /HUD|UIDocument` then `$hud|label|text`

### Batch example (pseudo — not yet full batch support for UITOOLKIT)
```
inspect_uitk path=/HUD depth=5
lint_uitk path="Assets/UI/HUD.uxml"
uitk_element action=query path=/HUD selector=".health-label"
uitk_element action=set_style path=/HUD ref=~3 property=width value=50%
```

## Playtest DSL Extensions (UIDocument)

FILL, FOCUS steps + 4-segment addressing:

```
VAL $hud /Player/HUD|UIDocument
FILL $hud|input-name "Player1"
FOCUS $hud|input-name
ASSERT $hud|score-label|text == "0"
```

## Tests
- C# uGUI: `unity-test-project/Assets/Tests/Editor/UIHelperTests.cs`, `UIHelperNewTypesTests.cs`
- C# UITOOLKIT: `unity-test-project/Assets/Tests/Editor/UIToolkit/*.cs` (NEW)
- Python uGUI: `server/tests/test_server_ui.py`, `test_create_ui_render_mode.py`
- Python UITOOLKIT: `server/tests/test_uitk.py`, `test_uitk_element.py`, `test_uitk_file_tool.py`, `test_uitk_intent.py` (NEW)

## Related
- Knowledge: `AI/intent-tools.md` (intent tools overview)
- ClientSkills: `unity-plugin/ClientSkills/skills/unity-ugui-authoring/SKILL.md` (uGUI workflow)
- ClientSkills: `unity-plugin/ClientSkills/skills/unity-uitoolkit-authoring/SKILL.md` (UI Toolkit workflow)

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
