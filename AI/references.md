# Deep Reference Analysis and Remapping

## Overview
Track and remap ObjectReferences within scenes. Provides outgoing reference analysis, reverse search, and automatic/explicit remapping.

## Architecture (for Architect)
- `ReferenceHelper.cs` — outgoing and reverse reference traversal
- `RemapReferencesHelper.cs` — reference remapping
- Three entry points: `GetReferences` (outgoing), `FindReferencesTo` (reverse), `RemapReferences` (mutate)
- Data flow:
  1. MCP client calls `references(action="get", path=...)` → Python → C# CommandRouter → ReferenceHelper.GetReferences
  2. Returns list of `RefEntry` objects (target object path, component type, field name, relation type)
  3. For remapping: `references(action="remap", source=..., target=..., mappings=...)` replaces matching refs
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
- `ReferenceHelper` output includes the referenced path plus a process-local
  `#<unsigned-decimal>` instance ID. Treat it as transient and prefer paths for
  durable examples. Other serializers may expose `$<hex>` transient IDs; see
  [`AI/hierarchy-serializer.md`](hierarchy-serializer.md#boundaries)
  for the format boundaries.

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
  - Output IDs are diagnostic, process-local instance references; they are not
    the short-lived `&<base62>` hierarchy IDs.

**API (Python tools / C# commands):**
```
references(action="get", path=path, children=false, depth=1)
  → returns list of RefEntry objects (outgoing refs from path)

references(action="find_to", path=path)
  → reverse search: all objects in scene referencing path

references(action="remap", path=source, source=source, target=target, mappings=null)
  → source: path to remap from
  → target: path to remap to
  → mappings: null (auto prefix-replace) or explicit "old=new\nold2=new2"
  → returns refMap with status per remapped reference

set_property ObjectReference inputs:
  → prefer null or /path; transient $hex and legacy #decimal forms are accepted by compatible resolvers
```

## Code Locations
- Python: `server/src/unity_mcp/tools/batch.py` (`references`, `validate_references`)
- C#: `unity-plugin/Editor/ReferenceHelper.cs` and `RemapReferencesHelper.cs`
- C# Router: `unity-plugin/Editor/CommandRouter.MediaHandlers.cs` (`ExecReferencesConsolidated`)
- C# ObjectManager: `unity-plugin/Editor/ObjectManager.cs` (set_property enhanced)
- C# ComponentSerializer: `unity-plugin/Editor/ComponentSerializer.cs` (ObjectReference output)
- Tests Python: `server/tests/test_server_references.py`
- Tests C#: `unity-plugin/Editor/Tests/ReferenceHelperTests.cs` and `ValidateReferencesHelperTests.cs`

## Tests

- `server/tests/test_server_references.py` verifies the public wrappers.
- `unity-plugin/Editor/Tests/ReferenceHelperTests.cs` covers traversal and
  multi-scene relation classification.
- `unity-plugin/Editor/Tests/ValidateReferencesHelperTests.cs` covers integrity
  diagnostics.
- Use [`AI/testing.md`](testing.md) for current commands and acceptance policy.
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
- Consumer editing workflow: `unity-plugin/ClientSkills/skills/unity-csharp-editing/SKILL.md`
- Knowledge: [`AI/architecture.md`](architecture.md) (CommandRouter routing)
- Knowledge: [`AI/batch.md`](batch.md) (batch remapping pattern)
- Knowledge: [`AI/agent-chat.md`](agent-chat.md) (Chat interactive refs implementation)
