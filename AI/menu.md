# Menu & Editor Chrome (Phase 25 + Pass 2)

## Overview

**Phase 25:** MCP tool for executing and listing Unity Editor menu items (MenuItem).

**Pass 2 (2026-06-03):** Flattened the "Tools/Unity Biome MCP" menu → top-level "MCP/" prefix (priority 0=Chat, 1=Status, 2=Settings). Added MCPStatusBarWidget injection into Editor AppStatusBar via reflection. Extracted MCPActions (Restart, Kill, Reimport) as shared utilities.

## Architecture

### Python (server.py)
```python
@mcp.tool(annotations=_RW)
async def menu(action: str, path: str | None = None) -> str
```

### C# (MenuHelper.cs)
- `Execute(path)` — validates existence + enabled → `EditorApplication.ExecuteMenuItem`
- `List(path)` — reflection `Menu.ExtractSubmenus` → text list
- `ListRoots()` — enumerates File/Edit/Assets/GameObject/Component/Window/Help/Tools

### CommandRouter
- `case "menu"` → `ExecMenu(args)` with action switch
- Added to `IsMutatingCommand` (execute can modify scene)

### Menu Structure (2026-06-03)

```
MCP/ [priority 0 → top of Window menu]
├── Chat [priority 0] → MCPChatWindow.ShowWindow()
├── Status [priority 1] → MCPStatusWindow.ShowWindow()
└── Settings [priority 2] → MCPSettings.ShowWindow()

MCP Status Bar Widget
├── Injected into Editor AppStatusBar via reflection
├── Displays state pill: "MCP :{port}" (UP), "MCP ..." (Listen), "MCP off" (Down)
└── Breathing pulse animation (connected=bright, listening=subdued, down=dimmed)
```

### Status Bar Widget (MCPStatusBarWidget.cs)

- **Reflection-based injection:** Finds `AppStatusBar` root VE at startup (delayed via `EditorApplication.delayCall` until panel exists)
- **State polling:** 600ms scheduled tick reads `MCPServer.IsRunning` + `MCPServer.IsClientConnected`
- **Dynamic label:** Maps state → pill text via MCPStatusModel.GetPill()
- **Breathing animation:** Opacity toggled every 600ms (up=0.35↔1.0, listen=0.55↔0.85, down=steady 0.55)
- **Fully defensive:** Try/catch at every reflection step; if AppStatusBar unavailable, retries; logs warnings but never crashes

## API

### Actions
| Action | Args | Description |
|--------|------|-------------|
| `execute` | `path` (required) | Run menu item by full path |
| `list` | `path` (optional) | List sub-items; omit for all roots |

### Editor Command (Editor State & Control)

Python-side `editor` command (wraps EditorStateHelper.cs methods):
| Action | Args | Description |
|--------|------|-------------|
| `state` | none | Get editor state (playing, paused, compiling, scene, dirty, selected, prefab stage) |
| `play` | none | Start play mode |
| `pause` | none | Toggle pause |
| `stop` | none | Exit play mode |
| `select` | `path` (required) | Set active selection to GameObject path |
| `project_path` | none | Get project root directory path |

### Examples
```
menu action=execute path="GameObject/3D Object/Cube"
menu action=execute path="Window/General/Console"
menu action=list path="GameObject"
menu action=list  # lists all root menus
```

## Known Limitations
- `Edit/` menu items not supported by Unity API (long-standing bug since 2011)
- Menu items with validation functions may fail if context requirements not met
- `execute` may open dialogs (Build Settings, Project Settings, etc.)

## Unity Internal API (via reflection)
- `Menu.ExtractSubmenus(path)` — returns `string[]` of sub-items
- `Menu.MenuItemExists(path)` — returns `bool`
- `Menu.GetEnabled(path)` — public API, checks if item is enabled

## Tests
- Python: `server/tests/test_server_menu.py` (8 tests)
- C#: `unity-test-project/Assets/Tests/Editor/MCPMenuTests.cs` (10 tests)

## Static Unity Menu Items

**MCPActions.cs** provides shared static methods used by status window and status bar widget:
- `Restart()` — Stop + StartAsync
- `Kill()` / `KillAll()` — Kill MCP server process(es) via lockfile PID
- `Reimport()` — Force plugin reimport + recompile (finds com.unity-biome-mcp.editor asmdef)

These are invoked directly from editor UI without going through MCP protocol.

## Senior Developer Notes
- Reflection cached in static constructor for performance
- Graceful fallback when internal APIs unavailable
- `Debug.LogWarning` on startup if reflection fails
- MCPActions used for UI-driven restarts (not MCP tool-invoked)
