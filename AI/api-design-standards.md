# API Design Standards

Use this reference when adding or changing an MCP tool or a Unity TCP command.
The executable contract remains the Python signature, its generated MCP
schema, `ToolSpec`, the Unity command registration, and parity tests.

Consumer-agent workflow guidance lives in `unity-plugin/ClientSkills/`; do not
link developer documentation to an installed `.claude/` copy.

## Scope and Sources of Truth

| Concern | Canonical source |
|---|---|
| Public Python signature and description | `server/src/unity_mcp/tools/*.py` |
| Category, visibility, timeout, mutability, runtime and batch surface | `server/src/unity_mcp/tools/tool_specs.py` |
| Canonical category membership | derived by `tools/gating.py` from `ToolSpec` |
| Deferred schema registry | `tools/schema_registry.py` and `server_filtering.py` |
| Unity command and argument behavior | `unity-plugin/Editor/CommandRouter*.cs` and helpers |
| Published exhaustive schema | generated `docs/tools-schema/index.md` |
| Cross-language conformance | parity and API-standard tests under `server/tests/` |

Do not put a total tool count in authored documentation. It is volatile and
derivable from the runtime/generator.

## Public Names

Use `verb_noun` snake case for a new standalone tool. Action-based tools may use
a Unity-domain noun (`asset`, `scene`, `shader`) when `action` is the verb.

```text
Good: get_hierarchy, set_property, create_object, manage_component
Avoid: hierarchy_get, setProperty, CreateObject, object_manager
```

Before adding a tool, search both surfaces:

```bash
rg 'async def .*keyword' server/src/unity_mcp/tools
rg "'[^']*keyword[^']*': ToolSpec" server/src/unity_mcp/tools/tool_specs.py
rg 'Register\("[^\"]*keyword' unity-plugin/Editor
```

Prefer extending a cohesive action-based tool when the new operation shares its
target, validation, and response model. Do not force unrelated operations into
one action enum merely to reduce the count.

## Parameters

Reuse established names when semantics are identical, but do not rename an
existing public parameter solely to satisfy an aspirational vocabulary. Current
intentional contracts include:

- `path` for one scene or asset path and `paths` for a comma-separated set;
- `component` for a component type;
- `type` where the underlying Unity operation selects a type;
- `prop` in serialized-property mutation tools;
- `field` in reflection/query protocols;
- `pattern` for a glob/search expression;
- `action` for a closed action enum.

A Python-friendly name may map to a different legacy TCP key only when the
mapping is explicit and covered by tests. Examples include
`show_unity_private -> include_internal` and `watch_id -> id`.

Closed enums should use `Literal[...]` when practical. Otherwise the docstring
must list the values and the Unity handler must reject an invalid value with a
useful error; a silent no-op is never valid.

## Optional Arguments and Booleans

`server._args()` removes only values equal to `None`. It does not normalize
types. Unity handlers commonly read scalar arguments through string-oriented
`JsonHelper` methods, so wrappers must deliberately encode optional booleans.

For a Unity default of `false`, send `"true"` when enabled and omit the key when
disabled:

```python
negate="true" if negate else None
```

For a Unity default of `true`, omit the key for the normal case and send
`"false"` for the override:

```python
world_position_stays=None if world_position_stays else "false"
```

When the boolean is the operation's required payload, send both values
explicitly as lowercase strings:

```python
{"active": "true" if active else "false"}
```

Do not pass a raw Python `bool` to `_args()` and assume it will be converted.
The API-standard AST tests enforce the supported patterns and maintain a small,
reviewed exception set.

## Responses and Errors

Prefer compact text across the Unity TCP boundary. Python wrappers should not
parse and reserialize a response unless they are explicitly composing,
projecting, validating, or correlating a higher-level contract.

Rules:

- Unity command failures use a stable `err:`/`error:` form or a failed response
  envelope; Python-only validation raises `ToolError` or returns an equally
  explicit error contract.
- Machine-readable sentinels use stable keys and delimiters. Durable operations
  retain all identities (`request_id`, `run_id`, `utf_guid`) rather than asking
  callers to infer the latest run.
- A wrapper that waits, retries, or composes another tool states that fact in its
  docstring and distinguishes caller timeout from terminal operation state.
- Never claim rollback, persistence, or verification unless the implementation
  observed it. Report partial and unknown states explicitly.
- Avoid hard token limits in prose. Response distillation, compression, and
  schema deferral are separate mechanisms and should be named precisely.

## Tool Metadata and Visibility

Every public tool has one `ToolSpec` entry. `_SPECS` drives the category catalog,
core/tier visibility, TCP timeout, read/write fail-closed behavior, Play Mode
guard, and `direct_only` surface.

```python
_SPECS = {
    "my_tool": ToolSpec(category="SCENE", mutability="read"),
}
```

- `core=True`: always visible with a full schema; reserve for the minimum
  cross-domain workflow.
- `tier1=True`: bypasses category gating, but a non-CORE tool can still be
  hidden by Unity tool settings.
- neither: category-gated and enabled through `discover_tools`.
- `direct_only=True`: may be called as a typed MCP tool but is rejected inside
  the batch DSL.
- `runtime_only=True`: the Python guard requires cached Play Mode state before
  sending the command.

New tools default to category-gated. Promotion requires a documented use case
and schema/token-budget review. Plugin tools remain dynamically registered and
must not be copied into the built-in catalog.

The canonical authored categories are SCENE, COMPONENTS, ASSETS, UGUI,
UITOOLKIT, MEDIA, VERIFY, RUNTIME, TESTS, and SYSTEM. CORE is a catalog group,
not an additional `discover_tools` category. Legacy aliases are compatibility
metadata in `gating.py`; do not add an alias casually.

## Deferred Schemas

`server_filtering.py` keeps full schemas for core tools and for an explicit
`_SCHEMA_KEEP_FULL_EXTRA` allowlist. The allowlist contains non-core tools whose
required arguments must remain constructible before a deferred-schema fetch.

Most non-core schemas are replaced in `ListTools` by a stub and are retrieved
with `resolve_tool_schema`. Adding to the allowlist requires evidence that the
initial call cannot be constructed reliably through discovery plus schema
resolution, as well as updates to filtering/schema tests. It is not a general
"frequently changing schema" list.

## Compatibility and Removal

There is no blanket "never preserve compatibility" rule. The repository
currently supports deliberate compatibility paths, including legacy category
aliases and selected wire-format parsers. Conversely, keeping every duplicate
forever produces an unsafe and confusing surface.

For a removal or rename:

1. identify public Python, ToolSpec, Unity command, generated schema, docs,
   ClientSkills, tests, and migration impact;
2. decide explicitly whether a compatibility window is required;
3. update both language surfaces and parity tests in one change;
4. document the migration in `CHANGELOG.md`;
5. remove stale examples and installed-skill source references.

The internal C# `set_runtime_property` command is a current example: it remains
for optional middleware routing, but no public Python MCP tool exposes that
name. Public docs must not advertise it as a typed tool.

## Intent Tools

`do`, `ui_intent`, `uitk_intent`, `vfx_intent`, and `animator_intent` are active
composition tools. Natural-language paths use configured sampling profiles;
several also provide deterministic templates that bypass sampling. Do not name
a specific provider model as part of their contract.

Before adding another intent tool, prefer a deterministic public API, batch, or
`configure_objects`. A new intent surface needs a constrained intermediate
format, validation, safe dry-run behavior, partial-failure reporting, sampling
profile configuration, and tests for unavailable sampling. Intent tools that
compose complex results remain `direct_only`.

`ask` is read-only sampled analysis. `ask_user` is a separate interactive user
input path; do not conflate them.

## Common Workflow Boundaries

### Compile and verification

| Tool | Contract |
|---|---|
| `get_compile_errors` | Current corroborated compile-error view |
| `compile_preflight` | Check proposed C# content before applying it |
| `await_compile` | Observe compilation/reload reconciliation that another action started |
| `verify_after_change` | Additive compile, error, console, NUnit, and playtest gates |

Do not describe `verify_after_change` as running reference, scene-scan, or
screenshot gates: those are not part of its current implementation.

### Component reads

| Tool | Contract |
|---|---|
| `get_component` | One object and component type; optional field projection |
| `inspect` | Multiple paths or a component-type search in one call |
| `configure_objects` | Compact multi-object mutation DSL, not a read result |

Do not loop `get_component` over many known paths when one `inspect` call can
preserve the same evidence. `compress=True` asks Unity to strip defaults before
transfer; `fields=` is a Python-side projection and intentionally bypasses
distillation.

## Review Checklist

- [ ] Existing Python, ToolSpec, Unity, docs, and ClientSkills surfaces were searched.
- [ ] The public signature uses established names or documents a tested mapping.
- [ ] Optional values and booleans are encoded deliberately.
- [ ] A closed action enum validates invalid input.
- [ ] Errors, sentinels, partial states, and timeout semantics are unambiguous.
- [ ] `ToolSpec` category, mutability, timeout, runtime, and direct-only flags match behavior.
- [ ] Visibility or full-schema promotion is justified.
- [ ] Compatibility/removal impact is explicit and changelogged when public.
- [ ] Composition and intent tools report partial application and sampling failure.
- [ ] Tests cover signature/schema and Python-to-Unity argument parity.
- [ ] Authored docs do not copy volatile tool totals or complete rosters.

Run the focused policy checks from `server/`:

```bash
uv run pytest tests/test_api_standards.py tests/test_toolspec_v2_parity.py \
  tests/test_schema_parity.py tests/test_registration_parity.py -q
```

Follow the repository-wide evidence requirements in `AI/testing.md`.
