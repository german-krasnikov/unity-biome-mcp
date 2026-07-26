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
