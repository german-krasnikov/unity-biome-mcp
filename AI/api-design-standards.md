# API Design Standards — Unity Biome MCP

Reference for Architects, Developers, and Reviewers. Read before designing or reviewing any MCP tool.

**Companion skill:** `.claude/skills/api-design-standards.md` (compact DO/DON'T, auto-loaded when editing tool files).

---

## 1. Scope

Covers all 148+ MCP tools: Python-side definition (`server/src/unity_mcp/tools/`) and C#-side command registration (`unity-plugin/Editor/Commands/`).

**No backward compatibility.** Deprecated tools are deleted — not shimmed, not aliased, not marked legacy. The API surface must contain only actively used, non-redundant tools. When a tool is identified as a duplicate or anti-pattern, it is removed from both Python and C# sides in the same PR.

---

## 2. Tool Naming

**Rule:** `verb_noun` snake_case. Verb = action. Noun = Unity concept.

```
DO:   get_hierarchy, set_property, create_object, manage_component, wire_event
DON'T: hierarchy_get, setProperty, CreateObject, object_manager, hierarchyTool
```

**Check:** `grep -rn "async def " server/src/unity_mcp/tools/ --include="*.py" | grep -v "_"`

---

## 3. Parameter Naming

Same concept → same name across ALL tools. Inconsistency forces callers to memorize per-tool differences.

| Canonical | BANNED aliases | Semantics |
|-----------|---------------|-----------|
| `path` | `object_path`, `go_path` | Scene path to GameObject |
| `component` | `comp`, `comp_type` | Component type (string) |
| `field` | `prop`, `property`, `field_name` | Field/property name on component |
| `value` | `val`, `new_value` | Value to set |
| `pattern` | `paths`, `search` | Search pattern |
| `scene` | `scene_name`, `scene_path` | Scene name |
| `mode` | `type`, `action_type` | Operation mode (enum string) |
| `fields` | `field_filter`, `props` | Filter list (comma-sep string) |
| `parent` | `new_parent` | Target parent path |
| `action` | — | Verb for multi-action tools (enum) |

**Resolved:** `set_runtime_parent` removed from Python tools (§7 resolution complete).

**Known violation:** `set_property` uses `prop`; `set_runtime_property` uses `field` for the same concept. Unification in progress via middleware auto-rerouting.

**Check:** `grep -rn '"new_parent"' server/src/unity_mcp/tools/` — must return 0 hits.

---

## 4. Boolean Encoding

**THE ONE RULE:** Python booleans never cross the TCP boundary as Python objects. Encode them as strings.

### Pattern A — Optional flag, C# default is `false`

Send `"true"` when set; omit (`None`) when not set. `_args()` strips `None` — this is the designed mechanism.

```python
# DO — delete_object(force=)
if force:
    args["force"] = "true"

# DO — wait_until(negate=)
negate="true" if negate else None,
```

### Pattern A′ — Optional flag, C# default is `true`

Send `"false"` when overriding to False; omit (`None`) when True.

```python
# DO — transfer_object(world_position_stays=)
wps = None if world_position_stays else "false"

# DON'T — set_parent (inconsistent — always sends both values)
args["world_position_stays"] = "true" if world_position_stays else "false"  # always sends
```

### Pattern B — Required bool (no default; the bool IS the payload)

Always send `"true"/"false"`. Use only when the sole purpose of the call is to set the boolean (e.g. `set_active`).

```python
# DO — set_active(active=)
return await _send("set_active", {"path": path, "active": "true" if active else "false"})
```

### P0 bug — raw Python bool through `_args()`

`_args()` receives a Python bool and serializes it as `"True"`/`"False"` (capital letter) — C# `bool.Parse` fails silently.

```python
# WRONG — animator tool, _args() call
has_exit_time=has_exit_time  # if True → "True" over TCP, not "true"

# RIGHT
has_exit_time="true" if has_exit_time else None,  # Pattern A
```

**Check violations:** `grep -rn ': True\b\|: False\b' server/src/unity_mcp/tools/` inside `_args(...)` calls.

---

## 5. Response Format

**R1: Text over JSON.** C# serializes to text; Python forwards the string. Never re-parse and re-serialize on the Python side.

**R2: Token budget targets.**

| Response | Max tokens | Format |
|----------|-----------|--------|
| Hierarchy (50 objects) | ~350 | indented text tree |
| Component read | ~100 | `Key: Value\n` |
| Error | ~30 | plain string |
| Success | ~10 | `ok` or `saved` |

**R3: `err:` prefix is the error contract.** Every error response from a TCP command starts with `err:`. No bare error strings. Callers check `result.startswith("err:")`.

**R4: Sentinels must be machine-parseable.** Fixed prefix + delimited fields. No trailing prose in the parseable part.

```python
# WRONG — prose and no durable identity
return f"tests-started|{mode}|check back later"
# prose tail leaks into sentinel

# RIGHT
return (
    f"tests-started|request_id={request_id}|run_id={run_id}"
    f"|utf_guid={utf_guid}|state=dispatched"
)
# callers retain every identity and query only the exact run
```

**R5: Wrapper tools must say "wrapper".** When one tool polls another, its docstring must call out the relationship.

```python
# DO — run_tests_wait docstring
async def run_tests_wait(...) -> str:
    """Consumer wrapper around the durable direct protocol.

    Resolves one request identity and waits for its exact run to reconcile.
    Timeout is observational and does not mark the Unity run complete.
    """
```

**R12: Action enum tools must validate and redirect.** When a tool accepts an `action: str` param with a closed enum, the docstring must list valid values. Invalid values must return `err:invalid action '<value>'`, NOT a silent no-op.

```
Example: manage_component accepts `add|remove` only.
LLMs consistently try `enable`/`disable`.
The docstring bans them but C# must also return:
  err:invalid action 'enable' — use set_property(field='m_Enabled', value='true'/'false') instead
```

---

## 6. Tool Gating & Tier

Canonical source: `server/src/unity_mcp/tools/tool_specs.py`.

| Tier | When | Token cost |
|------|------|-----------|
| `core=True` | Must-have for baseline (read+write+verify). Example: `get_hierarchy`, `get_compile_errors` | Always in context |
| `tier1=True` | Always-visible, moderate frequency. Example: `delete_object` | Always in context |
| Default (TIER2) | Gated by category. Example: `animation`, `animator` | On-demand only |

**Rule:** New tools default to TIER2. Elevation to TIER1/CORE requires explicit justification of token cost. Every new tool needs a `ToolSpec` entry in `_SPECS` before merge — `gating.py` crashes at import otherwise.

```python
# DO
'my_tool': ToolSpec(category='SCENE', mutability='write'),  # TIER2 by default

# MUST justify
'my_tool': ToolSpec(category='SCENE', tier1=True),  # justify in PR why always-visible
```

---

## 7. Tool Deduplication & Deletion

**Rule:** Duplicate tools are deleted, not deprecated. No aliases, no shims, no "call the other one internally."

**Resolved (v0.x): `set_parent`/`set_runtime_parent` twins.** `set_runtime_parent` deleted from Python MCP tools. `set_parent` unified to work in both Edit and Play Mode (no `runtime_only` restriction). Middleware auto-reroutes `set_property` → `set_runtime_property` in Play Mode.

**Ongoing:** `set_runtime_property` C# handler retained (middleware depends on it). Python MCP tool stays exposed for Play Mode reflection-based field writes. Future: unify parameter names (`prop` vs `field`).

**Before adding a new tool, grep:**
```bash
grep -rn "async def " server/src/unity_mcp/tools/ --include="*.py" | grep -i "<keyword>"
grep -rn "'<cmd>'" server/src/unity_mcp/tools/tool_specs.py
```

If an existing tool can be extended with a `mode` or `action` param — extend it. New tool is the last resort.

---

## 8. Intent Tools — Delete, Don't Add

**Intent tools** (`do`, `ui_intent`, `vfx_intent`, `animator_intent`) call a sub-LLM (Haiku) for planning. This creates double latency and double token spend. All are marked `direct_only=True` in `tool_specs.py`.

**Policy:** These tools are **scheduled for deletion**. New domains (terrain, physics, etc.) must use `batch` directly or `configure_objects`. Do not add new intent tools.

**Exception:** `ask` tool uses Haiku as summarizer (read-only, no planning). This pattern is acceptable.

---

## 9. Compile Health — Which Tool When

| Tool | Use case |
|------|---------|
| `get_compile_errors` | Check error list right now (TCP-based, always current — never use Editor.log) |
| `compile_preflight` | Before mutation: is compile clean? Blocks if not |
| `await_compile` | After writing a `.cs` file: wait for recompile to finish |
| `verify_after_change` | After multi-step mutation: 5-gate check in one call (compile + refs + console + scan + screenshot) |

**Anti-pattern:** Do not call `get_compile_errors` in a poll loop — it is a point-in-time check, not a wait mechanism. Use `await_compile` for that.

---

## 10. Component Read — Which Tool When

| Tool | Use case |
|------|---------|
| `get_component` | One object, full field list. Use `fields=` and `compress=True` to reduce tokens |
| `inspect` | N objects in one TCP call. `inspect(paths='a,b,c', compress=True)` |
| `configure_objects` | Read + write in one call (batch mutate pattern) |

**Anti-pattern:** Never call `get_component` in a loop over N objects — that is N TCP round-trips. Use `inspect(paths='...')`.

---

## 11. Screenshot / Media Token Budget

`screenshot` tool находится в `_SCHEMA_KEEP_FULL_EXTRA` (`server_filtering.py`), что означает его полная schema всегда сериализуется (не deferred).

**Criteria for `_SCHEMA_KEEP_FULL_EXTRA`** — добавлять tool только если:
1. Схема меняется чаще чем раз в месяц, ИЛИ
2. Параметры критичны для первого вызова (нет safe defaults)

**Rule:** Новые tools НЕ добавлять в `_SCHEMA_KEEP_FULL_EXTRA` без обоснования в PR.

---

## 12. Compliance Checklist

For every new or modified tool — PASS/FAIL:

```
[ ] Tool name: verb_noun snake_case
[ ] Parameters: canonical names from §3 table (no new_parent, no prop for field)
[ ] Booleans: Pattern A/A′/B — no raw Python bool in _args() (no ": True" or ": False")
[ ] Response: text format, err: prefix on errors
[ ] Sentinel: no prose after the second | delimiter
[ ] Tier: TIER2 by default; TIER1/CORE requires PR justification
[ ] No new intent tools (do/ui_intent pattern); delete existing when opportunity arises
[ ] No new *_runtime_* twin tools; extend with mode param or delete old twin
[ ] _SCHEMA_KEEP_FULL_EXTRA: not added without documented reason (schema changes >monthly OR no safe defaults)
[ ] ToolSpec entry exists in tool_specs.py before merge
[ ] fields= and compress= supported in any tool returning component data
[ ] compress= means C#-side stripping only — no Python-side filtering under that name
[ ] Wrapper tools: docstring contains "wrapper" and names the wrapped tool
[ ] Mode-scoped twin tools cross-reference each other in docstrings
[ ] Duplicate check: grep tool_specs.py + tools/ before adding
```

**Auto-checks (pytest):** `server/tests/test_api_standards.py` — bool Pattern A, ToolSpec coverage, arg-name parity. Run with `pytest tests/test_api_standards.py -v`.
