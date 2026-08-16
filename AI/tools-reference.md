# Tool Surface Reference

This document explains how the MCP tool surface is assembled. It intentionally
does not copy the complete roster or parameter list. Those are generated from
the executable contract and change too frequently for a second hand-maintained
inventory.

## Inspect the Current Surface

For a running server:

```python
# Browse canonical categories without enabling them.
await discover_tools(enable=False, structured=True)

# Retrieve exact descriptions and parameter schemas for deferred tools.
await resolve_tool_schema(tools="spatial_query,uitk_file")

# Inspect Unity's registered TCP capabilities when cross-language drift is suspected.
await get_capabilities()
```

For a repository checkout:

- `server/src/unity_mcp/tools/tool_specs.py` is the per-tool metadata source.
- `server/src/unity_mcp/tools/gating.py` derives categories and visibility.
- `server/src/unity_mcp/server.py` composes module registration.
- `unity-plugin/Editor/CommandRouter.Registration.cs` and related partials
  register Unity commands.
- `docs/tools-schema/index.md` is the generated exhaustive public schema.

Do not add authored totals such as "N tools" or "N tools in SYSTEM". Derive
them during validation when a count is evidence that the surfaces agree.

## Visibility Model

Every public tool has one `ToolSpec`:

```python
ToolSpec(
    category="SCENE",
    core=False,
    tier1=False,
    timeout_s=30.0,
    mutability="write",
    runtime_only=False,
    direct_only=False,
)
```

The fields drive these independent decisions:

| Field | Effect |
|---|---|
| `core` | Always visible and retains its full initial schema |
| `tier1` | Always visible but not necessarily full-schema |
| neither | Hidden until its category is enabled |
| `mutability` | Read-only/read-write policy and fail-closed metadata |
| `runtime_only` | Python Play Mode guard before TCP dispatch |
| `direct_only` | Rejected in batch DSL; callable as a typed MCP tool |
| `timeout_s` | Default TCP deadline when a wrapper does not override it |

CORE is a catalog group, not an eleventh `discover_tools` category. The ten
canonical discoverable categories are:

```text
SCENE, COMPONENTS, ASSETS, UGUI, UITOOLKIT,
MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM
```

Their ownership is intentionally domain-oriented:

| Category | Owns |
|---|---|
| SCENE | GameObjects, scenes, spatial queries, scene transactions |
| COMPONENTS | Component events, wiring, and reference relationships |
| ASSETS | Asset database, prefabs, materials, shaders, packages, builds/bakes |
| UGUI | Canvas authoring and validation |
| UITOOLKIT | UXML/USS files and live VisualElement panels |
| MEDIA | Screenshots, animation, VFX, timeline, rendering analysis |
| VERIFY | Compile, diagnosis, integrity, and post-change verification |
| RUNTIME | Play Mode reads, invocation, watches, profiling, debugging |
| TESTS | NUnit and playtest execution/linting |
| SYSTEM | Discovery, connection, permissions, sessions, code/meta operations |

Core tools either use the `CORE` category or retain a domain category with
`core=True`; tier-1 promotion likewise preserves the declared category.
`get_catalog()` lists every core tool only under `CORE` and removes it from its
domain category bucket. Do not maintain a separate hand-written tier list.

## Discovery

`discover_tools(category=None, enable=True, include_legacy=False,
structured=False)` operates on Python session visibility.

- With no category, it lists canonical categories. `include_legacy=True` adds
  compatibility aliases to that listing.
- With a category, `enable=True` enables its current tool set for the session
  and emits `notifications/tools/list_changed` when a context is available.
- `enable=False` browses without mutating visibility.
- `structured=True` adds `core`/`tier1`, surface, and mutability tags.

Legacy aliases are defined in `_CATEGORY_ALIAS`. They remain accepted as
compatibility inputs even when omitted from the default listing. Any removal or
addition is a public compatibility decision, not a documentation-only cleanup.

Plugin tools register dynamically through `register_tools`. They cannot promote
themselves to the built-in always-visible budget. Unknown plugin categories are
preserved in the legacy `CATEGORIES` view; they must not be copied into the
built-in themed source.

## Deferred Schemas

The initial `ListTools` response keeps full schemas for core tools and the
explicit `_SCHEMA_KEEP_FULL_EXTRA` allowlist in `server_filtering.py`. Other
known non-core tools receive a short description and stub input schema until
`resolve_tool_schema` is called.

The deferred-schema registry captures the full schema before filtering. Schema
deferral changes the discovery payload, not dispatch validation: the FastMCP
tool manager retains the callable's real schema and rejects unknown arguments.

When changing a signature, update tests that cover:

- captured schema presence and required arguments;
- stub/full filtering behavior;
- runtime dispatch rejecting additional properties;
- docs-schema generation from the final export.

## Batch Surface

`direct_only=True` is the sole metadata source for typed tools that cannot be
embedded in `batch`. `gating._DIRECT_ONLY`, the Python batch guard, and the
structured discovery output derive from it.

Do not paste the direct-only roster into authored docs. Ask structured discovery
or derive it from `_SPECS`. A tool belongs on the direct-only surface when it
has a composite return, performs Python-side orchestration/file I/O, waits on a
durable operation, or otherwise cannot fit the line-indexed Unity batch
protocol safely.

Atomic batch mode rolls back only Unity changes recorded through Undo. It does
not make direct-only Python orchestration or external side effects transactional.

## Python and Unity Parity

Not every public MCP tool has a one-to-one Unity command:

- Python-only orchestration may compose several commands.
- One public wrapper may use an internal C# compatibility command.
- Protocol-only commands use `category="_INTERNAL"` and are not MCP tools.
- Deprecated hidden stubs may remain callable by exact name during a migration.

Parity checks must therefore use the explicit exception/ownership rules in the
test suite rather than comparing two naive name sets.

Run the focused checks from `server/`:

```bash
uv run pytest tests/test_toolspec_v2_parity.py tests/test_surface_parity.py \
  tests/test_registration_parity.py tests/test_catalog.py \
  tests/test_deferred_schema.py tests/test_schema_parity.py -q
```

## Change Checklist

- Add or update the Python function and its registration.
- Add or update exactly one `ToolSpec`.
- Add or update the Unity command when the tool crosses TCP.
- Verify category, mutability, runtime, timeout, and direct-only behavior.
- Test required/optional arguments and unknown-argument rejection.
- Update the smallest owning developer and user document.
- Update ClientSkills only when consumer-agent behavior changes.
- Regenerate the schema and quality artifacts through their scripts.
- Record public additions, removals, and migrations in `CHANGELOG.md`.

## Related

- `AI/api-design-standards.md`
- `AI/mcp-server.md`
- `AI/batch.md`
- `AI/testing.md`
- `docs/tools-schema/index.md`
- `unity-plugin/ClientSkills/skills/unity-mcp-operations/SKILL.md`
