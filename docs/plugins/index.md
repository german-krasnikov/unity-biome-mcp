# Create Your First Unity Biome MCP Plugin

## Prerequisites

- unity-biome-mcp installed (pip + UPM)
- Python 3.10+, Unity 6000.0+

## 1. Scaffold (Python side)

```
my-unity-plugin/
  python/
    pyproject.toml
    src/my_plugin/
      plugins/
        __init__.py
        my_tools.py
  unity/
    Editor/
      MyMCPPlugin.cs
```

### pyproject.toml

```toml
[project]
name = "my-unity-plugin"
version = "0.1.0"
dependencies = ["unity-biome-mcp"]

[project.entry-points."unity_mcp.plugins"]
my_tools = "my_plugin.plugins.my_tools"
```

### my_tools.py

```python
from unity_mcp.plugin_api import RO, register_tools

_MY_TOOLS = {"my_count_objects"}

def register(mcp, send, args):
    @mcp.tool(annotations=RO)
    async def my_count_objects(name_filter: str = "") -> str:
        """Count GameObjects matching a name filter."""
        return await send("my_count_objects", args(name_filter=name_filter))

    register_tools("my_plugin", _MY_TOOLS)
```

## 2. Scaffold (C# side)

### MyMCPPlugin.cs

```csharp
using UnityEditor;
using UnityMCP.Editor;

namespace MyPlugin.Editor
{
    [InitializeOnLoad]
    public class MyMCPPlugin : IMCPPlugin
    {
        public string Name => "MyPlugin";
        public string CommandPrefix => "my_";

        static MyMCPPlugin() => PluginRegistry.Register(new MyMCPPlugin());

        public void RegisterCommands()
        {
            CommandRegistry.Register("my_count_objects", args =>
            {
                var filter = JsonHelper.ExtractString(args, "name_filter");
                var objects = string.IsNullOrEmpty(filter)
                    ? UnityEngine.Object.FindObjectsOfType<UnityEngine.GameObject>()
                    : System.Array.FindAll(
                        UnityEngine.Object.FindObjectsOfType<UnityEngine.GameObject>(),
                        go => go.name.Contains(filter));
                // Build response manually — JsonHelper.FormatResponse is internal.
                // Return plain text; the framework wraps it in the wire protocol.
                return $"count: {objects.Length}";
            });
        }

        public void OnDomainReload() { }

        // Organize tools into subcategories (v0.56.0+)
        public string GetToolSubcategory(string toolName)
        {
            return toolName switch
            {
                "my_count_objects" => "Scene",
                _ => null  // Top-level placement
            };
        }
    }
}
```

## 3. Install and Test

```bash
# Python (make it discoverable)
cd my-unity-plugin/python && pip install -e .

# C# — symlink into game project (Unix/macOS)
ln -s /path/to/my-unity-plugin/unity/Editor /path/to/game-project/Assets/MyPlugin/Editor

# Windows: use mklink (run as Administrator in cmd.exe)
# mklink /D "C:\path\to\game-project\Assets\MyPlugin\Editor" "C:\path\to\my-unity-plugin\unity\Editor"
# Or just copy the Editor folder directly instead of symlinking.

# Force MCP server reload
# Open MCP > Status in Unity and select Restart, then restart the Claude Code CLI session
```

**Plugin Discovery:** Plugins are discovered from 3 sources in order: (1) built-in plugins, (2) pip entry points in `pyproject.toml`, (3) `UNITY_MCP_PLUGIN_DIRS` environment variable. Restart the MCP server process after installing or changing a Python plugin.

**Important:** Always call `register_tools()` to declare your plugin's tools. If you use `@mcp.tool()` without declaring the tool via `register_tools()`, it will be automatically hidden in the `"plugins"` category (visible only via `discover_tools(category="plugins")`). This prevents undeclared tools from cluttering the budget.

From any connected MCP client, call `my_count_objects`. A successful response
contains `count: <number>`. If the tool does not appear, open
**MCP > Status > Diagnose**.

## 4. Testing Your Plugin

```python
# tests/test_my_tools.py
import pytest
from unittest.mock import MagicMock, AsyncMock

@pytest.mark.asyncio
async def test_count_objects():
    mcp = MagicMock()
    tools = {}
    mcp.tool = lambda **kw: lambda fn: tools.update({fn.__name__: fn}) or fn

    send = AsyncMock(return_value="count: 42")
    args = lambda **kw: {k: v for k, v in kw.items() if v is not None}

    from my_plugin.plugins.my_tools import register
    register(mcp, send, args)

    result = await tools["my_count_objects"](name_filter="Player")
    send.assert_awaited_once_with("my_count_objects", {"name_filter": "Player"})
    assert "42" in result
```

## 5. Distribution

| Method | Command | When |
|--------|---------|------|
| Dev (editable) | `pip install -e .` | Local development |
| Git | `pip install git+https://...` | Team sharing |
| Local dir | `UNITY_MCP_PLUGIN_DIRS=/path` | Quick prototyping |
| Skip | `UNITY_MCP_SKIP_PLUGINS=my_` | Disable temporarily |

## Next Steps

- [Plugin API Reference](api-reference.md) — full API surface
- For a new command checklist: register C# handler → register Python tool → add test → verify with `my_count_objects` in Claude Code
