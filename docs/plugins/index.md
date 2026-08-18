# Create a Plugin

A Unity Biome MCP tool has two halves:

- a Python tool that defines the MCP schema and sends a named command
- a Unity Editor handler that receives that command and performs the work

Use a plugin when a project needs a stable domain-specific tool. For one-off
scene changes, use the existing tools or `execute_code` instead.

## Prerequisites

- Unity Biome MCP installed in the target Unity 6 project
- Python 3.14 or newer for plugin development
- an Editor folder or Unity package for the C# integration

## 1. Create the Python Package

```text
my-unity-plugin/
├── python/
│   ├── pyproject.toml
│   └── src/my_plugin/
│       ├── __init__.py
│       └── my_tools.py
└── unity/
    └── Editor/
        └── MyMcpPlugin.cs
```

Declare the server package and plugin entry point:

```toml
[project]
name = "my-unity-plugin"
version = "0.1.0"
requires-python = ">=3.14"
dependencies = ["unity-biome-mcp"]

[project.entry-points."unity_mcp.plugins"]
my_tools = "my_plugin.my_tools"

[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[tool.hatch.build.targets.wheel]
packages = ["src/my_plugin"]
```

Register the MCP tool, its capability category, and its middleware mutability:

```python
from unity_mcp.plugin_api import RO, register_read_cmds, register_tools

_TOOLS = {"my_count_objects"}


def register(mcp, send, args):
    @mcp.tool(annotations=RO)
    async def my_count_objects(name_filter: str = "") -> str:
        """Count loaded GameObjects whose names contain the filter."""
        return await send(
            "my_count_objects",
            args(name_filter=name_filter),
        )

    register_tools("my_plugin", _TOOLS)
    register_read_cmds(*_TOOLS)
```

`register_tools` makes the capability discoverable under the chosen category.
`register_read_cmds` is independent: it tells middleware that forwarding this
Unity command is read-only. Use `register_write_cmds` for mutations.

## 2. Register the Unity Command

Add `MyMcpPlugin.cs` to `Assets/MyPlugin/Editor/` in the target project, or ship
it in an Editor-only assembly that references `UnityMCP.Editor`.

```csharp
using System;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace MyPlugin.Editor
{
    [InitializeOnLoad]
    public sealed class MyMcpPlugin : IMCPPlugin
    {
        static MyMcpPlugin()
        {
            PluginRegistry.Register(new MyMcpPlugin());
        }

        public string Name => "MyPlugin";

        // Canonical form omits the separator. The command boundary is `my_`.
        public string CommandPrefix => "my";

        public void RegisterCommands()
        {
            CommandRegistry.Register("my_count_objects", args =>
            {
                var filter = JsonHelper.ExtractString(args, "name_filter") ?? "";
                var objects = UnityEngine.Object.FindObjectsByType<GameObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                var count = 0;
                foreach (var gameObject in objects)
                {
                    if (gameObject.name.IndexOf(
                            filter,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                    }
                }

                // Return command data. The transport wraps the response.
                return $"count: {count}";
            });
        }

        public void OnDomainReload() { }

        public string GetToolSubcategory(string command)
        {
            return command == "my_count_objects" ? "Scene" : null;
        }
    }
}
```

Use a prefix without a trailing underscore. Legacy prefixes such as `my_` are
still normalized for compatibility, but new plugins should use `my`. Prefix
matching occurs at the command boundary, so `my` owns `my_count_objects` but
does not claim `myth_query`.

## 3. Install and Verify

Install the Python package into the same environment that launches the MCP
server. From the plugin repository root, an editable install is:

```bash
python -m pip install -e ./python
```

That command is sufficient only when the MCP client launches the server with
that Python environment. The standard generated configuration uses an isolated
`uvx` tool environment. Include a released plugin distribution in that launch
with `--with`:

```bash
uvx --with my-unity-plugin \
  --from git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server \
  unity-biome-mcp
```

For a local plugin checkout, replace `--with my-unity-plugin` with
`--with-editable ./python`. A package installed only into an unrelated system
Python environment is not visible to `uvx`.

Then:

1. Copy or package the `unity/Editor` code into the target Unity project.
2. Wait for a clean Unity compile.
3. Open **MCP > Status**, select **Restart**, and restart the external MCP
   client so the Python entry point is loaded again.
4. Ask the client to enable the plugin's custom category.

Then call the tool:

```python
await discover_tools(category="my_plugin", enable=True)
result = await my_count_objects(name_filter="Player")
```

A successful response contains `count: <number>`. If the tool is absent, run
**MCP > Status > Diagnose**, confirm that the Python distribution is installed
in the environment that launches the server, and inspect the Unity Console for
plugin registration errors. Custom plugin categories are hidden until enabled
in each new server session.

## 4. Test the Python Wrapper

```python
from unittest.mock import AsyncMock, MagicMock

import pytest


@pytest.mark.asyncio
async def test_count_objects_forwards_filter():
    registered = {}
    mcp = MagicMock()
    mcp.tool = lambda **_: lambda fn: registered.setdefault(fn.__name__, fn)
    send = AsyncMock(return_value="count: 2")
    args = lambda **values: {
        key: value for key, value in values.items() if value is not None
    }

    from my_plugin.my_tools import register

    register(mcp, send, args)
    result = await registered["my_count_objects"](name_filter="Player")

    send.assert_awaited_once_with(
        "my_count_objects",
        {"name_filter": "Player"},
    )
    assert result == "count: 2"
```

Also add a Unity EditMode test for the handler's filtering behavior. Test the
Python schema and C# operation independently before a live connection test.

## Discovery and Distribution

Python plugins load at server startup. Restart the Python MCP process after
installing or changing one. See
[Python Discovery Controls](api-reference.md#python-discovery-controls) for the
supported sources, load order, and skip behavior.

For a team release, distribute the Python package and the Unity Editor package
at compatible versions. Do not rely on an editable install or a local absolute
path outside development.

## Next Steps

- [Plugin API Reference](api-reference.md) covers annotations, registration,
  command options, lifecycle, and settings UI.
- [Extend Chat Context Chips](../chat/extending-chips.md) adds custom Chat
  context types without creating an MCP tool.
