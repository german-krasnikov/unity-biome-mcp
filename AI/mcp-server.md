# Feature: MCP Server

## Overview

Python MCP server for controlling Unity Editor. `_UnstructuredMCP(FastMCP)` +
ConnectionSlot + capability gating + the middleware pipeline. External plugins
can add tools dynamically. Structured output is disabled on all tools to avoid
duplicating `content` and `structuredContent` in MCP responses. Derive the
current tool and middleware counts from code/tests; do not preserve them here.

## Architecture (for Architect)

Selected files and ownership boundaries (not an exhaustive generated tree):

```
server/src/unity_mcp/
├── server.py           # FastMCP instance, lifespan, tool registration
├── bridge.py           # UnityBridge (TCP, heartbeat, keepalive)
├── connection_slot.py  # ConnectionSlot (single connection)
├── lockfile.py         # Exclusive fcntl.flock per port, stale server cleanup
├── compile_state.py    # CompileStateProbe (heuristic Unity compile detection)
├── middleware.py        # Middleware class: 23 layers (env-gated), holds _alias_cache (name→pipe_value, cleared on reset_session)
├── middleware_alias.py  # Pure alias functions (stdlib only): parse_aliases_from_hierarchy, parse_aliases_from_get_aliases, resolve_aliases_in_args, strip_alias_block
├── middleware_pipeline.py  # wrap_send() — assembles all hooks in order
├── plugin_api.py       # Stable public API for external plugins
├── resources.py        # MCP Resources: static (hierarchy, console, editor, categories) + dynamic (biome://go/cs/pfb/mat/so) via search_context
├── tools/
│   ├── __init__.py     # Tool module registry
│   ├── _annotations.py # MCP ToolAnnotations constants (RO, RW, RW_IDEM, DEL)
│   ├── _common.py      # bind() helper — uniform binding across register() functions
│   ├── tool_specs.py   # single source of truth for categories, tiers, timeouts, mutability and surfaces
│   ├── gating.py       # Capability gating: CORE, TIER1, category-based filtering, catalog
│   ├── schema_registry.py  # Tool schema lazy-loading
│   ├── objects.py      # get_component/inspect/find/set_property/create/delete/manage_component/set_active/rename_object/wire_event/unwire_event/set_material/set_parent/set_property_delta/transfer_object/object_diff
│   ├── scene.py        # get_hierarchy, scene, search_scene, fingerprint, scene_diff, scene_environment, save/load_session, screenshot_baseline/compare, get_changes
│   ├── console.py      # get_console, get_compile_errors (B2: split from scene.py)
│   ├── screenshot.py   # screenshot + optional configured sampling description
│   ├── editor_control.py  # editor (play/pause/stop/select), ping_object, undo_last, checkpoint, get_capabilities (B2: split from scene.py)
│   ├── testing.py      # run_tests, get_test_results, get_test_count, get_test_progress (B2: split from scene.py)
│   ├── code_intel.py   # compile_preflight, await_compile
│   ├── runtime.py      # invocation, waits, state queries, movement and playtest execution
│   ├── batch.py        # batch, references, validate_references + DRY serialization
│   ├── spatial.py      # spatial_query, validate_triggers, get_spatial_context, scan_scene, check_colliders
│   ├── ui.py           # uGUI, menu and shader wrappers
│   ├── uitk.py         # UI Toolkit inspection, files, attachment and live elements
│   ├── codegen.py      # execute_code, get_schema, auto_fix, smart_build
│   ├── skills.py       # save_skill, use_skill, list_skills, apply_template, save_template, list_templates
│   ├── animation.py    # animation, timeline, animator, particle
│   ├── asset.py        # asset, material, prefab, scriptable_object, project_settings, get_enabled_tools
│   ├── connection.py   # list_connections, reconnect_unity
│   ├── autobatch.py    # setup_objects, set_properties, configure_objects
│   ├── auto_wire.py    # auto_wire — fill null ObjectReference fields by name/type matching
│   ├── do_tool.py      # sampled NL intent → validated plan → batch execute
│   ├── ask_tool.py     # read-only question → route → optional sampled summary
│   ├── ask_user_tool.py     # ask_user — interactive question shown as Unity UI card
│   ├── animator_intent_tool.py  # Domain-specific animator NL
│   ├── vfx_intent_tool.py       # Domain-specific VFX NL
│   ├── ui_intent_tool.py        # Domain-specific UI NL
│   ├── intent_common.py         # Shared intent infrastructure
│   ├── permission_prompt_tool.py  # --permission-prompt-tool MCP handler for Claude CLI
│   ├── budget_tool.py           # budget_status tool (sampling spend tracking)
│   ├── metrics_tool.py          # Performance metrics
│   ├── meta.py         # discover_tools, doctor, resolve_tool_schema, set_llm_config, alias_status
│   ├── diagnose.py     # Python wrapper for C# diagnose command
│   ├── diagnostics.py  # Performance and diagnostics tools (Play Mode and editor)
│   ├── debug_tool.py   # AI-assisted debugging: gather diagnostic context
│   ├── profiling.py    # get_frame_stats, profile sessions, get_memory
│   ├── rendering.py    # render_analyze — dispatches to RenderAnalyzer.cs
│   ├── scene_health.py # Scene hierarchy/health audit
│   ├── sync.py         # sync_unity — unified Unity reload API
│   ├── watch.py        # Watch system — path-based field polling in Play Mode
│   ├── reload_ladder.py     # T0-T5 reload-recovery ladder
│   ├── transaction.py       # scene_change_plan + apply_scene_change (transactional scene edits)
│   └── verify.py            # verify_after_change — 5-gate verification pipeline
└── plugins/
    └── __init__.py              # 3-source auto-discovery (pkgutil, entry_points, UNITY_MCP_PLUGIN_DIRS)
```

### Tool surface

The complete roster is derived from `tool_specs.py` and the registration tests;
it is not maintained in this architecture document. `AI/tools-reference.md`
owns visibility and schema-loading behavior, while generated
`docs/tools-schema/index.md` owns exhaustive public parameters.

The canonical discoverable categories are SCENE, COMPONENTS, ASSETS, UGUI,
UITOOLKIT, MEDIA, VERIFY, RUNTIME, TESTS, and SYSTEM. CORE is a catalog group,
not another discoverable category. `direct_only` and full-schema exception sets
are also derived/verified from metadata rather than copied here.

### Compile-Tool Corroboration (v0.7.0+)

`get_compile_errors`, `await_compile`, `auto_fix`, and `ask` now cross-verify clean responses via `editor_log.py`: an out-of-band reader of Unity's `Editor.log` that catches cases where the in-plugin C# reporter is itself broken (stale bytecode, unsafe to trust). Only overrides when both signals agree: log shows errors AND dll is stale. Zero false positives (fresh dll trusted). Resolves P0 silent-blindness bug where plugin compile failures masked themselves.

**get_unity_events:** Returns all UnityEvent fields on a component with fully-qualified
target paths. Replaces manual `get_component` + parsing when auditing event wiring.

### Capability Gating (gating.py)

- CORE and `tier1=True` tools are always visible to the MCP client
- Categories enabled per-session via `discover_tools(category, enable=True)`
- Double-filtered: Python gating × Unity-side MCPSettings (tool cache from `get_enabled_tools`)
- Plugin self-registration: `gating.register_tools("category", tools_set)` adds
  tools to the category-gated surface; plugins have no tier1 escape hatch

**Tool Visibility Logic (v0.57.0, commit 2fac0bd)** — fixed AND logic in `server_filtering.py`:
- Previously: tool visibility had OR bug; disabled tools remained visible
- Fixed: tool is hidden only if explicitly in disabled set (AND logic: visible = `name not in disabled`)
- Impact: `is_visible=false` checkbox in MCPSettings now correctly hides tools
- Implementation: `_apply_gating()` calls `filter_by_tier()` which respects disabled tool set correctly

**InitializedNotification Hook (`install_initialized_hook`, server_filtering.py):**
- Registers a `notification_handlers[InitializedNotification]` handler on the MCP server.
- On MCP handshake completion, reads `session.client_params.clientInfo.name`.
- If name is absent or equals `_DEFAULT_CLIENT_LABEL = "Claude Code"` (already the C# default), returns immediately — no TCP call.
- Otherwise fires `bridge_.send("set_client_label", {"label": name}, timeout=3.0)` asynchronously via `asyncio.ensure_future`.
- Send failures log at DEBUG level, never raise. Handler errors also swallowed at DEBUG.
- Wired in `server.py` alongside `install_list_tools_filter`:
  ```python
  install_list_tools_filter(mcp, lambda: _disabled_tools_cache)
  install_initialized_hook(mcp, lambda: slot.bridge if slot else None)
  ```

### Server Startup

```python
# server.py
# _UnstructuredMCP(FastMCP) subclass forces structured_output=False on all tools
mcp = _UnstructuredMCP("UnityMCP", lifespan=lifespan)
register_all(mcp, _send, _args, get_slot=lambda: slot, get_middleware=lambda: _middleware)
load_plugins(mcp, _send, _args)

# Port configuration (v0.57.0)
# - DEFAULT_PORT = 9500 centralized in constants.py (replaces magic strings)
# - Auto-discovered from ~/.unity-biome-mcp/ports/*.port or UNITY_MCP_PORT env
#   (v0.96.1: iter_port_files() also checks legacy ~/.unity-mcp/ports/ as fallback)
# - Used throughout: lockfile creation, bridge connection, test harnesses

@asynccontextmanager
async def lifespan(app):
    # 1. Auto-discover Unity port from ~/.unity-biome-mcp/ports/*.port or UNITY_MCP_PORT env
    # 2. Cleanup stale port-scoped config files (v0.53.0): unity-biome-mcp-config-{port}.json older than 2h
    # 3. Acquire this session's presence lock:
    #    ~/.unity-biome-mcp/server-{port}-{pid}.lock
    # 4. Create ConnectionSlot, connect bridge
    # 5. Wire middleware layers (if UNITY_MCP_MIDDLEWARE=1)
    # 6. Wire ToolHinter (default on, disable with UNITY_MCP_HINTS=0)
    # 7. Wire budget tracking (default on, disable with UNITY_MCP_BUDGET=0)
    # 8. Wire optional layers: SceneBrief, SpeculativeLayer, Lessons, Watchdog, Inference
    # 9. Fetch enabled tools cache, start heartbeat, register reconnect callbacks
    yield
    # Shutdown: stop heartbeat, cancel watchdog, close bridge, release lock, delete own config

def main():
    _last_useful_activity = time.monotonic()
    _last_transport_activity = time.monotonic()
    _in_flight_count = 0
    _start_idle_watchdog()  # daemon: exits if parent dies + idle > UNITY_MCP_IDLE_TIMEOUT (default 300s), or if subagent > UNITY_MCP_USEFUL_IDLE_TIMEOUT (default 0)
    transport = os.environ.get("UNITY_MCP_TRANSPORT", "stdio")
    if transport == "http":
        port = int(os.environ.get("UNITY_MCP_HTTP_PORT", "8765"))
        mcp.run(transport="streamable-http", host="127.0.0.1", port=port)
    else:
        mcp.run(transport="stdio")
```

**Idle watchdog (T4: Activity Tracking)** — daemon thread with two idle timeout paths:
- **Path A (Subagent)**: UNITY_MCP_USEFUL_IDLE_TIMEOUT env var (default 0 = off). When set, exits regardless of parent liveness if useful activity idle > timeout. Used by subagent runtimes to enforce timeout independent of stdio parent.
- **Path B (Orphan Reaper)**: UNITY_MCP_IDLE_TIMEOUT env var (default 300s). Only exits when parent process has changed (truly orphaned) AND idle > timeout. Alive parent → watchdog remains dormant, acting as orphan-reaper only. Key gate: `if os.getppid() == _ORIGINAL_PPID: continue` — parent alive so skip exit.

**Activity Tracking Split (v1.x)**:
- `_last_useful_activity` — updated by `_touch_useful_activity()` before every MCP tool dispatch (in `_send()`). Measures work being done.
- `_last_transport_activity` — updated by `_touch_transport_activity()` on successful heartbeat ping. Measures transport-layer connectivity.
- `_in_flight_count` — counter incremented/decremented around `_send()` calls (try/finally guard). Watchdog skips idle check if `_in_flight_count > 0` — prevents premature exit while a tool is executing.
- Backward-compat alias: `_touch_activity = _touch_useful_activity` for legacy code.

### Bridge / Connection

- **ConnectionSlot**: single `UnityBridge` connection
  - `connect(port)`, `reconnect()`, `bridge` property
  - `status` property (v0.78.10): delegates to `bridge.status`; returns `"disconnected"` when bridge is None
- **UnityBridge**: single TCP connection
  - `status` property (v0.78.10): `"connected"` / `"reconnecting"` / `"domain-reloading"` / `"disconnected"`. Used by `list_connections` (replaces binary connected/disconnected boolean)
  - Protocol: JSON over TCP, 4-byte big-endian length prefix
  - Socket: `TCP_NODELAY`, `SO_KEEPALIVE` (macOS: idle=60s, interval=10s, count=3)
  - Heartbeat: 15s interval, raw ping, 3 failures → close, 2s polling when disconnected (5s if compile busy)
  - Reconnect backoff (v0.52.7): exponential (5s→60s, reset on success, ±10% jitter), cooldown re-armed on every attempt. Ping verification, fires callbacks
  - **DomainReloadError fast-fail (v0.81.4):** `send()` raises `DomainReloadError("Domain reload in progress — retry after recompile")` immediately on entry if reload state is active (checked via `_reload.is_active()`). No retry inside send(); caller must handle timeout/retry post-recompile. Reload state tracked independently via `DomainReloadTracker` (90s expiry window, marked when `going_away` event fires or explicit reload detection).
  - **ConnectionRefused during reload:** When Unity closes TCP during domain reload, bridge detects it through stale reload state + connection failure pattern. Fast-fail gate prevents commands from queuing during reload window.

### Server Control (server_control.py)

- `list_servers` — list all running MCP server PIDs/ports (reads
  `~/.unity-biome-mcp/server-{port}-{pid}.lock` files)
- `stop_server(port)` — graceful SIGTERM (Unix) or taskkill (Windows) shutdown of running server

### Compile State Probe (compile_state.py)

Simplified detector for Unity C# compile/domain-reload:
- **State file**: reads `~/.unity-biome-mcp/state/port-{port}.state` (ready/compiling/reloading)
- `is_process_dead()`: PID cross-check from port file

**compile_status** response format (v0.89.x):

```
idle                     # compile finished, domain-reload complete
compiling|2.3            # compiling, elapsed seconds
idle-stale               # isCompiling=false but no compilationFinished — wedge suspected
reloading|ready          # domain reload in progress, assembly side ready
reloading|compiling      # domain reload in progress, compile still running
```

`|reload=` suffix is appended by `SyncHelper` when a domain reload is tracked.
Use `compile_status` polling instead of a sleep loop to detect reload completion.

**console mark_id format (v0.89.x):** `console_mark()` accepts any non-empty string as
label. `get_console_since(mark)` uses tolerant parsing — leading `mark:`, timestamps,
and label suffixes are all handled. `keyword` and `count_only` params are forwarded
to the underlying `get_console` call.

```python
# All equivalent:
mark = await console_mark()                      # → "mark:1720000000.0"
mark = await console_mark(label="before_spawn")  # → "mark:1720000000.0:before_spawn"
errors = await get_console_since(mark, keyword="NullRef", count_only=True)
# → "3"  (count of matching errors since mark)
```

### Auto-Batch (autobatch.py)

- `setup_objects(specs)` — create+configure multiple objects (one per line DSL)
- `set_properties(path, props)` — set multiple properties (component.prop=value)
- `configure_objects(config)` — configure multiple objects (/Path component.prop=value)
- All expand internally to `batch` commands

### Intent Meta-Tools

- `do(intent, dry_run)` — sampled NL → plan → validate → batch execute
- `ask(question)` — read-only deterministic route with optional sampled summary
- `animator_intent`, `vfx_intent`, `ui_intent`, `uitk_intent` — constrained
  domain-specific intent tools; deterministic templates bypass sampling where supported

### MCP Resources (resources.py)

Static and dynamic resource URIs registered with `biome://` scheme:

**Static resources** (4):
- `biome://scene/hierarchy` — current scene hierarchy summary
- `biome://console/errors` — recent console errors
- `biome://editor/state` — editor state (play mode, scene, selection)
- `biome://tools/categories` — available tool categories

**Dynamic resources** (auto-refreshed on connect/reconnect):
- `biome://go/<path>` — GameObject at hierarchy path; reads via `inspect` command
- `biome://cs/<path>` — C# script asset; reads via `asset` command
- `biome://pfb/<path>` — Prefab asset; reads via `prefab` command
- `biome://mat/<path>` — Material asset; reads via `material` command
- `biome://so/<path>` — ScriptableObject asset; reads via `scriptable_object` command

**Registration and refresh:**
- `refresh_dynamic()` queries `search_context` TCP command (no args, max 200 results)
- C# `SearchContextPlugin` returns TSV: typeCode, path, displayName
- Python parses TSV and creates `FunctionResource` entries for each result
- Stale URIs removed, new URIs added on each refresh
- Called automatically on server lifespan init and on TCP reconnect

### SamplingService Singleton (v0.57.0, commit 787a397)

**Refactored from instance to module-level singleton:**
```python
# sampling.py — module-level singleton
sampling_service = SamplingService()
```

**Usage:** `SamplingService._get_semaphore()` returns shared asyncio.Semaphore across all callers.

**Benefits:**
- Centralized concurrency control: UNITY_MCP_VISUAL_CONCURRENCY (default 4) enforced globally
- Clean shared state: no instance passing required
- Testability: pytest can mutate module-level `sampling_service` directly (no dependency injection)
- Memory efficiency: one shared service instance for configured sampling calls

**Related:** Budget tracking in `sampling.py` (metrics, latency tracking) — wired from server.py lifespan.

### Plugin System

3-source discovery:
1. **pkgutil built-in**: discovers modules inside `plugins/` package
2. **entry_points**: `importlib.metadata.entry_points(group="unity_mcp.plugins")` for pip-installed packages
3. **UNITY_MCP_PLUGIN_DIRS**: env var pointing to filesystem directories with plugin modules

Each plugin implements `register(mcp, send_fn, args_fn)`. Plugin API facade (`plugin_api.py`) provides stable exports: `API_VERSION`, `RO`, `RW`, `RW_IDEM`, `DEL`, `SamplingService`, `strip_fences`, `sanitize_intent`, `register_dsl_tools()`, `register_read_cmds()`, `register_write_cmds()`, `register_tools()`, `register_features()`.

### Code Intel Tools (server 0.4.0)

**await_compile (NEW):** Read-only tool that blocks until Unity finishes C# compilation AND domain-reloading. Returns compile errors as plain text. Survives domain-reload disconnect via reconnect + re-query. `timeout=0` = instant snapshot. Replaces `sleep`-then-poll patterns.

- `compile_preflight` — pre-compile validation + type inference for code edits

### Deferred MCP Tool-Schema Loading (F4, server 0.3.0)

Non-core tools return a **stub inputSchema** `{"type":"object"}` from `list_tools` instead of full schemas. Full schemas are served lazily via a new meta-tool:

```
resolve_tool_schema(tools: "comma,separated,names") -> plain text
```

Returns a plain-text schema block (no JSON), one tool per section. Filtering
changes only the `ListTools` response object; the FastMCP manager retains the
real schema and dispatch rejects unknown arguments. Environment escape hatch:
`UNITY_MCP_FULL_SCHEMAS=1` disables stripping (default off).

This reduces the schema payload exposed on each turn. It is enabled by default;
discovery-gated tools show a stub until explicitly enabled in session.

### Chat Relay Permissions and User Input

ACP (Agent Communication Protocol) backend events are normalized server-side in `server/src/unity_mcp/stream_transform.py` and dispatched directly to `MCPChatWindow.HandleEvent()` with no intermediate parser:

- `PermissionPrompt` events render `ToolApprovalCard`, apply Ask/Agent policy, and send responses via `RelayBackend.SendControlResponse()`
- `AskUser` events render `AskUserCard` with user choice collection
- `PlanUpdate` events render `PlanStepCard` with Approve/Reject buttons for agent plan steps
- `FileChange` events display brief change notifications
- `CapabilitiesChanged` events update provider capability state

Do not add backend-native JSON-RPC or stream parsers to the C# window. The canonical architecture is `AI/architecture.md` under **Chat Relay System**.

### Middleware (`UNITY_MCP_MIDDLEWARE=1`)

The current layers and order are defined by `middleware_pipeline.py` and its
focused tests. They cover retry/circuit behavior, taint and dead-write guards,
path/alias resolution, blast-radius and batch checks, optional configured
sampling verification, Play Mode routing, caching/distillation, and
post-mutation evidence. Do not maintain a numeric layer count here.

**Alias Resolution (middleware_alias.py):**

Two hooks wired into `wrap_send()` (middleware_pipeline.py):
- **Hook 1 (pre-call):** resolve `$name` in arg values using `_alias_cache` before the call reaches Unity. Whole-value only: `"$hp"` resolves, `"/prefix/$hp"` does NOT (define a VAL alias for the full path instead). Per-key extraction from pipe format: `path`/`paths` → `segment[0]`, `component` → `segment[1]`, `field`/`prop` → `segment[2]`, all others → full pipe value. Comma-separated keys (`paths`, `queries`, `checks_before`, `checks_after`) are split, each token resolved, rejoined.
- **Hook 2 (post-call):** `get_aliases` populates `_alias_cache` from bare
  `name=value` lines. The `get_hierarchy` alias-block parser/stripper is a
  compatibility safety net; current Unity hierarchy responses do not emit that
  block.
- Cache format: `{name: "path|comp|field"}` — keys WITHOUT `$` prefix.
- Cache cleared on `reset_session()`.
- **Batch $alias guard (REMOVED v0.78.8):** `$alias` in batch DSL IS supported — `BatchHelper.cs` calls `AliasExpander.ExpandText()` C#-side before key=value parsing. No Python-side guard. Python pre-call alias hook still resolves `$name` in direct (non-batch) tool args.
- **`AliasExpander.BuildPipePath` (v0.78.11):** `GetTable()` previously returned only `a.path` for `ValPath` aliases, dropping `|component|field`. Fixed via private helper `BuildPipePath(QueryAlias a)` that appends `|component` and `|field` when non-empty, so expansion in `ExpandText()` always delivers the full pipe-format value (`path|Comp|field`) needed for C#-side key extraction.

**Middleware Pipeline Order (v0.57.0, commit 85c03bf; v0.72.x: play-mode fail-fast added):**

Guard conditions and reroute logic have been reordered for correctness:

1. **Cache-above-circuit check** (PrefetchCache, must run first even when circuit HALF_OPEN)
2. **Circuit breaker check** (prevents requests during outage)
3. **Pre-call checks first** (retry, taint, dead-write, blast-radius, verification, batch conflicts) — **guards see ORIGINAL cmd before reroute**. Read-only batches (`_is_batch_readonly()`) skip blast-radius and verification checks entirely (v0.78.10).
3.5. **Play Mode fail-fast guard** (`check_play_mode_required` — blocks `_RUNTIME_ONLY_CMDS` when `_play_state_known=True` and `is_playing=False`; returns early before TCP)
4. **Play mode auto-routing** (`reroute_cmd` — applied AFTER guards)
5. **Tier C features** (speculation tracking, lessons, inference)
6. **Command execution** (actual send to Unity)

**Read/write classification:** `middleware_types.py` owns command-level
classification, including dual-use `editor` actions. The exact sets are tested
against `ToolSpec`; do not duplicate their roster in documentation.

**The fix:** Previously reroute was applied BEFORE guards, allowing Play Mode reroutes to bypass safety checks (taint, dead-write detection). Now guards check original command intent, then reroute applies. Example: `update_player_pos` in Play Mode would reroute to `set_runtime_property`, but taint checks now see `update_player_pos` intention first.

### Additional Env-Gated Features

| Env Var | Default | Feature |
|---------|---------|---------|
| `UNITY_MCP_HINTS` | `1` (on) | ToolHinter — suggests underused tools. Set `=0` to disable |
| `UNITY_MCP_BUDGET` | `1` (on) | CostTracker/BudgetRouter — configured sampling spend tracking. Set `=0` to disable |
| `UNITY_MCP_SCENE_BRIEF` | off | SceneBrief — injects scene context on first call |
| `UNITY_MCP_SPECULATION` | off | SpeculativeLayer — speculative prefetch |
| `UNITY_MCP_LESSONS` | off | LessonStore/LessonRecorder — learns from usage patterns |
| `UNITY_MCP_WATCHDOG` | off | ProactiveWatchdog — background validate_references + console scan |
| `UNITY_MCP_INFERENCE` | off | SessionContext/Inferrer — argument inference from session |
| `UNITY_MCP_DISTILL` | `1` (on) | ResponseDistiller — heuristic response compression (set in server.py via setdefault); strip_defaults now always applies to {get_component, inspect, get_object_detail} regardless of this flag (use `_no_strip=1` arg to opt-out) |
| `UNITY_MCP_FULL_SCHEMAS` | off | Deferred Schema Loading — set `=1` to disable schema stripping (return full inputSchema for all tools instead of stubs) |

## Implementation Notes (for Developer)

### Tool Pattern

```python
@mcp.tool()
async def tool_name(arg1: str, arg2: int = 10) -> str:
    """Short description under 20 tokens."""
    return await _send("cmd_name", {"arg1": arg1, "arg2": arg2})
```

`_send()` raises `ToolError` on `!ok`, returns `data` or a file path, and routes
through the middleware pipeline when `UNITY_MCP_MIDDLEWARE=1`. Timeout ownership
is split between typed wrappers, the 120s Python retry session, and Unity's
per-command request deadlines; use the canonical table in
[`AI/tcp-bridge.md`](tcp-bridge.md).

### Consolidated Tool Pattern (action-based)

```python
@mcp.tool()
async def animation(action: str, path: str, clip: str | None = None) -> str:
    """Animation clips. Actions include get, create, edit/key operations, preview, events, wrap/framerate, and get_clip_path."""
    return await _send("animation", {"action": action, "path": path, "clip": clip})
```

### Plugin Registration

```python
# plugins/my_plugin.py
def register(mcp, send_fn, args_fn):
    @mcp.tool()
    async def my_tool(arg: str) -> str:
        return await send_fn("my_cmd", {"arg": arg})
```

## Code Locations

- Server: `server/src/unity_mcp/server.py`
- Bridge: `server/src/unity_mcp/bridge.py`
- ConnectionSlot: `server/src/unity_mcp/connection_slot.py`
- Lockfile: `server/src/unity_mcp/lockfile.py`
- Compile probe: `server/src/unity_mcp/compile_state.py`
- Middleware: `server/src/unity_mcp/middleware.py`
- Schema Registry (deferred): `server/src/unity_mcp/tools/schema_registry.py`
- Tools: `server/src/unity_mcp/tools/`
- Plugins: `server/src/unity_mcp/plugins/`
- Plugin API: `server/src/unity_mcp/plugin_api.py`
- Resources: `server/src/unity_mcp/resources.py`
- Tests: `server/tests/`

## TDD Scenarios (for Developer)

Tests organized by module in `server/tests/`:
- `test_server.py` — tool registration, _send helper, ToolError handling
- `test_bridge.py` — TCP connection, circuit breaker, heartbeat, keepalive, DomainReloadError
- `test_connection_slot.py` — single connection slot management
- `test_lockfile.py` — exclusive lock, stale cleanup, PID liveness
- `test_compile_state.py` — probe signals, estimated remaining
- `test_middleware.py` — each middleware layer independently
- `test_middleware_play_guard.py` — play mode fail-fast guard: state-unknown passthrough, edit-mode block, watch_remove exclusion
- `test_middleware_read_cmds.py` — `READ_CMDS`/`WRITE_CMDS` classification,
  `_is_batch_readonly()` edge cases (empty, comments, editor dual-use, mixed
  read/write), and read-only batch guard bypasses. Keep changing set membership
  and test counts in source and run evidence, not this guide.
- `test_tool_descriptions.py` — all TIER1 tools have `[Play Mode]` prefix where runtime=true
- `test_docstring_crossrefs.py` — all `use \`tool\`` cross-references in docstrings name real tools in _SPECS
- `test_gating.py` — tier filtering, category enable/disable
- `test_server_filtering.py` — `install_initialized_hook`: label sent for non-default client ("Cursor", "Codex"), skipped for "Claude Code", skipped when `client_params` is None
- `test_tool_schema_coverage.py` — 7 FastMCP contract tests: validates actual JSON Schema generated by FastMCP for TIER1 tools (required params, type annotations, optional with defaults); catches schema drift between Python signatures and what MCP clients see
- `test_plugins.py` — plugin loader, skip env, error handling
- `test_tools_*.py` — per-tool argument validation and response parsing

**C# TestRunner `DeleteTempScene` (v0.78.11):** `RunFinished` now schedules `EditorApplication.delayCall += DeleteTempScene`. The helper replaces the active scene with an empty one if it still points at the temp path (prevents dirty-scene dialog), then deletes the temp asset via `AssetDatabase.DeleteAsset`. Avoids leaving a dangling `__UnityMCP_Temp.unity` after every NUnit run.

## Review Checklist (for Reviewer)

- [ ] Tool descriptions < 20 tokens each
- [ ] All tools async
- [ ] ToolError for user-facing errors
- [ ] Logging to stderr, not stdout
- [ ] Type hints everywhere
- [ ] New tools added to gating.py (TIER1 or category)
- [ ] Plugin tools use `register(mcp, send_fn, args_fn)` pattern
- [ ] Middleware layers idempotent and env-gated

## Deployment Notes

- **Python changes** (F03, F04, F08, F12): take effect only after MCP server restart (`/mcp` command or process restart). Live MCP server will continue showing old behavior until restarted.
- **C# changes** (F02, F13, F18): live immediately after Unity recompile.

## Related

- API rules: `AI/api-design-standards.md`
- Bridge: `AI/tcp-bridge.md`
- Architecture: `AI/architecture.md`
- Batch: `AI/batch.md`
