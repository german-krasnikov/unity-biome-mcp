# Shader & Material Tools

Inspect, create, and modify shader assets, materials, and Shader Graph networks. Use these tools for shader development, material configuration, and visual effects.

## shader

Read or write shader assets (.shader / .shadergraph). Inspect shader properties and keywords, create new shaders from presets or raw HLSL, or edit Shader Graph node networks.

**Parameters:**
- `action` (string) — "get" | "create" | "set" | "graph_get" | "graph_create" | "graph_node" | "graph_edge"
- `path` (string) — Shader asset path (Assets/...)
- `target` (string, optional) — Shader compilation target
- `preset` (string, optional) — Shader preset: "unlit" | "lit" | "transparent"
- `code` (string, optional) — Raw HLSL shader code (used with create)
- `shader_name` (string, optional) — Shader name identifier
- `prop` (string, optional) — Property name (for set action)
- `value` (string, optional) — Property value
- `keyword` (string, optional) — Shader keyword name
- `enabled` (string, optional) — Keyword enabled state
- `node_type` (string, optional) — Shader Graph node type
- `node_id` (string, optional) — Shader Graph node ID
- `node_action` (string, optional) — Node action: "add" | "remove" | "configure"
- `output_node` (string, optional) — Output node ID (for edge)
- `output_slot` (int, optional) — Output slot index (for edge)
- `input_node` (string, optional) — Input node ID (for edge)
- `input_slot` (int, optional) — Input slot index (for edge)
- `edge_action` (string, optional) — Edge action: "connect" | "disconnect"
- `name` (string, optional) — Property name (for graph_node configure)
- `type` (string, optional) — Property type (for graph_node)
- `default_value` (string, optional) — Default value (for graph_node property)
- `reference_name` (string, optional) — Shader reference name (for graph_node property)
- `new_name` (string, optional) — New name for rename operations

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| get | Inspect shader properties and keywords | path | `shader("get", path="Assets/Shaders/MyShader.shader")` |
| create | New shader from preset or code | path, preset OR code | `shader("create", path="Assets/Shaders/Custom.shader", preset="lit")` |
| set | Change property or keyword | path, (prop+value) OR (keyword+enabled) | `shader("set", path="Assets/Shaders/MyShader.shader", prop="_Color", value="#FF0000")` |
| graph_get | Read Shader Graph nodes/edges | path | `shader("graph_get", path="Assets/Shaders/MyGraph.shadergraph")` |
| graph_create | New .shadergraph | path | `shader("graph_create", path="Assets/Shaders/NewGraph.shadergraph")` |
| graph_node | Add/remove/configure node | path, node_type, node_id, node_action | `shader("graph_node", path="Assets/Shaders/MyGraph.shadergraph", node_type="ColorNode", node_id="node_1", node_action="add")` |
| graph_edge | Connect/disconnect slots | path, output_node, output_slot, input_node, input_slot, edge_action | `shader("graph_edge", path="Assets/Shaders/MyGraph.shadergraph", output_node="node_1", output_slot=0, input_node="node_2", input_slot=0, edge_action="connect")` |

**Example:**

```python
# Inspect shader
info = await shader("get", path="Assets/Shaders/Standard.shader")

# Create new shader from preset
await shader("create", path="Assets/Shaders/MyUnlit.shader", preset="unlit")

# Modify shader property
await shader("set", path="Assets/Shaders/MyShader.shader", 
            prop="_MainColor", value="#FF5500")

# Enable keyword
await shader("set", path="Assets/Shaders/MyShader.shader",
            keyword="USE_NORMALMAP", enabled="true")

# Create Shader Graph
await shader("graph_create", path="Assets/Shaders/MyGraph.shadergraph")

# Add node
await shader("graph_node", path="Assets/Shaders/MyGraph.shadergraph",
            node_type="ColorNode", node_id="node_1", node_action="add")

# Connect nodes
await shader("graph_edge", path="Assets/Shaders/MyGraph.shadergraph",
            output_node="node_1", output_slot=0,
            input_node="node_2", input_slot=0,
            edge_action="connect")
```

**Use Cases:**
- Inspect built-in shader properties and keywords
- Create custom shaders without manual file editing
- Build Shader Graph networks visually
- Modify material shader assignments via `material` tool

**Note:** For material shader assignment (applying a shader to a scene material), use `material` tool instead.

---

## material

Create and configure materials. See [Asset Tools — material](assets.md#material) for full documentation.

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Inspect shader properties | shader("get", path) | `await shader("get", path="Assets/Shaders/Standard.shader")` |
| Create custom shader | shader("create", path, preset) | `await shader("create", path="Assets/Shaders/Custom.shader", preset="unlit")` |
| Create material with shader | material("create", path, shader) | `await material("create", path="Assets/Materials/New.mat", shader="Standard")` |
| Change material color | material("set", path, prop, value) | `await material("set", path="Assets/Materials/Player.mat", prop="_Color", value="#FF0000")` |
| Apply material to scene object | material("copy", source, targets) | `await material("copy", source="Assets/Materials/Base.mat", targets="Player")` |
| Build Shader Graph | shader("graph_create") → shader("graph_node") → shader("graph_edge") | Sequential node/edge operations |

---

**See also:** [Assets Tools](assets.md) for material asset management, [Objects Tools](objects.md) for `set_material` quick helper.
