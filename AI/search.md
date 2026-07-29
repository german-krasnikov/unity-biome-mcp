# Feature: Scene Search

## Overview

Search GameObject hierarchy by name, component type, tag, layer, and active state. Single MCP tool `search_scene` with Unity-style query syntax: `t:ComponentName tag=Tag layer=0 active=true`. Returns flat list of matches with components and instance IDs.

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
- `t:Rigidbody` — component type (exact class name)
- `tag=Player` — Unity tag (exact match)
- `layer=5` — layer number 0-31
- `active=true|false` — GameObject.activeSelf

### Output Format

One match per line:
```
/Path/To/Object #InstanceID [Component1,Component2] !
```

- `/Path/To/Object` — full hierarchy path via `ComponentSerializer.GetPath()` (includes scene prefix in multi-scene: `SceneName:/Path/To/Object`)
- `#ID` — decimal instance ID
- `[Comp1,Comp2]` — list of component types (excluding Transform, comma-separated, no spaces)
- `!` — suffix if GameObject inactive

### Edge Cases

- Empty query → **error** (`query is required`)
- No matches → helpful hint with scene context and available filter syntax
- Deep hierarchies → no depth limit (traverse entire tree)

## Code Locations

- Python tool: `server/src/unity_mcp/tools/scene.py` (1 tool)
- C# helper: `unity-plugin/Editor/SearchHelper.cs` (166 lines)
- C# command: `unity-plugin/Editor/CommandRouter.cs` (`CommandRegistry.Register`)
- Python tests: `server/tests/test_search.py` (8 tests)
- C# tests: `unity-test-project/Assets/Tests/Editor/MCPSearchTests.cs` (14 tests)

## MCP Tool

### `search_scene`

**Parameters:**
- `query` (required) — search expression
- `root` (optional) — scope search to subtree (object path); `None` searches whole scene; limits results to children of this object
- `limit` (optional, default 50) — cap results; `0` = unlimited. Default not sent over wire for token savings.
- `scene` (optional, multi-scene only) — filter to a single scene by name

Search GameObject hierarchy by name, component, tag, layer, active state.

```
# Search by component
search_scene(query="t:Rigidbody")
→ /Player #2000 [Rigidbody,PlayerController]
  /Enemy #3000 [Rigidbody,EnemyAI] !

# Search by name (substring, case-insensitive)
search_scene(query="Player")
→ /Player #2000 [Rigidbody,PlayerController]
  /UI/PlayerUI #1302 [Canvas,PlayerUIScript]

# Combine filters
search_scene(query="t:Light active=true")
→ /Lights/Directional Light #1200 [Light]
  /Lights/Spotlight #1201 [Light]

# Scoped search — within subtree, limit results
search_scene(query="t:Renderer", root="/Level/Cave", limit=10)
→ /Level/Cave/Rock_1 #4050 [Renderer]
  /Level/Cave/Rock_2 #4051 [Renderer]
  ...+8 more (limit=10)

# Multi-scene search — filter to specific scene
search_scene(query="t:Light", scene="Forest")
→ Forest:/Lights/Directional Light #1200 [Light]
```

**Overflow marker:** When results exceed limit, the final line is `...+{N} more (limit={L})` showing remaining count.

**Path format (v0.57.0+):** Paths returned via `ComponentSerializer.GetPath()` (full hierarchy, not just name). Single-scene paths: `/Path/To/Object`. Multi-scene paths: `SceneName:/Path/To/Object`. **These paths are directly usable in `get_component`, `set_property`, etc.** — no transformation needed.

**PrefabStage support:** If Prefab Stage is open, search roots in that stage's prefabContentsRoot instead of scenes.

## TDD Scenarios

### Red Phase
1. **test_search_scene_calls_bridge**: query → sends command
2. **test_search_scene_empty_query**: empty → error (query required)
3. **test_search_scene_by_name**: name substring → matches found
4. **test_search_scene_by_component**: t:Type → objects with component
5. **test_search_scene_by_tag**: tag=Tag → objects with tag
6. **test_search_scene_combined_filters**: multiple filters → AND logic

C# tests (14 total, in MCPSearchTests.cs):
1. **SearchHelper_ByName_FindsMatchingObjects**: fuzzy name match
2. **SearchHelper_ByComponent_FindsWithComponent**: t: filter
3. **SearchHelper_CombineFilters_ReturnsIntersection**: AND logic
4. **SearchHelper_FormatResults_IncludesInactiveMarker**: `!` suffix

### Green Phase
- Python: 1 tool + bridge call + 8 tests
- C#: SearchHelper.cs (SearchQuery struct, ParseQuery, Matches, CollectMatches, BuildEmptyHint)
- C#: CommandRouter `CommandRegistry.Register` for `search_scene`

## Review Checklist

- [ ] Security: GameObject.Find safe, no eval, no path traversal
- [ ] Performance: FindObjectsByType or GetRootGameObjects + BFS (no N² search)
- [ ] Token efficiency: compact text format avoids repeated JSON structure
- [ ] Edge cases: empty query, no matches, inactive objects handled

## Related

- Skill: `.claude/skills/csharp-unity.md` (Editor API)
- Knowledge: `AI/hierarchy-serializer.md` (formatting)
