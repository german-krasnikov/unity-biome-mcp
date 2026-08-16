# Hierarchy and Component Serialization

This document owns the compact Unity-to-text formats used by scene reads.
Public usage belongs in `docs/tools/scene.md` and `docs/tools/components.md`.

## Boundaries

- `HierarchySerializer` serializes the scene GameObject tree.
- `ComponentSerializer` serializes component fields and resolves scene paths.
- `RefManager` assigns compact hierarchy references.
- `TransientObjectId` formats transient object identities used outside the
  hierarchy format.
- `ConsoleCapture` and screenshot capture have independent formats and are not
  hierarchy serializers.

Do not treat the three identity syntaxes as interchangeable:

| Syntax | Owner | Meaning |
|---|---|---|
| `&<base62>` | `RefManager` | Compact hierarchy reference, for example `&1`, `&a`, `&10` |
| `$<decimal>` | `RefManager` compatibility parser | Legacy compact hierarchy input; current hierarchy output never emits it |
| `$<hex>` | Chat `HierarchyResultParser` compatibility | Reference token accepted in old v1.32-format `get_hierarchy` result text; it is not a current `RefManager` reference |
| `$<HEX>` | `TransientObjectId` | Current process-local Unity object or component identity outside hierarchy serialization |
| `#<decimal>` | `TransientObjectId` compatibility parser | Legacy transient object ID input |

`$name` is also the playtest-alias syntax. For that reason, new hierarchy
references use `&`. `RefManager` recognizes only decimal `$...` compatibility
tokens, while the Chat result parser accepts the hexadecimal shape produced by
the old hierarchy format. Current `$HEX` identities are resolved by
`TransientObjectId`; the owner and surrounding format determine the meaning.

## Hierarchy Output

The default format is a Unicode tree. Every emitted object ends with an
`&<base62>` reference:

```text
Main Camera &1
Directional Light &2
Player [Rigidbody,PlayerController] &3
├─ Body [SkinnedMeshRenderer] &4
└─ WeaponSlot [] &5
   └─ Sword [MeshFilter,MeleeWeapon] &6 !
```

### Format rules

| Element | Contract |
|---|---|
| Name | GameObject name as plain text |
| Components | `[Type1,Type2]` only when `components=true`; `Transform` is omitted |
| Reference | `&` plus an unbounded base-62 sequence assigned by `RefManager` |
| Inactive | `!` suffix when `activeSelf` is false |
| Depth limit | `+N` suffix is the descendant count below a truncated node |
| Global limit | `MAX_NODES = 3000`, followed by a narrowing hint when reached |
| Tree structure | `├─`, `└─`, and `│` connectors |

`RefManager` encodes `counter + 1`, so the first values are `&1`, `&2`, and so
on; after `&Z` it continues with `&10`. It does not wrap at a fixed slot count.
The server invalidates the map on connection lifecycle boundaries that make
references unsafe. Invalidating the maps does not reset the process-local
counter, so a stale reference cannot alias an object assigned later in the same
Editor process. A reference is still a convenient short-lived address, not a
durable object identity.

### Multi-scene output

`SceneContext.Current` owns loaded-scene enumeration and filtering.

- One selected scene is emitted without a header.
- Multiple selected scenes use `[SceneName]` headers.
- Duplicate scene names are disambiguated with their directory; an unsaved
  duplicate uses `(unsaved)`.
- `scene="Name"` filters enumeration to that scene.
- When `root` is also supplied, the handler scene-qualifies an unqualified root
  before lookup.
- A Prefab Stage takes precedence over ordinary loaded-scene enumeration.

Never build `sceneName + ":/" + path` at call sites. Use the shared scene/path
helpers (`SceneContext`, `ScenePathParser`, and
`ComponentSerializer.GetPath`) so single- and multi-scene behavior stays
consistent.

### Filter and depth

The name filter is case-insensitive. An ancestor is retained when a descendant
within the requested depth matches, preserving enough tree context to navigate
the result. `root` resolves one subtree before serialization.

### Summary and incremental modes

`summary=true` calls `SerializeSummary`. It emits scene/root counts and direct
root objects without per-object references or component lists. It is a
navigation overview, not a schema-compatible variant of the full tree.

`incremental=true` calls `SerializeIncremental`. The Unity side compares the
complete serialized string with the last incremental result and returns
`NO_CHANGE` when equal. The cache is process-local and is reset explicitly; it
is not a persisted scene version.

### Python-only compression

`compress=true` is implemented in `server/src/unity_mcp/tools/scene.py` after
Unity returns the hierarchy. It groups consecutive same-depth `slot_N` and
`point_N` objects and runs of visual mesh nodes. This presentation transform is
not part of `HierarchySerializer.cs` and must preserve the final sibling
connector.

`full=true` bypasses response distillation; it does not change Unity's
serialization format.

## Implementation Flow

```text
get_hierarchy (Python)
  -> command "get_hierarchy"
  -> CommandRouter.ObjectHandlers
  -> SerializeSummary | SerializeIncremental | Serialize
  -> optional Python compression
  -> optional response distillation
```

Primary files:

- `server/src/unity_mcp/tools/scene.py`
- `unity-plugin/Editor/CommandRouter.ObjectHandlers.cs`
- `unity-plugin/Editor/HierarchySerializer.cs`
- `unity-plugin/Editor/RefManager.cs`
- `unity-plugin/Editor/SceneContext.cs`
- `unity-plugin/Editor/ComponentSerializer.Finder.cs` (`ScenePathParser`)

## Alias Separation

Playtest aliases are read from `PlaytestConfig` by the separate `get_aliases`
command. `get_hierarchy` does not append an alias block. The Python middleware
may strip a legacy `--- ALIASES ---` block defensively, but new Unity responses
must not emit one.

Example `get_aliases` response:

```text
hp=/Player|HealthComponent|m_HP
speed=/Player|Rigidbody|m_Velocity
```

Alias cache and expansion behavior are documented in `AI/mcp-server.md` and
`AI/playtest-dsl.md`.

## Component Serialization

`ComponentSerializer` uses `SerializedObject` / `SerializedProperty` for
serialized Unity state. Output is compact sectioned text:

```text
name: Player
active: true
tag: Player
layer: Default
---
[Transform]
m_LocalPosition: (1.2, 3.4, 5.6)
---
[Rigidbody]
m_Mass: 1
m_UseGravity: true
```

Important invariants:

- component sections use `[TypeName]` and `---` separators;
- property lines use `name: value`;
- object references include a human-readable scene or asset description plus the
  current `$<hex>` transient identity when available;
- null object references serialize as `null`;
- floats use compact invariant-culture formatting;
- `get_component` field filtering and `inspect` composition belong to their
  handlers, not to the serializer itself.

ObjectReference writes must be preceded by an exact read when the middleware
requires it. See `AI/references.md` and `AI/api-design-standards.md` rather than
duplicating the mutation rules here.

## Invalidation and Safety

- A destroyed target resolves as stale and is removed from `RefManager`.
- `Invalidate()` clears both maps but keeps the counter monotonic for the life
  of the Editor process.
- `Prune()` removes destroyed objects without renumbering live entries.
- Hierarchy changes may invalidate assumptions even when a previously assigned
  reference still resolves; verify the target before a consequential write.
- Multi-scene tests must cover both qualified and unqualified paths and retain a
  single-scene regression case.

## Verification

Focused coverage lives in:

- `unity-plugin/Editor/Tests/MultiSceneHierarchyTests.cs`
- `unity-plugin/Editor/Tests/RefManagerTests.cs`
- component serializer/finder fixtures under `unity-plugin/Editor/Tests/`
- `server/tests/test_compress_hierarchy.py`
- scene wrapper and middleware tests under `server/tests/`

Follow the fixture and durable-run requirements in `AI/testing.md`.

## Review Checklist

- Hierarchy output uses `&<base62>`, never alphabetic `$alias` syntax.
- Component output and hierarchy output keep their distinct identity formats.
- `Transform` is omitted only from the optional hierarchy component list, not
  from full component serialization.
- Scene filtering scopes unqualified roots correctly.
- Summary, incremental, compression, and distillation are not conflated.
- New scene iteration uses the shared multi-scene helpers.
- Tests cover invalidation, ambiguity, depth, filtering, and node limits.

## Related

- `AI/search.md`
- `AI/references.md`
- `AI/testing.md`
- `unity-plugin/ClientSkills/skills/unity-mcp-operations/SKILL.md`
- `unity-plugin/ClientSkills/skills/unity-scene-authoring/SKILL.md`
