# Feature: Scene Search

## Overview

Search the GameObject hierarchy by name, component type, tag, layer, and active
state. The `search_scene` tool uses compact query syntax such as
`t:ComponentName tag=Tag layer=0 active=true` and returns paths, transient IDs,
components, and active-state markers.

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     search_scene tool             CommandRouter (1 case)
                                                         │
                                                   SearchHelper.cs
                                    (traverse, filter, format results)
```

## Implementation Notes

### Query Syntax

- `Player` — name substring match (case-insensitive, plain text, no wildcards or `name:` prefix)
- `t:Rigidbody` — component type name (case-insensitive; namespace optional)
- `tag=Player` — Unity tag (exact match)
- `layer=5` — layer number 0-31
- `active=true|false` — GameObject.activeSelf

### Output Format

One match per line:
```
/Path/To/Object &ref [Component1,Component2] !
```

- `/Path/To/Object` — full hierarchy path via `ComponentSerializer.GetPath()` (includes scene prefix in multi-scene: `SceneName:/Path/To/Object`)
- `&ref` — compact hierarchy reference assigned by `RefManager` (process-local, for example `&1`, `&a`, `&10`); prefer the path in durable examples
- `[Comp1,Comp2]` — list of component types (excluding Transform, comma-separated, no spaces)
- `!` — suffix if GameObject inactive

### Edge Cases

- Empty query → **error** (`query is required`)
- No matches → helpful hint with scene context and available filter syntax
- Deep hierarchies → no depth limit (traverse entire tree)

## Code Locations

- Python tool: `server/src/unity_mcp/tools/scene.py` (1 tool)
- C# helper: `unity-plugin/Editor/SearchHelper.cs`
- C# registration: `unity-plugin/Editor/CommandRouter.Registration.cs`
- Python tests: `server/tests/test_search.py` and `test_search_scoped.py`
- C# tests: `unity-plugin/Editor/Tests/SearchHelperFilterTests.cs` and `SearchHelperScopedTests.cs`

## MCP Tool

### `search_scene`

**Parameters:**
- `query` (required) — search expression
- `root` (optional) — scope search to an object and its descendants; `None`
  searches the whole scene
- `limit` (optional, default 50) — cap results; `0` = unlimited. Default not sent over wire for token savings.
- `scene` (optional, multi-scene only) — filter to a single scene by name

Search GameObject hierarchy by name, component, tag, layer, active state.

```
# Search by component
search_scene(query="t:Rigidbody")
→ /Player &1 [Rigidbody,PlayerController]
  /Enemy &2 [Rigidbody,EnemyAI] !

# Search by name (substring, case-insensitive)
search_scene(query="Player")
→ /Player &1 [Rigidbody,PlayerController]
  /UI/PlayerUI &3 [Canvas,PlayerUIScript]

# Combine filters
search_scene(query="t:Light active=true")
→ /Lights/Directional Light &4 [Light]
  /Lights/Spotlight &5 [Light]

# Scoped search — within subtree, limit results
search_scene(query="t:Renderer", root="/Level/Cave", limit=10)
→ /Level/Cave/Rock_1 &6 [Renderer]
  /Level/Cave/Rock_2 &7 [Renderer]
  ...+8 more (limit=10)

# Multi-scene search — filter to specific scene
search_scene(query="t:Light", scene="Forest")
→ Forest:/Lights/Directional Light &4 [Light]
```

**Overflow marker:** When results exceed limit, the final line is `...+{N} more (limit={L})` showing remaining count.

**Path format (v0.57.0+):** Paths returned via `ComponentSerializer.GetPath()` (full hierarchy, not just name). Single-scene paths: `/Path/To/Object`. Multi-scene paths: `SceneName:/Path/To/Object`. **These paths are directly usable in `get_component`, `set_property`, etc.** — no transformation needed.

**PrefabStage support:** If Prefab Stage is open, search roots in that stage's prefabContentsRoot instead of scenes.

## Tests

- `server/tests/test_search.py` covers the base wrapper/query contract.
- `server/tests/test_search_scoped.py` covers root, limit, and scene scoping.
- `unity-plugin/Editor/Tests/SearchHelperFilterTests.cs` and
  `SearchHelperScopedTests.cs` cover C# filtering and traversal.
- Use [`AI/testing.md`](testing.md) for current commands and acceptance policy.
## Review Checklist

- [ ] Security: GameObject.Find safe, no eval, no path traversal
- [ ] Performance: FindObjectsByType or GetRootGameObjects + BFS (no N² search)
- [ ] Token efficiency: compact text format avoids repeated JSON structure
- [ ] Edge cases: empty query, no matches, inactive objects handled

## Related

- Consumer workflow: `unity-plugin/ClientSkills/skills/unity-scene-authoring/SKILL.md`
- Knowledge: [`AI/hierarchy-serializer.md`](hierarchy-serializer.md) (paths and reference lifetime)
