# Feature: MCP Server

## Overview

Python MCP server with 126 MCP tools for controlling Unity Editor. `_UnstructuredMCP(FastMCP)` subclass (v0.50.3) + ConnectionSlot + capability gating + 23 middleware layers. External plugins can add more tools dynamically. Structured output disabled on all tools to eliminate duplicate `content` + `structuredContent` in MCP responses (reduces size & parsing overhead).

## Architecture (for Architect)

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
├── resources.py        # MCP Resources (4 URIs: hierarchy, console, editor, categories)
├── tools/
│   ├── __init__.py     # Tool module registry
│   ├── objects.py      # get_component/inspect/find/set_property/create/delete/manage_component/set_active/rename_object/wire_event/unwire_event/set_material/set_parent/set_property_delta/transfer_object/object_diff
│   ├── scene.py        # hierarchy, console, compile_errors, screenshot(annotation_id), recompile, run_tests, get_test_results, scene (open_additive/close/set_active/list), search_scene, editor, checkpoint, fingerprint, scene_diff, save/load_session, screenshot_baseline/compare, get_changes
│   ├── code_intel.py   # find_references, compile_preflight, semantic_at, await_compile
│   ├── runtime.py      # invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest
│   ├── batch.py        # batch, references, validate_references + DRY serialization
│   ├── spatial.py      # spatial_query, validate_layout, get_spatial_context, scan_scene, check_colliders
│   ├── ui.py           # create_ui, set_rect, menu, shader
│   ├── codegen.py      # execute_code, get_schema, auto_fix, smart_build
│   ├── skills.py       # save_skill, use_skill, list_skills, apply_template, save_template, list_templates
│   ├── animation.py    # animation, timeline, animator, particle
│   ├── asset.py        # asset, material, prefab, scriptable_object, project_settings, get_enabled_tools
│   ├── connection.py   # list_connections, reconnect_unity
│   ├── autobatch.py    # setup_objects, set_properties, configure_objects
│   ├── gating.py       # Capability gating: TIER1 + category-based filtering
│   ├── do_tool.py      # NL intent → Haiku plan → batch execute
│   ├── ask_tool.py     # NL read-only question → route → Haiku summarize
│   ├── animator_intent_tool.py  # Domain-specific animator NL
│   ├── vfx_intent_tool.py       # Domain-specific VFX NL
│   ├── ui_intent_tool.py        # Domain-specific UI NL
│   ├── intent_common.py         # Shared intent infrastructure
│   ├── budget_tool.py           # budget_status tool (Haiku spend tracking)
│   ├── metrics_tool.py          # Performance metrics
│   └── schema_registry.py        # Tool schema lazy-loading
└── plugins/
    └── __init__.py              # 3-source auto-discovery (pkgutil, entry_points, UNITY_MCP_PLUGIN_DIRS)
```

### Tools (126 total)

**TIER1 — always visible (43 tools):**

Core (25): get_hierarchy, get_component, inspect, set_property, create_object, delete_object, manage_component, batch, scene, search_scene, set_parent, get_console, get_compile_errors, get_enabled_tools, discover_tools, editor, do, ask, ask_user, permission_prompt, reconnect_unity, list_connections, resolve_tool_schema, doctor, alias_status

Other (18): screenshot, run_tests, setup_objects, set_properties, configure_objects, find_references, compile_preflight, semantic_at, await_compile, sync_unity, invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest

### Compile-Tool Corroboration (v0.7.0+)

`get_compile_errors`, `await_compile`, `auto_fix`, and `ask` now cross-verify clean responses via `editor_log.py`: an out-of-band reader of Unity's `Editor.log` that catches cases where the in-plugin C# reporter is itself broken (stale bytecode, unsafe to trust). Only overrides when both signals agree: log shows errors AND dll is stale. Zero false positives (fresh dll trusted). Resolves P0 silent-blindness bug where plugin compile failures masked themselves.

**Category-gated (enabled via `discover_tools`):**

| Category | Tools |
|----------|-------|
| object | find_objects, get_object_detail, get_components_list, set_active, rename_object, clone_object, set_material, wire_event, unwire_event, auto_wire, set_property_delta, object_diff, transfer_object |
| animation | animation, timeline, animator, particle |
| asset | asset, material, prefab, scriptable_object, project_settings, shader, references, material_audit |
| advanced | execute_code, recompile, get_schema, auto_fix, smart_build, checkpoint, undo_last, validate_references, menu, diagnose, scan_scene, check_colliders, spatial_query, region_clear, navmesh_query, scene_health, set_llm_config, budget_status |
| ui | create_ui, set_rect, validate_layout, get_spatial_context, ui_intent, vfx_intent |
| runtime | get_test_results, get_perf, debug_animator, debug_physics |
| session | fingerprint, scene_diff, get_changes, save_session, load_session, screenshot_baseline, screenshot_compare, save_skill, use_skill, list_skills, apply_template, save_template, list_templates |
| debug | debug, snapshot, watch_add, get_watches, watch_remove, watch_clear, watch_reset, get_metrics |
| profiling | get_frame_stats, profile, get_memory |
| rendering | render_analyze, analyze_lod_culling |
| plugins | (auto-gated: tools registered without register_tools() call) |
| connection | (empty — list_connections and reconnect_unity are in TIER1/CORE) |

### Capability Gating (gating.py)

- TIER1 tools (42) always visible to LLM
- Categories enabled per-session via `discover_tools(category, enable=True)`
- Double-filtered: Python gating × Unity-side MCPSettings (tool cache from `get_enabled_tools`)
- Unknown (plugin) tools auto-gated to hidden `plugins` category by default
- Plugin self-registration: `gating.register_tools("category", tools_set)` adds tools to Tier2 category (no tier1 escape hatch — platform controls TIER1 membership)

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
# - Auto-discovered from ~/.unity-mcp/ports/*.port or UNITY_MCP_PORT env
# - Used throughout: lockfile creation, bridge connection, test harnesses

@asynccontextmanager
async def lifespan(app):
    # 1. Auto-discover Unity port from ~/.unity-mcp/ports/*.port or UNITY_MCP_PORT env
    # 2. Cleanup stale port-scoped config files (v0.53.0): unity-mcp-config-{port}.json older than 2h
    # 3. Acquire exclusive PID lockfile ~/.unity-mcp/server-{port}.lock
    # 4. Create ConnectionSlot, connect bridge
    # 5. Wire middleware layers (if UNITY_MCP_MIDDLEWARE=1)
    # 6. Wire ToolHinter (default on, disable with UNITY_MCP_HINTS=0)
    # 7. Wire budget tracking (default on, disable with UNITY_MCP_BUDGET=0)
    # 8. Wire optional layers: SceneBrief, SpeculativeLayer, Lessons, Watchdog, Inference
    # 9. Fetch enabled tools cache, start heartbeat, register reconnect callbacks
    yield
    # Shutdown: stop heartbeat, cancel watchdog, close bridge, release lock, delete own config

def main():
    _last_activity = time.monotonic()
    _start_idle_watchdog()  # daemon: exits if parent dies + idle > UNITY_MCP_IDLE_TIMEOUT (default 300s)
    transport = os.environ.get("UNITY_MCP_TRANSPORT", "stdio")
    if transport == "http":
        port = int(os.environ.get("UNITY_MCP_HTTP_PORT", "8765"))
        mcp.run(transport="streamable-http", host="127.0.0.1", port=port)
    else:
        mcp.run(transport="stdio")
```

**Idle watchdog (v0.53.0)** — daemon thread calling `os._exit(0)` after UNITY_MCP_IDLE_TIMEOUT seconds of inactivity. Key gate: `if os.getppid() == _ORIGINAL_PPID: continue` (line 40 in server.py) — only exits if parent process has changed (truly orphaned). Alive parent → watchdog remains dormant, acting as orphan-reaper only. Timeout=0 disables. Updated on every `_touch_activity()` call before MCP tool dispatch.

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
  - DomainReloadError on Unity `going_away` event frame

### Server Control (server_control.py)

- `list_servers` — list all running MCP server PIDs/ports (reads ~/.unity-mcp/server-{port}.lock files)
- `stop_server(port)` — graceful SIGTERM (Unix) or taskkill (Windows) shutdown of running server

### Compile State Probe (compile_state.py)

Simplified detector for Unity C# compile/domain-reload:
- **State file**: reads `~/.unity-mcp/state/port-{port}.state` (ready/compiling/reloading)
- `is_process_dead()`: PID cross-check from port file

### Auto-Batch (autobatch.py)

- `setup_objects(specs)` — create+configure multiple objects (one per line DSL)
- `set_properties(path, props)` — set multiple properties (component.prop=value)
- `configure_objects(config)` — configure multiple objects (/Path component.prop=value)
- All expand internally to `batch` commands

### Intent Meta-Tools

- `do(intent, dry_run)` — NL → Haiku plan → validate → batch execute
- `ask(question)` — NL read-only question → deterministic route → Haiku summarize
- `animator_intent`, `vfx_intent`, `ui_intent` — domain-specific NL intent tools (Tier2, discoverable via discover_tools)

### MCP Resources (resources.py)

4 resource URIs registered:
- `unity://scene/hierarchy` — current scene hierarchy summary
- `unity://console/errors` — recent console errors
- `unity://editor/state` — editor state (play mode, scene, selection)
- `unity://tools/categories` — available tool categories

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
- Memory efficiency: single instance, reused for all claude CLI calls

**Related:** Budget tracking in `sampling.py` (metrics, latency tracking) — wired from server.py lifespan.

### Plugin System

3-source discovery:
1. **pkgutil built-in**: discovers modules inside `plugins/` package
2. **entry_points**: `importlib.metadata.entry_points(group="unity_mcp.plugins")` for pip-installed packages
3. **UNITY_MCP_PLUGIN_DIRS**: env var pointing to filesystem directories with plugin modules

Each plugin implements `register(mcp, send_fn, args_fn)`. Plugin API facade (`plugin_api.py`) provides stable exports: `API_VERSION`, `RO`, `RW`, `RW_IDEM`, `DEL`, `SamplingService`, `strip_fences`, `sanitize_intent`, `register_dsl_tools()`, `register_read_cmds()`, `register_write_cmds()`, `register_tools()`, `register_features()`.

### Code Intel Tools (server 0.4.0)

**await_compile (NEW):** Read-only tool that blocks until Unity finishes C# compilation AND domain-reloading. Returns compile errors as plain text. Survives domain-reload disconnect via reconnect + re-query. `timeout=0` = instant snapshot. Replaces `sleep`-then-poll patterns.

- `find_references` — semantic search for usages of a symbol (method, property, class)
- `compile_preflight` — pre-compile validation + type inference for code edits
- `semantic_at` — AST analysis at a line:col position (type info, references, quick-fix suggestions)

### Deferred MCP Tool-Schema Loading (F4, server 0.3.0)

Non-core tools return a **stub inputSchema** `{"type":"object"}` from `list_tools` instead of full schemas. Full schemas are served lazily via a new meta-tool:

```
resolve_tool_schema(tools: "comma,separated,names") -> plain text
```

Returns a plain-text schema block (no JSON), one tool per section. Backwards-compatible: MCP dispatch doesn't validate against inputSchema, so stubbed tools execute normally. Environment escape hatch: `UNITY_MCP_FULL_SCHEMAS=1` disables stripping (default off).

**Token impact:** ~58-68% per-turn schema-token reduction. Enabled by default; discovery-gated tools show stub until explicitly enabled in session.

### Codex App-Server Elicitation Handling (Chat CLI backends, v0.53.1+)

**Problem:** Codex app-server gates MCP tool invocations with `mcpServer/elicitation/request` JSON-RPC (OpenAI issue #11816, no timeout) before executing mutating tools (e.g., `set_property`). Codex sets no timeout on the request, causing infinite spinner when the elicitation is silently dropped or not auto-accepted. Read-only tools don't trigger elicitation, so they pass through normally.

**Solution:** Layered handling in **asmdef `UnityMCP.Editor.Chat.CLI`** distinguishes requests (expect replies) from notifications (fire-and-forget).

**Layer 1: Approval Policy Suppression (Performance)**

Configure `approvalPolicy` in MCP client to suppress elicitation prompts:
- In `thread/start` request: `"sandbox":"danger-full-access"` (string)
- In `turn/start` request: `sandboxPolicy:{type:"dangerFullAccess"}` (object)

**Layer 2: Parser Auto-Accept (Correctness)**

**CodexAppServerParser.cs** detects the elicitation request and auto-replies immediately:
```csharp
case "mcpServer/elicitation/request":
{
    var rpcId = JsonHelper.ExtractString(line, "id") ?? "0";
    sink.Add(ChatEvent.AutoReply(ControlResponseBuilder.CodexElicitationAccept(rpcId)));
    break;
}
```

**ControlResponseBuilder.cs** formats the JSON-RPC 2.0 accept response:
```csharp
public static string CodexElicitationAccept(string rpcId) =>
    $"{{\"jsonrpc\":\"2.0\",\"id\":{FormatRpcId(rpcId)},\"result\":{{\"action\":\"accept\",\"content\":{{}}}}}}";
```

**Layer 3: Request vs Notification Invariant (Safety)**

**Critical distinction:** JSON-RPC requests have a top-level `"id"` field; notifications don't. Parser correctly ignores nested `id` fields (e.g., `params.turn.id`) via `JsonHelper.ExtractString(line, "id")` which is depth-aware (returns top-level keys only).

Unknown server requests with top-level `id` are auto-declined (not auto-accepted) to avoid granting unintended permissions:
```csharp
if (HasRpcId(line))  // top-level id present
{
    var rpcId = JsonHelper.ExtractString(line, "id") ?? "0";
    sink.Add(ChatEvent.Error($"[MCP Chat] Unhandled server request: {m} — auto-declined"));
    sink.Add(ChatEvent.AutoReply(ControlResponseBuilder.CodexElicitationDecline("codex:" + rpcId)));
}
// else: benign notification → ignore silently
```

**ChatEvent.cs** defines `AutoReply` kind (not rendered, transparent to UI; text holds raw JSON-RPC response).

**Why This Works**
- Mutating tools trigger elicitation → caught at parser, auto-accepted immediately → no spinner
- Read-only tools don't trigger elicitation → no request sent → passes through
- Unknown requests (safety net) → declined, never silently dropped → visible warning
- Shell/file approvals (safety gate) → surfaced as Error, never auto-accepted

**Files:** `CodexAppServerParser.cs`, `ControlResponseBuilder.cs`, `ChatEvent.cs` (all in `unity-plugin/Editor/Chat/CLI/`)

### Middleware (23 layers, `UNITY_MCP_MIDDLEWARE=1`)

Retry Watchdog, Confidence Decay (gated <0.5), Taint Tracking, Periodic State Injection (staleness-gated), Path Cache, Dead Write Elimination, Starvation Monitor, Blast Radius Tags, Incremental Verification, Workflow Phase FSM, Visual Verification (Haiku), Play Mode Auto-Routing, find_objects Cache Bypass, Batch Conflict Scan, Post-mutation Snapshot, Component Cache, Console Error Categorization, PrefetchCache (TTL 12s), HierarchyDiff, Distiller, Disambiguator, SchemaGuard, Asymmetric Reflection

**Alias Resolution (middleware_alias.py):**

Two hooks wired into `wrap_send()` (middleware_pipeline.py):
- **Hook 1 (pre-call):** resolve `$name` in arg values using `_alias_cache` before the call reaches Unity. Whole-value only: `"$hp"` resolves, `"/prefix/$hp"` does NOT (define a VAL alias for the full path instead). Per-key extraction from pipe format: `path`/`paths` → `segment[0]`, `component` → `segment[1]`, `field`/`prop` → `segment[2]`, all others → full pipe value. Comma-separated keys (`paths`, `queries`, `checks_before`, `checks_after`) are split, each token resolved, rejoined.
- **Hook 2 (post-call):** after `get_hierarchy`, parse `--- ALIASES ---` block → populate `_alias_cache`; strip block from result (LLM never sees it). After `get_aliases`, populate `_alias_cache` from bare `name=value` lines.
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

**READ_CMDS / WRITE_CMDS audit (v0.78.11, `middleware_types.py`):**
- `READ_CMDS` expanded from 15 → 43 entries: added `screenshot_compare`, `get_selection`, `get_capabilities`, `alias_status`, `get_aliases`, `list_connections`, `get_enabled_tools`, `budget_status`, `permission_prompt`, `get_test_results`, `get_test_progress`, `get_test_count`, `get_frame_stats`, `get_memory`, `get_metrics`, `get_perf`, `get_watches`, `debug`, `debug_animator`, `debug_physics`, `profile`, `object_diff`, `scene_diff`, `scene_health`, `material_audit`, `analyze_lod_culling`, `render_analyze`, `fingerprint`, `validate_layout`, `check_colliders`, `spatial_query`, `get_schema`, `get_changes`, `compile_preflight`, `await_compile`, `auto_fix`, `diagnose`, `list_scenarios`, `list_skills`, `list_templates`, `load_scenario`, `load_session`, `ask`, `ask_user`
- `compress_hierarchy` removed from `READ_CMDS` (dead command — does not exist in Unity plugin)
- `WRITE_CMDS` gains `rename_object` and `set_sibling_index`
- `_EDITOR_READ_ACTIONS: frozenset[str] = frozenset({"state", "project_path"})` — `editor` cmd is dual-use; only these two actions are reads; all others (play/stop/pause/step/select) are writes. Used by `_is_batch_readonly()` and `transition()` to avoid misclassifying editor state queries as mutations.

**The fix:** Previously reroute was applied BEFORE guards, allowing Play Mode reroutes to bypass safety checks (taint, dead-write detection). Now guards check original command intent, then reroute applies. Example: `update_player_pos` in Play Mode would reroute to `set_runtime_property`, but taint checks now see `update_player_pos` intention first.

### Additional Env-Gated Features

| Env Var | Default | Feature |
|---------|---------|---------|
| `UNITY_MCP_HINTS` | `1` (on) | ToolHinter — suggests underused tools. Set `=0` to disable |
| `UNITY_MCP_BUDGET` | `1` (on) | CostTracker/BudgetRouter — Haiku spend tracking. Set `=0` to disable |
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

`_send()` helper: raises ToolError on `!ok`, returns `data` or file path. Routes through middleware pipeline when `UNITY_MCP_MIDDLEWARE=1`. Per-command timeouts via `COMMAND_TIMEOUTS` dict (run_tests/run_playtest/fuzz_playtest: 120s, compile_preflight: 60s, batch: 60s, default: 30s).

### Consolidated Tool Pattern (action-based)

```python
@mcp.tool()
async def animation(action: str, path: str, clip: str = "", ...) -> str:
    """Animation CRUD. Actions: get|create|edit|add_key|remove_key|set_keys|set_loop|preview"""
    return await _send("animation", {"action": action, "path": path, "clip": clip, ...})
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
- `test_middleware_read_cmds.py` — 59 tests: READ_CMDS membership for all 43 entries, `_is_batch_readonly()` edge cases (empty, comments, editor dual-use, mixed read/write), readonly batch skips blast/verif/FSM guards
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
- **Pre-existing C# EditMode test failures**: 2 failures in `MCPPrefabTests.Revert_RevertsChanges` and `MCPValueParserTests.ValueParser_Enum_NegativeInt` are unrelated to Wave 0; their source files (prefab logic, ValueParser.cs) were not touched by any Wave 0 commit. C# NUnit tests for Wave 0 fixes written locally at `unity-test-project/Assets/Tests/Editor/MCPF02F13F18Tests.cs` (gitignored directory, not version-controlled).

## Related

- Skill: `.claude/skills/python-mcp.md`
- Bridge: `AI/tcp-bridge.md`
- Architecture: `AI/architecture.md`
- Batch: `AI/batch.md`
