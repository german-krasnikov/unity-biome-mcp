# Plugin API Reference

API_VERSION: **1**

## Python Side (`unity_mcp.plugin_api`)

Import ONLY from `plugin_api` — never from `unity_mcp.tools._annotations` or `unity_mcp.tools.gating` directly.

### Entry Point: `register(mcp, send_fn, args_fn)`

Every plugin module must export this function. Called once at server startup.

| Param | Type | Description |
|-------|------|-------------|
| `mcp` | FastMCP | Call `@mcp.tool()` to register tools |
| `send_fn` | `async (cmd, args, timeout?) -> str` | Send command to Unity via TCP bridge |
| `args_fn` | `(**kwargs) -> dict` | Build args dict, drops None values |

### Tool Annotations

```python
from unity_mcp.plugin_api import RO, RW, RW_IDEM, DEL
```

| Constant | Meaning | Use for |
|----------|---------|---------|
| `RO` | Read-only | Queries: get_component, inspect |
| `RW` | Read-write | Mutations: create_object, batch |
| `RW_IDEM` | Idempotent write | Set operations: set_property |
| `DEL` | Destructive | Deletions: delete_object |

### `register_tools(category, tools)`

Register tools into capability gating.

```python
register_tools("my_category", {"tool_a", "tool_b"})
```

- `category: str` — gating category name
- `tools: set[str]` — tool names

**Auto-gating:** If a plugin uses `@mcp.tool()` without calling `register_tools()`, those tools are automatically enrolled into the hidden `"plugins"` category (visible only via `discover_tools(category="plugins")`). This prevents undeclared plugin tools from cluttering the default Tier1 budget.

### `register_read_cmds(*names)`

Mark C# commands as read-only for middleware classification.

### `register_write_cmds(*names)`

Mark C# commands as mutating for middleware.

### `register_dsl_tools(*names)`

Mark tools that need Python-side DSL expansion. Blocks these from `batch()`.

### `register_features(features)`

Register feature metadata for token budget system.

```python
register_features({"my_feature": {"priority": "low", "difficulty": 0.3, "est_in": 200, "est_out": 100, "image": False}})
```

### `API_VERSION`

Integer. Current: 1. Set `REQUIRED_API_VERSION` in your module to enforce minimum.

```python
REQUIRED_API_VERSION = 1  # module-level, checked before register() is called
```

---

## C# Side

### `IMCPPlugin` Interface

```csharp
public interface IMCPPlugin
{
    string Name { get; }              // Unique plugin identifier
    string CommandPrefix { get; }     // Prefix for filtering (e.g. "myext_")
    void RegisterCommands();          // Called on registration + domain reload
    void OnDomainReload();            // Cleanup hook on script recompile
    
    // Optional: Register additional commands beyond CommandPrefix matching
    IReadOnlyList<string> AdditionalCommands => Array.Empty<string>();
    
    // Optional: Organize tools into subcategories (v0.56.0+)
    string GetToolSubcategory(string command) => null;  // Return subcategory string or null
    
    // Optional: Settings UI for the Plugins settings page
    VisualElement BuildSettingsUI() => null;  // Return null to skip
    bool HasSettingsUI => false;              // Override alongside BuildSettingsUI
    
    // Optional: Short description for the plugin card
    string Description => "";
}
```

**Subcategories:** Tools can be organized in the UI by returning a category string like `"Animation"` or `"Physics"`. Return `null` or empty string for top-level placement.

**Settings UI:** Implement `BuildSettingsUI()` and set `HasSettingsUI => true` to show a per-plugin settings panel on the Plugins settings page.

**Tool Grouping:** The `PluginToolGrouping` table allows fine-grained organization of tool visibility per subcategory.

### `PluginRegistry`

```csharp
PluginRegistry.Register(new MyPlugin());  // [InitializeOnLoad] static ctor
```

Idempotent by `Name` — duplicate registrations silently ignored.

### `CommandRegistry`

```csharp
// Read-only command
CommandRegistry.Register("my_query", handler);

// Mutating command
CommandRegistry.Register("my_mutate", handler, mutating: true);

// Runtime-only command (Play Mode)
CommandRegistry.Register("my_runtime", handler, runtime: true);
```

Handler signature: `Func<string, string>` — receives JSON args string, returns response string.

### `JsonHelper`

Public methods available to plugins:

```csharp
// Parse incoming args (public)
var value = JsonHelper.ExtractString(args, "param_name");
var unescaped = JsonHelper.UnescapeJsonString(raw);
```

`FormatResponse`, `EscapeJson`, `ExtractObject`, `ExtractArray`, and the `Format*` family are `internal` — not callable from external assemblies. Return plain text from your handler; the framework wraps it in the wire protocol automatically:

```csharp
CommandRegistry.Register("my_query", args =>
{
    var filter = JsonHelper.ExtractString(args, "name_filter");
    return $"count: {SomeLogic(filter)}";  // plain string, no manual JSON wrapping needed
});
```

---

## Plugin Discovery (3 sources, in order)

1. **Built-in** — `server/src/unity_mcp/plugins/` (pkgutil scan, internal only)
2. **Entry points** — `[project.entry-points."unity_mcp.plugins"]` in pyproject.toml
3. **UNITY_MCP_PLUGIN_DIRS** — env var, OS pathsep-separated directories

## Environment Variables

| Variable | Effect |
|----------|--------|
| `UNITY_MCP_SKIP_PLUGINS` | Comma-separated prefixes to skip |
| `UNITY_MCP_PLUGIN_DIRS` | Additional plugin directories |

## Plugin Lifecycle

```
Server startup
  └─ load_plugins(mcp, send_fn, args_fn)
       ├─ 1. Built-in (pkgutil)
       ├─ 2. Entry points (pip packages)
       └─ 3. UNITY_MCP_PLUGIN_DIRS
            └─ For each: import → check API_VERSION → register()

Unity domain reload
  └─ PluginRegistry.OnDomainReload()
       └─ For each: OnDomainReload() → RegisterCommands()
```

## Error Handling

- Python: exceptions in `register()` caught, logged, plugin skipped — server continues
- C#: duplicate `Name` silently ignored (idempotent by design)
- Version mismatch: `REQUIRED_API_VERSION > API_VERSION` → plugin skipped with warning

## Assembly Setup (C#)

Extension code MUST NOT have its own `.asmdef` if it needs to reference types from `Assembly-CSharp` (e.g. custom gameplay types). Place files in `Assets/` (not `Assets/Plugins/`) to compile into `Assembly-CSharp-Editor`.

`UnityMCP.Editor` asmdef has `autoReferenced: true` — your code sees it automatically.
