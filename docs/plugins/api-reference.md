# Plugin API Reference

Current Python plugin API version: `1`.

Import supported Python extension points from `unity_mcp.plugin_api`. Import
Unity extension points from the public `UnityMCP.Editor` assembly. Internal
modules and internal C# members can change without compatibility guarantees.

## Python API

### Entry Point

An external distribution declares an entry in
`[project.entry-points."unity_mcp.plugins"]`. The loaded module must expose:

```python
def register(mcp, send_fn, args_fn):
    ...
```

| Argument | Contract |
|---|---|
| `mcp` | FastMCP instance; use `@mcp.tool(...)` to define a tool |
| `send_fn` | Async callable: `send_fn(command, arguments, timeout=0) -> str`; `0` selects the command default |
| `args_fn` | Keyword argument builder that removes values set to `None` |

The server calls `register` once while loading the plugin process. An exception
is logged and skips that plugin without stopping the server.

### Tool Annotations

```python
from unity_mcp.plugin_api import DEL, RO, RW, RW_IDEM
```

| Constant | Use |
|---|---|
| `RO` | Read-only inspection |
| `RW` | Mutation with side effects |
| `RW_IDEM` | Mutation that can safely set the same value again |
| `DEL` | Destructive removal |

Annotations describe the MCP tool to clients. They do not replace middleware
classification of the forwarded Unity command.

### Capability and Middleware Registration

```python
from unity_mcp.plugin_api import (
    register_dsl_tools,
    register_read_cmds,
    register_tools,
    register_write_cmds,
)

register_tools("my_plugin", {"my_query", "my_update"})
register_read_cmds("my_query")
register_write_cmds("my_update")
```

- `register_tools(category, tools)` adds tool names to a capability category.
- `register_read_cmds(*names)` classifies forwarded Unity commands as
  read-only.
- `register_write_cmds(*names)` classifies forwarded Unity commands as
  mutating.
- `register_dsl_tools(*names)` marks tools that expand a Python-side DSL and
  therefore cannot be nested in `batch`.

Call `register_tools` for every plugin tool. If a plugin omits it, newly
registered names are placed in the hidden `plugins` category and remain outside
the default tool budget until a client calls
`discover_tools(category="plugins")`.

### Feature Metadata

`register_features` adds budget metadata for an optional model-backed feature:

```python
from unity_mcp.plugin_api import register_features

register_features({
    "my_summary": {
        "priority": "low",
        "difficulty": 0.3,
        "est_in": 200,
        "est_out": 100,
        "image": False,
    }
})
```

`priority` is `critical`, `medium`, or `low`; `difficulty` is from `0.0` to
`1.0`; token estimates are integers; and `image` records whether the feature
adds image input.

### Text and Sampling Helpers

The public module also exports:

```python
from unity_mcp.plugin_api import SamplingService, sanitize_intent, strip_fences
```

- `strip_fences(text)` removes one outer Markdown code fence.
- `sanitize_intent(text, max_len=500)` caps input and removes newlines and
  braces before an intent is inserted into a prompt.
- `SamplingService` exposes the server's optional generation, summary, and
  visual-verification methods.

`SamplingService` is disabled unless the server starts with
`UNITY_MCP_VISUAL_VERIFY=1`. In this release it executes Claude CLI profiles
only; unsupported configured providers fail closed. A plugin should degrade
cleanly when a sampling method returns `None`.

### API Version Check

An external module can require a minimum API:

```python
REQUIRED_API_VERSION = 1
```

If `REQUIRED_API_VERSION` is greater than `plugin_api.API_VERSION`, the loader
logs a warning and skips the plugin before calling `register`.

### Python Discovery Controls

Plugins load at server startup in this order:

1. built-in modules
2. installed `unity_mcp.plugins` entry points
3. modules found in `UNITY_MCP_PLUGIN_DIRS`

`UNITY_MCP_PLUGIN_DIRS` is separated with the operating system path separator.
`UNITY_MCP_SKIP_PLUGINS` is a comma-separated list of discovery-name prefixes.
Changing either variable requires a new server process.

## Unity C# API

### `IMCPPlugin`

```csharp
public interface IMCPPlugin
{
    string Name { get; }
    string CommandPrefix { get; }
    void RegisterCommands();
    void OnDomainReload();

    IReadOnlyList<string> AdditionalCommands
        => Array.Empty<string>();
    string GetToolSubcategory(string command)
        => null;
    VisualElement BuildSettingsUI()
        => null;
    bool HasSettingsUI
        => false;
    string Description
        => "";
}
```

`Name` is the registry identity. A second registration with the same name is
ignored.

`CommandPrefix` owns the exact command and commands beginning with
`<prefix>_`. Use the canonical form without a trailing underscore, such as
`my`; the legacy `my_` form is normalized for compatibility. Boundary matching
prevents `my` from claiming `myth_query`.

Use `AdditionalCommands` only for commands that cannot share the prefix.
`GetToolSubcategory` controls grouping under **MCP > Settings > Tools**; an empty
value falls back to the plugin name.

To add a settings card, return a `VisualElement`, set `HasSettingsUI` to
`true`, and provide a short `Description`. A plugin without a settings UI can
still register tools.

### Registration Lifecycle

Register the plugin instance from an Editor load hook:

```csharp
[InitializeOnLoad]
public sealed class MyPlugin : IMCPPlugin
{
    static MyPlugin()
    {
        PluginRegistry.Register(new MyPlugin());
    }

    // Interface implementation...
}
```

`PluginRegistry.Register` stores the instance; it does not call
`RegisterCommands`. When the Unity command catalog initializes,
`PluginRegistry.RegisterAllPlugins` calls `RegisterCommands` once for each
registered plugin. After a domain reload, `OnDomainReload` runs as the plugin's
cleanup or recovery hook, and the next command-catalog initialization registers
its commands again.

Exceptions from either lifecycle method are isolated and logged. Failures from
the latest command-registration pass are available through
`PluginRegistry.GetFailedPlugins()` and diagnostics.

### `CommandRegistry`

Register a synchronous command:

```csharp
CommandRegistry.Register(
    "my_query",
    args => "ok",
    mutating: false,
    runtime: false,
    required: "path",
    optional: "include_inactive",
    description: "Inspect project-specific state");
```

Use `RegisterAction` when every request has an `action` field:

```csharp
CommandRegistry.RegisterAction(
    "my_asset",
    (action, args) => action == "refresh" ? "refreshed" : "unknown action",
    mutating: true,
    optional: "path");
```

Use `RegisterAsync` for an asynchronous Unity operation. Its handler is
`Action<string, string, TaskCompletionSource<string>>`, where the first string
is the request ID and the second is the JSON argument object. Complete the
`TaskCompletionSource` on every success and failure path.

Common options:

| Option | Meaning |
|---|---|
| `mutating` | Command changes Unity or project state |
| `runtime` | Command is available only in Play Mode |
| `required` | Comma-separated required argument names |
| `optional` | Comma-separated optional argument names |
| `description` | Help text exposed by the command catalog |
| `maxResponseChars` | Soft response limit; `0` disables it |

`alwaysAllowed` and `allowedDuringCompile` are core-only trust flags. Requests
for them from plugin registration are stripped and logged. Do not use
`specialDispatch` for an external handler: special dispatch requires core
router integration.

A duplicate command keeps the first registration and logs a warning. Use
`CommandRegistry.IsRegistered`, `GetDescription`, and `BuildHelp` for public
read-only inspection.

### `JsonHelper`

Public parsing helpers are:

```csharp
string value = JsonHelper.ExtractString(args, "name");
int count = JsonHelper.ExtractInt(args, "count", 0);
float speed = JsonHelper.ExtractFloat(args, "speed");
string nested = JsonHelper.ExtractObject(args, "options");
string items = JsonHelper.ExtractArray(args, "items");
string text = JsonHelper.UnescapeJsonString(raw);
string array = JsonHelper.BuildJsonStringArray(values);
```

`ExtractObject` and `ExtractArray` return raw JSON (`{}` or `[]` when missing).
`FormatResponse`, `EscapeJson`, and the `Format*` response helpers are internal.
Return command data from the handler; the router constructs the wire response.

## Assembly Placement

For project-specific code that directly references gameplay types, place the
adapter under `Assets/.../Editor/` without a separate assembly definition so it
compiles into `Assembly-CSharp-Editor`.

For a reusable Unity package, create an Editor-only assembly definition with an
explicit `UnityMCP.Editor` reference. A packaged assembly cannot directly
reference types compiled into the consuming project's `Assembly-CSharp`; keep
that boundary behind a project-side adapter or a shared runtime assembly.

Start with the complete [plugin example](index.md), then add focused Python and
Unity EditMode tests before live verification.
