# Feature: Batch Commands

## Overview

Single MCP tool that executes multiple compatible Unity commands in one call
using a compact text format. Operations run sequentially on the Unity main
thread with configurable error handling.

Prefer `batch` for two or more compatible operations. Direct-only and
Python-expanded tools must be called through their typed MCP wrappers. For
multi-object component reads, prefer `inspect`.

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
              batch tool (no parsing)    CommandRouter (batch case)
                                                    │
                                     BatchHelper.Execute
                                      (ParseLines → seq ops)
```

## Implementation Notes

### Validation Layer (Anti-Hallucination)
- **CommandValidator.cs**: Pure validation functions over CommandRegistry's per-command contract
  - `Validate(cmd, argsJson)` checks: command exists, required params present, unknown params detected
  - Contract declared at `CommandRegistry.Register()` call site via `required:` and `optional:` CSV params (not a separate schema dict)
  - Returns error message with sigil grammar: `!param` (missing), `?param→closest` (unknown with suggestion), or `?param` (unknown, no suggestion)
  - `AutoUsage()` computes usage string (e.g. `wire_event path=... component=... [mode=...]`), never hand-written
- **StringDistance.cs**: Levenshtein distance + ClosestMatch for fuzzy suggestion matching
- **What it catches**:
  - Wrong command names: `move_object` → "Unknown command 'move_object'. Did you mean 'create_object'?"
  - Missing required params: `set_property path=/A` → "set_property !component !prop !value Unknown param.\n  set_property path=... component=... prop=... value=..."
  - Wrong param names (typo): `set_property path=/A ?valuee→value` → "set_property ?valuee→value Unknown param.\n  set_property path=... component=... prop=... value=..."

### Data Format
- Commands as text, one per line: `cmd key=value key=value`
- Python forwards raw text to Unity (no JSON parsing)
- C# pipeline: `ParseLines()` → `JsonHelper.UnescapeJsonString()` → split lines → `ParseLine()` → `ParseKeyValuePairs()` → `BuildJsonObject()` (values escaped via `JsonHelper.EscapeJson()`)
- All values always quoted as JSON strings: `{"name":"123"}` (no type detection)
- Quoted values: `name="My Object"` (handles spaces inside quotes)
- Empty lines and `#` comments ignored
- On error: continue through all or stop at first failure

### Nested Batch Depth Counter (F11 Fix, Wave 1)

**Problem**: `BatchHelper.InBatch` was a `bool`. A nested `batch` command's `finally` block reset it to `false` and fired `Physics.Sync` while the outer batch was still running, so the outer tail lost the batch optimization and physics synced twice.

**Fix**: Replaced with `_batchDepth` int counter. `InBatch` property now returns `_batchDepth > 0`. `Physics.Sync` fires only at the outermost exit (`--_batchDepth == 0`). `finally` block still decrements on mid-batch exceptions, preventing leaks.

### Python Batch Guard (DSL-Tool + direct_only Enforcement + Parameter Stripping)

Plugins can register DSL-expansion tools via `register_dsl_tools()` from `plugin_api.py`. These tools are rejected by Python `batch()` with ToolError — they require Python-side processing before reaching C#. Always call them as typed MCP tools.

When a registered DSL tool is called via batch, Python raises ToolError immediately:
```
ToolError: <tool_name> requires typed MCP tool (Python DSL expansion), not batch
```

**Python-only parameter stripping (v1.15.0):** Before forwarding batch commands to C#, Python strips any parameters that are Python-only (not defined in the C# CommandRegistry). This allows the batch caller to use shorthand or middleware-expanded params without triggering validation errors on the C# side. The stripped params are consumed by Python middleware and not sent to Unity.

Example: A hypothetical `Python-only_flag=true` parameter would be stripped from the text before TCP dispatch, allowing downstream C# validators to succeed.

**Direct-only tools:** `batch()` rejects every tool whose `ToolSpec` has `direct_only=True`; these tools require typed arguments, Python-side orchestration, or result handling that the line DSL cannot preserve. The authoritative set is `server/src/unity_mcp/tools/tool_specs.py` and can change without this document changing.

With `on_error=continue` (the default), direct_only lines are filtered out before TCP dispatch. Their errors are prepended to the final result with original line numbers remapped correctly. With `on_error=stop`, a ToolError is raised immediately before any dispatch.

```
[1] err: 'do' is direct-only; call it as a typed MCP tool, not in batch
[2] ok: /Player
ok:1 err:1
```

### Constraints
- No async commands allowed (wait_until, move_to, run_tests, test_step, run_playtest prohibited)
- `screenshot` not allowed — uses specialDispatch path in CommandRouter and returns a file-path
  response (not an "ok" string), which batch output format cannot represent
- `ask_user` not allowed — blocks up to 300 s awaiting user input; incompatible with sequential
  batch execution and the 75 s default timeout
- No inter-command references (each op is independent)
- Tool enable/disable checks apply to each command
- Play Mode guard: mutating commands blocked in Play Mode (`BLOCKED` response)
- Runtime guard: runtime-only commands blocked outside Play Mode (`BLOCKED` response)
- Compile guard: mutating commands blocked during compilation (unless explicitly allowed)
- Main thread processing only (no concurrency)
- DSL-expansion tools (registered via `register_dsl_tools()`) rejected with clear error message (Python-side check)
- **`$alias` sigils expanded in batch DSL (v0.78.8):** `BatchHelper.cs` calls `AliasExpander.ExpandText()` per line before key=value parsing. Python alias resolution is NOT needed for batch; C# alias table is populated via `get_aliases` / auto-warmup.

### Blast Radius Exemption for Read-Only Batches (v0.78.10)

`_is_batch_readonly(commands)` helper (`middleware_guards.py`) checks if every non-blank, non-comment line in the batch text is a read command. When true, `check_blast_radius()`, `check_verification_needed()`, and `transition()` all return `None` immediately — no blast-radius warning, no verification nudge, no FSM write counter increment. Read-only batches (`get_hierarchy`, `get_component`, `inspect`, etc.) pass through middleware cleanly without any guard noise.

`editor` is special-cased: only `action=state` and `action=project_path` count
as reads (`_EDITOR_READ_ACTIONS` constant); absent or any other action
(`play`, `stop`, `pause`, `step`, or `select`) is treated conservatively as a
write. All other commands are checked against the source-owned `READ_CMDS` set;
do not copy its changing membership into documentation.

### C# Command Filtering Options

`inspect` and `get_component` accept optional filtering params when used in batch DSL or direct TCP calls (applied C#-side via `ApplyFieldsCompress`):
- `fields=field1,field2` — return only named fields from component output (`FieldProjector.Project`)
- `compress=true` — strip default values from output (`DefaultStripper.Strip`)

`fields` and `compress` are mutually exclusive; `fields` takes precedence.

```
# Batch examples
get_component path=/Player type=Rigidbody fields=m_Mass,m_Drag
inspect paths=/Player,/Enemy components=Rigidbody compress=true
```

### Edge Cases
- Empty text → returns `ok:0` (no operations, summary only)
- Quoted values with spaces: `name="Object A"` parsed correctly
- Escaped quotes inside values: `name="Object \"A\""` handled by JsonHelper.UnescapeJsonString
- Escaped backslashes: `\\n` (literal backslash + n) not converted to newline
- Unquoted values with no spaces: `key=value` parsed without quotes
- Comments: `# comment` lines skipped
- Tool disabled → per-operation error with `continue`/`stop` respect
- Unknown command → caught per-operation, error formatted as `[N] err: message`

## Code Locations

- Python tool: `server/src/unity_mcp/tools/batch.py` (batch tool)
- Auto-batch: `server/src/unity_mcp/tools/autobatch.py` (setup_objects, set_properties, configure_objects)
- C# executor: `unity-plugin/Editor/BatchHelper.cs` (Execute, ParseLines, ParseLine, ParseKeyValuePairs, ParseValue, BuildJsonObject; uses JsonHelper.EscapeJson, JsonHelper.UnescapeJsonString)
- Validation layer: `unity-plugin/Editor/CommandValidator.cs` (Validate, AutoUsage, ExtractKeys — pure functions over CommandRegistry contract)
- String matching: `unity-plugin/Editor/StringDistance.cs` (Levenshtein, ClosestMatch)
- Command registry: `unity-plugin/Editor/CommandRegistry.cs` (Register/RegisterAction — declares required/optional params via CSV, contract source of truth)
- Command dispatch: `unity-plugin/Editor/CommandRouter.cs` (batch case, timeout_ms support)
- Python tests: `server/tests/test_batch.py`, `test_batch_conflict.py`, `test_batch_timeout.py`, `test_autobatch.py`
- C# tests: `unity-plugin/Editor/Tests/MCPBatchAtomicTests.cs`, `BatchHelperParserTests.cs`, and `BatchHelperReadOnlyTests.cs`
- Batch rejection tests: `unity-plugin/Editor/Tests/BatchRejectionTests.cs` (async, specialDispatch, runtime-only, atomic rollback)
- Validation tests: `unity-plugin/Editor/Tests/CommandValidatorOptionalParamsTests.cs`

## Atomic Mode (F27, Transactional Batches)

Opt-in `atomic=true` groups Undo-recorded Unity changes. On the first failure,
`UndoGroupHelper` reverts changes recorded in that group. File-system, process,
and other external side effects are outside the guarantee. Default
`atomic=false` is backward-compatible and token-neutral.

**Semantics:**
- **Outermost-only grouping**: `_batchDepth` counter ensures only the outermost batch (depth=1) opens/closes the Undo group. Nested batches roll back under the single outer group.
- **atomic overrides on_error**: When atomic, batch always stops on first failure regardless of on_error setting.
- **Error output format**:
  - Normal rollback: `ATOMIC_ROLLBACK: reverted ops 0..K-1` (ops 0 through K-1 reverted)
  - First op fails: `op 0 failed, nothing to revert` (no prior ops to rollback)
- **Limitation**: `execute_code` file-system side effects are NOT reverted (only Unity Undo-registered scene mutations roll back).

**Example:**
```python
batch(
  commands="create_object name=A\nset_material path=/A color=#FF0000\nUNKNOWNCMD",
  atomic=True
)
# → [0] ok: created /A
# → [1] ok
# → [2] err: Unknown command 'UNKNOWNCMD'. Did you mean 'get_console'?
# → ATOMIC_ROLLBACK: reverted ops 0..1
# → err:1
```

## MCP Tool

### Tool: `batch`
**Parameters:** `commands` (required, text), `on_error`
(default=`"continue"`), `atomic` (default=false), `timeout` (default=75.0;
Python sends an internal timeout five seconds lower, while Unity's outer request
deadline is 65s), and `validate_aliases` (default=false; dry-run alias
resolution before execution).

Executes multiple text-based commands. One command per line, format: `cmd key=value key=value`

**Examples:**
```python
batch(
  commands="create_object name=A primitive=Cube\nset_material path=/A color=#FF0000",
  on_error="continue"
)
# → ok:2
```

```python
batch(
  commands="create_object name=A\nset_material path=/A color=#FF0000\nBADCMD",
  atomic=True
)
# → ATOMIC_ROLLBACK: reverted ops 0..1
# → err:1
```

Note: commands returning `"ok"` are suppressed from output. Only data responses and errors get `[N]` lines.

**Parsing rules:**
- First word = command name
- Key=value pairs separated by spaces
- Quoted values: `name="My Object"` (spaces allowed inside quotes)
- Parenthesized values: `pos=(1,0,0)` (treated as single value, supports nesting)
- Empty lines and `#` comments ignored

**Error modes:**
- `continue` (default) — run all operations, collect results
- `stop` — halt execution on first error, skip remaining

**Response format:**
```
[N] data response     # only for non-"ok" results (e.g. get_component data)
[N] err: error message
[N] skip
[N] TIMEOUT: batch deadline reached after Xs
[N] BLOCKED: reason
ATOMIC_ROLLBACK: reverted ops 0..K-1  # only in atomic mode on failure
op 0 failed, nothing to revert         # in atomic mode when first op fails
ok:N                  # summary line always present
ok:N err:M            # when errors occurred
ok:N err:M timeout:K  # when timeout hit
```

## TDD Scenarios

### Python Tests
1. **test_batch_text_forwarded**: text passed unchanged to bridge (no JSON parse)
2. **test_batch_on_error_forwarded**: on_error="continue"|"stop" forwarded correctly
3. **test_batch_multiple_commands**: multiple lines executed sequentially
4. **test_batch_error_response**: bridge error → Python returns error message
5. **test_batch_empty_commands**: empty text → empty response
6. **test_batch_stop_on_error**: on_error="stop" → remaining operations skipped

### C# Tests (EditMode)
1. **ParseLine_SingleCommand**: `ping` → `(cmd="ping", argsJson="{}")`
2. **ParseLine_QuotedValue**: `cmd name="A B"` → args with spaces handled
3. **ParseLines_SkipEmpty**: empty lines skipped, comments skipped
4. **Execute_SingleOp**: single command → `[0] ok: result`
5. **Execute_MultipleOps**: 3+ commands → indexed responses
6. **Execute_StopOnError**: error + stop mode → remaining marked `skip`
7. **Execute_DisabledTool**: disabled tool → `[N] err: Tool disabled`

## Review Checklist

- [x] Security: no code injection (text forwarded safely, C# parser validates)
- [x] Performance: no per-command overhead, sequential only
- [x] Token efficiency: compact command text avoids repeated typed-call envelopes
- [x] Text parsing: quoted values, comments, empty lines handled
- [x] Edge cases: empty text, disabled tools, stop vs continue modes, special chars
- [x] Anti-hallucination: CommandValidator validates all commands before execution via contracts declared at Register() call site, catches typos with fuzzy suggestions, error format uses sigils (!missing, ?unknown→suggestion)

## Related

- Standards: `AI/api-design-standards.md` (MCP tool patterns)
- Protocol: `AI/tcp-bridge.md` (message format)
- Consumer workflow: `unity-plugin/ClientSkills/skills/unity-mcp-operations/references/batching.md`
- Knowledge: `AI/mcp-server.md` (auto-batch tools: setup_objects, set_properties, configure_objects)
