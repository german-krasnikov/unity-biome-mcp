# Feature: Deep Reference Analysis & Remapping (Phase 13)

## Overview
Track and remap ObjectReferences within scenes. Provides outgoing reference analysis, reverse search, and automatic/explicit remapping.

## Architecture (for Architect)
- `ReferenceHelper.cs` (374 lines) — Core reference traversal and remapping logic
- Three entry points: `GetReferences` (outgoing), `FindReferencesTo` (reverse), `RemapReferences` (mutate)
- Data flow:
  1. MCP client calls `get_references(path)` → Python → C# CommandRouter → ReferenceHelper.GetReferences
  2. Returns list of `RefEntry` objects (target object path, component type, field name, relation type)
  3. For remapping: `remap_references(source, target, mappings)` → traverses all objects, replaces refs
- RefEntry struct contains: ComponentType, PropertyPath, ReferencedPath, Relation, ReferencedId, ReferencedObject
- Relation types: `"self"`, `"child"` (in hierarchy), `"parent"`, `"sibling"` (same root), `"external"` (different root), `"asset"` (Material/Texture), `"null"`
- Cycle protection: `HashSet<int> visited` tracks processed objects
- Constraints: MAX_SCAN = 5000 objects, MAX_ARRAY = 100 array elements per field

## Implementation Notes (for Developer)

**Data storage:**
- RefEntry is struct, not persisted — generated on-the-fly during traversal
- Visited set prevents infinite loops when objects reference each other
- Undo.RecordObject called before any remap mutation

**Key constraints:**
- Scene traversal capped at 5000 objects (for find_references_to)
- Array elements capped at 100 per field (safety limit)
- Asset references (no mapping match) kept unchanged, marked "keep"
- Missing target in remap → output status "MISSING"
- ObjectReference serialized as "$hexId (TypeName)" (e.g. "$3E8 (Transform)") in get_component output; accepts #decimal format for backward compatibility in inputs

**Edge cases:**
- Null references → shown as "fieldName: null" (no RefEntry generated)
- Cyclic references → visited set prevents infinite recursion
- External refs (different scene) → cannot be remapped, logged as "external"
- Deleted objects in refMap → shows "MISSING" status in output
- Multi-level arrays → flattened iteration, reported as ArrayPath[index]

**MCP Tools:**
- `references(action, path, children, depth, source, target, mappings)` — outgoing/reverse/remap reference analysis
- `validate_references(path, depth, verbose, ignore_optional)` — deep ObjectReference integrity check
  - `verbose=true` includes [OK] lines (off by default to save tokens)
  - `ignore_optional=true` skips [Optional]-marked fields (reduces noise)
  - **RefManager internals (v1.28.0: $HEX format):** Outputs object IDs as `$HEX` (e.g. `$3E8`). Accepts `$HEX` and `#decimal` formats in inputs for backward compatibility.

**API (Python tools / C# commands):**
```
get_references(path, children=false, depth=1)
  → returns list of RefEntry objects (outgoing refs from path)

find_references_to(path)
  → reverse search: all objects in scene referencing path

remap_references(source, target, mappings=null)
  → source: path to remap from
  → target: path to remap to
  → mappings: null (auto prefix-replace) or explicit "old=new\nold2=new2"
  → returns refMap with status per remapped reference

set_property enhancement:
  → now accepts ObjectReference: null, $hexId (e.g. $3E8), #id (legacy), or /path
```

## Code Locations
- Python: `server/src/unity_mcp/tools/batch.py` (`references`, `validate_references`)
- C#: `unity-plugin/Editor/ReferenceHelper.cs`
- C# Router: `unity-plugin/Editor/CommandRouter.cs` (consolidated action dispatch via `ExecReferencesConsolidated`)
- C# ObjectManager: `unity-plugin/Editor/ObjectManager.cs` (set_property enhanced)
- C# ComponentSerializer: `unity-plugin/Editor/ComponentSerializer.cs` (ObjectReference output)
- Tests Python: `server/tests/test_server_references.py` (15 tests)
- Tests C#: `unity-test-project/Assets/Tests/Editor/MCPReferenceTests.cs` (19 tests)

## TDD Scenarios (for Developer)

### Red Phase (write failing tests first)
1. **get_references from object with single field ref**: Input path, depth=1 → expect RefEntry for each field containing ObjectReference
2. **get_references with children=true**: Input path, children=true → expect refs from target AND all children
3. **find_references_to**: Input path → expect all objects in scene that reference that path
4. **remap_references auto-prefix**: source="/A/B", target="/C/D" → all refs to "/A/B/*" become "/C/D/*"
5. **remap_references explicit mapping**: mappings="old_path=new_path" → exact replacements applied
6. **remap_references with missing target**: target doesn't exist → marked "MISSING", original ref unchanged
7. **Cycle detection**: Object A refs B, B refs A → no infinite loop, both found
8. **Array iteration**: Field is array[10] → all 10 elements iterated (or capped at 100)
9. **Null reference handling**: Field is null → no RefEntry, but field still appears in output as "null"
10. **External references (assets)**: Material/Texture references → marked "asset", skipped in remap

### Green Phase (minimal implementation)
- SerializedProperty.GetValue() → iterate through properties recursively
- Detect ObjectReference by type check (obj is GameObject/Component)
- Build RefEntry with source path and target path
- For find_references_to: full scene traversal with visited set
- For remap_references: Undo.RecordObject, iterate detected refs, SerializedProperty.SetValue(newRef)
- Mark results as "success", "keep" (external), or "MISSING"

### Refactor Phase
- Extract common path manipulation to utility function
- Cache relation type detection (child vs external vs asset)
- Consider batch entry point for multiple source paths

## Review Checklist (for Reviewer)
- [ ] Security: Undo.RecordObject called before mutations
- [ ] Performance: MAX_SCAN and MAX_ARRAY limits prevent hangs on large scenes/arrays
- [ ] Token efficiency: RefEntry struct serialized compactly, minimal output
- [ ] Edge cases: Cycles tested, null refs handled, assets skipped in remap
- [ ] Undo/Redo: All mutations recordable via Edit→Undo
- [ ] Type safety: ObjectReference deserialization correct (null/id/path)

## Chat Interactive References (2026-06-03)

In-Unity Chat messages can embed reference links with special syntax:
- **Scene objects:** `obj:/Path/To/Gameobject` → renders as `<link="obj:/Path/To/Gameobject">...</link>`
- **Scripts:** `script:Assets/Path/To/Script.cs` → renders as `<link="script:Assets/Path/To/Script.cs">...</link>`

**ChatRefResolver** (startup + cached):
- Scans loaded scenes, maps hierarchy paths
- Resolves script assets via AssetDatabase

**ChatRefAction** (interaction handlers):
- **Click:** Navigates — calls `EditorGUIUtility.PingObject()` + `Selection.activeObject = obj`
- **Alt+Click:** "Add to Context" → injects ref payload into input field
- **Right-Click:** Context menu with "Navigate" + "Add to context" options
- **Hover:** Shows tooltip "Alt+Click to add to context"

**Token savings:** No new MCP tools — reuses get_component/set_property. Chat just makes refs clickable.

## Related
- Skill: `.claude/skills/csharp-unity.md` (SerializedProperty API)
- Knowledge: `AI/architecture.md` (CommandRouter routing)
- Knowledge: `AI/batch.md` (batch remapping pattern)
- Knowledge: `AI/agent-chat.md` (Chat interactive refs implementation)
