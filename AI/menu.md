# Menu and Editor Chrome

## Overview

The `menu` MCP tool lists and executes Unity Editor menu items. The plugin also
adds its own Editor windows and a status-bar control; those UI entry points call
shared `MCPActions` directly rather than routing back through MCP.

## Architecture

### Python (`server/src/unity_mcp/tools/ui.py`)
```python
@mcp.tool(annotations=_RW)
async def menu(action: str, path: str | None = None) -> str:
    ...
```

### C# (MenuHelper.cs)
- `Execute(path)` — validates existence + enabled → `EditorApplication.ExecuteMenuItem`
- `List(path)` — reflection `Menu.ExtractSubmenus` → text list
- `ListRoots()` — enumerates File/Edit/Assets/GameObject/Component/Window/Help/Tools

### CommandRouter
- `case "menu"` → `ExecMenu(args)` with action switch
- Added to `IsMutatingCommand` (execute can modify scene)

### Plugin Menu

Plugin windows are registered under the top-level `🧬MCP/` menu. The exact
entries are intentionally not duplicated here; use `menu(action="list",
path="🧬MCP")` or inspect `[MenuItem]` declarations for the current catalog.
Window titles and the status pill use `BiomeLabel.DisplayName`, which is either
the emoji or `Biome` according to the Editor preference.

### Status Bar Widget (MCPStatusBarWidget.cs)

- **Reflection-based injection:** Finds `AppStatusBar` root VE at startup (delayed via `EditorApplication.delayCall` until panel exists)
- **State polling:** 900 ms scheduled tick reads server, client, and chat-backend state
- **Dynamic label:** Maps state → pill text via MCPStatusModel.GetPill()
- **Breathing animation:** Dot/halo scale and opacity reflect connected, listening, chat-active, and down states
- **Click menu:** Restart server/relay, reimport, kill current/all/phantom servers, or open Status
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
| `select` | `path` or comma-separated `paths` | Set one or more selected GameObjects |
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
- C#: `unity-plugin/Editor/Tests/BiomeMenuPathTests.cs` covers label/status behavior

## Static Unity Menu Items

**MCPActions.cs** provides shared static methods used by the status window and
status-bar widget:
- `Restart()` — Stop + StartAsync
- `RestartRelay()` — restart the chat relay
- `KillCurrent()` / `KillAll()` — kill MCP server process(es) via lockfile PID
- `KillByPort()` / `TerminateByPid()` / `StopAllOnPort()` — targeted multi-server cleanup
- `Reimport()` — Force plugin reimport + recompile (finds com.unity-biome-mcp.editor asmdef)

These are invoked directly from editor UI without going through MCP protocol.

## Senior Developer Notes
- Reflection cached in static constructor for performance
- Graceful fallback when internal APIs unavailable
- `Debug.LogWarning` on startup if reflection fails
- MCPActions used for UI-driven restarts (not MCP tool-invoked)

## Related

- [`AI/tools-reference.md`](tools-reference.md) — public tool discovery and schemas
- [`AI/testing.md`](testing.md) — current verification policy
