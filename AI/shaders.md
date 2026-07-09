# Feature: Shader Management (Phase 20)

## Overview
Consolidated `shader` MCP tool with 7 actions for ShaderLab code shaders, Shader Graph, and material property management.

## Architecture (for Architect)
- **ShaderSerializer** — reads shader/material properties via Unity API (`Shader.GetPropertyCount/Name/Type`)
- **ShaderHelper** — creates .shader files from presets (unlit/lit/transparent) or custom code, sets material properties with Undo
- **ShaderGraphHelper** — reads/creates/edits .shadergraph files; uses Unity API for compiled info, raw file parsing for graph structure (no public API exists)
- **CommandRouter** — routes `shader` command to appropriate helper based on `action` field
- All 7 actions consolidated into single MCP tool with 18 parameters

## Implementation Notes (for Developer)
- **Shader Graph has NO public C# API** for graph structure — must parse MultiJson format directly
- **MultiJson format**: multiple JSON blocks separated by `\n\n`, each with `m_Type`, `m_ObjectId`, `m_SGVersion`
  - Example: `{ "m_ObjectId": "12345", "m_Type": "UnityEditor.Graphing.Edge", "m_Properties": {...} }\n\n{ "m_ObjectId": "12346", "m_Type": "UnityEditor.Graphing.Node", ... }`
  - GraphData block is first (type=GraphData), contains arrays of node/edge/property IDs
  - Each node/edge/property is separate JSON block with matching m_ObjectId
- **GraphData root block**: contains `m_Nodes`, `m_Edges`, `m_Properties` arrays referencing other blocks by ID
- `Create` for .shadergraph copies from Unity's package templates (`Library/PackageCache/com.unity.shadergraph*`)
- `AssetDatabase.ImportAsset` wrapped in try/catch for node/edge edits (incomplete slot blocks cause warnings)
- Material property types: Color (`#RRGGBB`), Float, Range, Vector (`(x,y,z,w)`), Texture (asset path), Int
- ShaderLab presets use string concatenation (NOT verbatim `@""` strings — `{{` stays literal there)
- **JSON unescape** (Phase 20f): Custom shader code in `create` action requires JSON unescape (`\"` → `"`, `\\` → `\`, `\n` → newline, etc.) via new `UnescapeJsonString` method in CommandRouter
- **RemoveNode multi-line** (Phase 20f): ShaderGraphHelper.RemoveNode now uses `ParseJsonObjects` to handle multi-line JSON format in .shadergraph files instead of single-line string.Replace

### Actions (Shader Tool)
| Action | Helper | Description |
|--------|--------|-------------|
| get | ShaderSerializer | Read shader properties (from scene object or asset path) |
| create | ShaderHelper | Create .shader from preset or custom code |
| set | ShaderHelper | Set material property or keyword on scene object |
| graph_get | ShaderGraphHelper | Read .shadergraph structure (nodes, edges, properties) |
| graph_create | ShaderGraphHelper | Create .shadergraph from Unity template |
| graph_node | ShaderGraphHelper | Add/remove node in .shadergraph |
| graph_edge | ShaderGraphHelper | Add/remove edge in .shadergraph |
| graph_set_value | ShaderGraphHelper.Mutations | Set input value on a Shader Graph node by node name + port name |
| graph_connect | ShaderGraphHelper.Mutations | Connect two ports in a Shader Graph (source nodeId/port → target nodeId/port) |
| graph_add_node | ShaderGraphHelper.Mutations | Add a new node of specified type to a Shader Graph |

### Material Tool (MaterialHelper)
| Action | Description |
|--------|-------------|
| create | Create new Material asset from shader template (default: Standard) |
| get | Read material properties (from asset path or scene object) |
| set | Set material property (Color, Float, Texture, Vector, Int) |
| set_fields | Batch set multiple properties in one call (`\n`-separated `prop=value` pairs) |
| copy | Copy material to new asset (with property preservation) |
| list_properties | Enumerate all properties of a material |
| list_shaders | List available shaders with optional name filter |
| get_shader_errors | Return shader compilation errors for a material |

## Code Locations
- Python: `server/src/unity_mcp/tools/ui.py` (shader tool)
- C#: `unity-plugin/Editor/ShaderSerializer.cs`, `ShaderHelper.cs`, `ShaderGraphHelper.cs` (331 lines), `ShaderGraphHelper.Mutations.cs` (+110 lines: SetNodeValue, ConnectPorts, AddNode)
- C# Material: `unity-plugin/Editor/MaterialHelper.cs` — 5 actions (create, get, set, copy, list_properties)
- Router: `unity-plugin/Editor/CommandRouter.cs` (1053 lines, ExecShaderConsolidated + UnescapeJsonString)
- Tests: `server/tests/test_server_shader.py` (22), `unity-test-project/Assets/Tests/Editor/MCPShaderTests.cs` (41 tests incl. 2 regression)

## Review Checklist (for Reviewer)
- [ ] Security: no arbitrary file writes outside Assets/
- [ ] Performance: ShaderGraph parsing is O(n) on file size
- [ ] Token efficiency: text output format, no JSON overhead
- [ ] Edge cases: URP vs Standard pipeline, missing Shader Graph package, invalid presets

## Related
- Skill: `.claude/skills/csharp-unity.md`
- Knowledge: `AI/architecture.md`
