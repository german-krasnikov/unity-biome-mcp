# Architecture Overview

Unity Biome MCP connects MCP-compatible clients to a Python server, which
orchestrates typed tools and communicates with a Unity Editor plugin over a
loopback TCP transport. This document owns stable system boundaries and
cross-language invariants. Domain details belong in the linked `AI/` references;
public workflows and exhaustive parameters belong in `docs/`.

## Process Boundaries

```text
MCP client
    │ MCP transport
    ▼
Python server (`server/src/unity_mcp/`)
    │ length-prefixed JSON over loopback TCP
    ▼
Unity Editor package (`unity-plugin/Editor/`)
    │ main-thread Unity API calls
    ▼
Scenes, assets, Editor state, runtime state, and test runners
```

The optional reload package (`unity-plugin-reload/`) provides an independent
recovery listener when the main Editor assembly is unavailable during a compile
or domain reload. The chat relay is a separate Python entrypoint that normalizes
backend events before the Unity chat UI consumes them.

## Distribution and Ownership

- `server/` is the installable Python MCP server. `server.py` is its composition
  root; typed wrappers live under `tools/`.
- `unity-plugin/` is the main Unity Package Manager package. Its Editor assembly
  owns the listener, command dispatch, Unity integrations, and Editor UI.
- `unity-plugin-reload/` is deliberately independent of the main plugin assembly
  so it can assist recovery when that assembly does not load.
- `unity-plugin/ClientSkills/` is the canonical bundled source for consumer-agent
  skills and agents. It is not part of the internal `AI/` documentation tree.
- `docs/` owns user-facing guidance and the generated public schema reference.
  `AI/` owns implementation contracts only.

See [`structure.md`](structure.md) for subsystem entrypoints. Use `rg --files`
for the exact current inventory.

## Request Lifecycle

### Python side

1. `server.py` registers built-in tool modules and resources, then loads external
   Python plugins.
2. A typed wrapper validates and normalizes its public arguments. A wrapper may
   orchestrate Python-local work, make one Unity request, or coordinate multiple
   Unity requests.
3. `ToolSpec` metadata supplies the default category, timeout, mutability,
   runtime-only, direct-only, and transport behavior.
4. When enabled, `middleware_pipeline.py` applies pre-call guards and
   normalization, dispatches through the bridge, and applies post-call evidence,
   cache, and response transforms.
5. `bridge.py` frames the command and correlates the response. Connection and
   reload state survive transient disconnects according to the bridge contract.

### Unity side

1. `MCPServer.cs` owns loopback listeners and connection lifecycle.
2. Network work stays off the Unity API thread. `MainThreadDispatcher` queues
   Unity work for `EditorApplication.update`.
3. `CommandRouter` expands cached aliases, applies guards, opens an Undo group
   for a mutating command when applicable, and invokes `CommandRegistry`.
4. Synchronous, asynchronous, and file-response handlers return the common
   protocol envelope. The Python bridge converts a failed envelope to a tool
   error.

Python-only orchestration names are rejected on direct TCP. Conversely,
`direct_only=True` means the public tool must use its typed wrapper rather than
the batch text DSL; it does not necessarily mean that the wrapper makes no Unity
request.

## Tool Contract Sources

No single handwritten roster is authoritative. Keep these sources aligned:

| Contract | Source |
|---|---|
| Public Python signature and docstring | Owning module under `server/src/unity_mcp/tools/` |
| Category, visibility tier, timeout, mutability, runtime and direct-only metadata | `server/src/unity_mcp/tools/tool_specs.py` |
| Session category visibility | `server/src/unity_mcp/tools/gating.py` |
| Deferred schema behavior | `server/src/unity_mcp/tools/schema_registry.py` and `server_filtering.py` |
| Unity handler and validation metadata | `unity-plugin/Editor/CommandRegistry.cs` registration sites |
| Argument-dependent read/write behavior | `middleware_types.py` and `CommandRegistry.IsMutating(cmd, argsJson)` |
| Exhaustive generated public parameters | `docs/tools-schema/` |

Tool visibility is a discoverability control, not an authorization boundary.
Read-only enforcement, Play Mode restrictions, compile guards, session mode, and
Unity tool settings are separate checks.

## C# Command Registration API

`CommandRegistry` is a public extension surface. Core code also uses internal
`CommandOptions` overloads, but external callers use the public bool-parameter
overloads:

```csharp
public static void Register(
    string cmd,
    Func<string, string> handler,
    bool mutating = false,
    bool runtime = false,
    string required = null,
    string optional = null,
    bool specialDispatch = false,
    bool alwaysAllowed = false,
    bool allowedDuringCompile = false,
    Func<string, string, string> fileHandler = null,
    string description = null,
    int maxResponseChars = 0);

public static void RegisterAction(
    string cmd,
    Func<string, string, string> handler,
    bool mutating = false,
    bool runtime = false,
    string required = null,
    string optional = null,
    bool alwaysAllowed = false,
    bool allowedDuringCompile = false,
    string description = null,
    int maxResponseChars = 0);

public static void RegisterAsync(
    string cmd,
    Action<string, string, TaskCompletionSource<string>> handler,
    bool mutating = false,
    bool runtime = false,
    string required = null,
    string optional = null,
    bool alwaysAllowed = false,
    bool allowedDuringCompile = false,
    string description = null,
    int maxResponseChars = 0);
```

Registration rules:

- Command names are unique; a duplicate is logged and skipped.
- `required` and `optional` are comma-separated parameter names. `null` for both
  marks a free-form contract; an empty string represents no parameters.
- `RegisterAction` adds `action` to the required contract automatically.
- `mutating` and `runtime` feed routing guards. Mixed read/write commands must
  also implement conservative argument-aware classification on both sides.
- `specialDispatch`, asynchronous handlers, file handlers, and externally owned
  file effects are not ordinary batch operations.
- `alwaysAllowed` and `allowedDuringCompile` are core trust flags. They are
  stripped when a third-party plugin registers a command.
- `description` and `maxResponseChars` travel with the registry entry.

The registry is initialized explicitly before the listener is treated as ready.
Do not restore eager static registration or a second handwritten metadata table.

## Guard and Trust Boundaries

The layers are complementary:

- The Python endpoint checks `UNITY_MCP_READ_ONLY` before dispatch and uses
  source-derived mutability in middleware when enabled.
- The Unity listener separately reads its project `readOnly` setting and blocks
  mutating commands. Configure both boundaries when both must be read-only.
- Unity checks registry readiness, Python-only misuse, chat session mode,
  compilation state, Play Mode rules, runtime requirements, read-only state, and
  per-tool settings before execution.
- Conditional commands fail closed when an action or flag is absent, malformed,
  or not in a known read subset, except where the command contract explicitly
  defines a read default.
- Tool/category visibility does not prevent a caller that already knows a tool
  name from invoking it; it is not authentication.
- The listeners bind to loopback. The transport does not provide an independent
  authentication boundary. See [`../SECURITY.md`](../SECURITY.md) for the user
  threat model and supported controls.

`execute_code` is mutating and can change Editor, runtime, scene, asset, file, or
process state. The configurable pattern scan is not a sandbox, and the default
Allow All level skips it. Unity Undo can cover only operations that actually
record Undo state; it cannot reverse arbitrary external effects.

## Mutation, Undo, and Batch

For normal mutating C# commands, `CommandRouter` opens and closes a named Unity
Undo group and records mutation evidence. This is a best-effort Unity-state
boundary, not a general transaction manager.

`batch` executes compatible command lines sequentially. Python rejects or
filters direct-only lines according to `on_error`; Unity validates and dispatches
the remaining lines. `atomic=true` reverts prior Undo-recorded Unity changes on
the first failure. File, asset-import, package, process, and other external side
effects may remain. See [`batch.md`](batch.md).

Higher-level scene transactions must use their explicit allowlist and
verification contract; they do not make arbitrary commands transactional.

## Failure Handling

**FailureCategory (MCP-DIAG-009):** Typed protocol-level cause classification
for command failures. Categories:

- `TRANSPORT_CLOSED` — Connection lost before response
- `CAPACITY_BUSY` — Unity TCP server at MaxClients capacity (retryable)
- `SESSION_MISMATCH` — Reconnect landed on different project
- `TIMEOUT` — Operation exceeded deadline
- `COMPILE_PENDING` — Compile or domain reload blocked the operation
- `PLAY_NOT_READY` — Play Mode readiness check failed
- `PROTOCOL_ERROR` — JSON/frame malformation
- `COMMAND_NOT_FOUND` — Tool name not recognized
- `UNKNOWN` — Unmapped exception

Use `categorize_failure(exc)` to convert exceptions to typed (category, detail)
tuples. This is distinct from `classify_failure()` which builds human-focused
messages for chat; categorization is machine-readable for routing and retry logic.

## Compile and Reload

Agents call the public `sync_unity` wrapper after external code or package
changes. `tools/sync.py` triggers the C# `sync` command, tracks its epoch, waits
for `SyncHelper` to report the matching ready or failed state, tolerates the
expected domain-reload disconnect, corroborates errors, and invokes bounded
internal recovery only when needed.

`force_refresh`, `recompile`, and `force_play_stop` are recovery implementation
details, not the public agent workflow. `sync_unity` is mutating because import,
compile, optional package resolution, and optional version bump can change
project or Editor state. See [`reload-reference.md`](reload-reference.md).

## Lifecycle Support

**Version tracking:** `BiomeVersion.cs` is the single source of truth for version
constants — Plugin (semantic version matching `package.json`) and Protocol (numeric
version matching `server/src/unity_mcp/bridge.py PROTOCOL_VERSION`). `mcp_status`
exposes both plus `python_version` for cross-language version diagnostics and
pre-release compatibility checks.

**Play Mode readiness:** `PlayModeEpochTracker` ([InitializeOnLoad]) tracks a
monotonic `play_epoch` (incremented on each `EnteredPlayMode` callback) and a
`world_ready` flag that becomes true after the first `EditorApplication.update`
fires in Play Mode (post-Awake/Start completion). This is the robust gate for
playtest setup phases; it replaces heuristic frame waits. `PlayReadinessTracker`
on the Python side queries both to implement the `wait_for_ready` fence in
`_enter_fresh_play`.

## Playtest Execution

`PlaytestParser` converts the DSL to a `ParseResult`; `PlaytestRunner` executes
setup, main, and teardown phases on Editor updates.

- Global `ABORT_ON_FAIL` or `run_playtest(abort_on_fail=True)` stops Play Mode
  after any failed step or automatic console failure and finishes immediately.
  All remaining setup, main, and teardown steps are skipped.
- Without global abort, a failed setup step skips the remaining setup and all
  main steps, then runs teardown.
- Without global abort, an ordinary main-step failure does not suppress later
  main steps or teardown.
- A per-step `WAIT_UNTIL ... ABORT` modifier applies only when that wait times
  out; it stops Play Mode independently of the global policy.

See [`runtime-playtest.md`](runtime-playtest.md) for tool orchestration and
[`playtest-dsl.md`](playtest-dsl.md) for grammar.

## Main-Thread Dispatch & Configuration Persistence

**Main-thread dispatcher:** `MainThreadDispatcher` is the single queueing point for
all non-network Editor API calls. It uses `[InitializeOnLoad]` to subscribe directly
to `EditorApplication.update`, independent of `MCPServer` lifecycle (so it remains
active in batchmode and survives domain reloads). `Enqueue(action)` queues work;
`Drain(shuttingDown)` executes a snapshot-bounded batch per tick, with per-action
exception handling and reentrancy guards. This design prevents hangups when the
Editor loses focus (e.g., Task Manager interaction) or during domain reload cycles.
`EditorTickOnce` is retired; all callsites migrate to `Enqueue`. `delayCall` is
restricted to GUI-only contexts (Chat/UI, Wizard, menu callbacks) via allowlist tests.

**Atomic file writes:** Configuration, state, and test-run persistence files
(`MCP_Port.json`, `{pid}.port`, editor state, wizard config, test-run store) use
`AtomicFile.Swap(path, content)` to atomically replace files via temp + `File.Replace`.
This prevents data loss under concurrent file-locking scenarios (Windows OneDrive,
antivirus, network paths). The atomic pattern is standard and maintains durability
even when writes are interrupted by process death or lock contention.

**Durable test-run protocol:** Test run state is persisted to disk (`.unity-biome-mcp/tests/`)
and survives transport disconnect and caller timeout. Results include:
  - `health` (enum: `healthy`, `no_test_progress`, `editor_unresponsive`) — gate for
    stalled dispatch detection and disk-fallback recovery
  - `expected_count` (nullable int) — optional expected test count for zero-match validation
  - `issues` (list of dicts) — includes `ZERO_TEST_MATCH` warning when a filter produces no tests
  - Terminal states reconcile with the durable store on every query; if dispatch is stuck,
    a disk-fallback read returns the last stable snapshot

Lifecycle-gate self-heal: runs in `Finalizing` state longer than 180 seconds (since
execution boundary events, not dispatch) are auto-purged if no new test output arrives,
allowing subsequent runs to proceed.

**Error classification (UNITY-UNREACHABLE):** When the Editor process is hung, dead, or
otherwise unreachable, error responses now include explicit `UNITY-UNREACHABLE` verdict
instead of silent empty errors or stalled connection states. Liveness detection uses
~30-second ping intervals to catch dead processes quickly; `mcp_status` exposes
`liveness` (connected, connected-stalled, unreachable) for diagnostics.

## Extension Boundaries

Python plugins implement `register(mcp, send_fn, args_fn)` and use the supported
facade in `plugin_api.py`. Explicit custom categories should be registered via
the gating API. An uncategorized plugin tool is auto-enrolled through the legacy
`plugins` compatibility category, which shares SYSTEM visibility; visibility
still is not authorization.

C# plugins implement `IMCPPlugin`, register with `PluginRegistry`, and add
commands through the public `CommandRegistry` overloads. A plugin assembly
depends on the main Editor assembly, never the reverse.

## Maintenance Rules

- Update the smallest domain document that owns a changed invariant; link to it
  from secondary documents rather than copying parameter tables or rosters.
- Derive current inventories from source and tests. Release history belongs in
  [`CHANGELOG.md`](../CHANGELOG.md), and test policy/evidence belongs in
  [`testing.md`](testing.md).
- Follow [`testing.md`](testing.md) for the skills-freshness heuristic: strict
  failures, warning triage, and semantic review are distinct evidence levels.
- Validate cross-language mutability, runtime, registration, and schema parity
  whenever a public tool contract changes.

## Source Patch (Optional Mutation Mode)

**Scope:** Optional FSR-based body-only source patching. Enabled via `editor(action="mutation_mode")` intent and optional `com.unity-biome-mcp.source-patch.fsr` adapter package.

**Architecture boundary:**

```
Public MCP tools (editor / asset / mcp_status)
       ↓
Python internal router (_source_patch_mutation_is_on)
       ↓
C# SourcePatchHost (seam in asset.write_text path)
       ↓
Neutral SourcePatch asmdef (state machine, coordinator, no provider dependency)
       ↓
Optional FSR adapter (Roslyn body classifier, Harmony detour, exact-target loader)
```

**State machine:** `Unavailable` (package absent) → `Off` (default) → `OnReady/Busy` (intent ON, provider ready) ↔ `Recovery` (failed write). One causal domain reload on `Disabling → Off`.

**Limitations (release-tier):**
- Body-only mutations only (existing sync non-generic methods in `Assets/`)
- MonoBehaviour-derived mutable types unsupported (fail-closed Recovery)
- Async/iterator/lambda/local-fn/generic/overloaded methods rejected in preflight
- Single file at a time; no multi-file transactions
- No Play Mode mutations; auto-OFF on domain reload
- Mono backend only; deterministic one-at-a-time operations
- Qualified window: `6000.0.65f1` (Mono). Extension beyond this window is P2-07 (reviewed compatibility change).

**CI qualification:** Two required-pass cells (Unity 6000.0.65f1 × macOS ARM64, Linux x64) per `.github/workflows/fsr-qualification.yml`. Windows x64 documented as INFRASTRUCTURE_BLOCKED (headed-GUI unavailable on GH-hosted runners); engineering-supported with CI qualification pending. Adapter SHA pinned via `scripts/source_patch_provider_pin.json`.

See `.claude/skills/mutation-mode.md` for contributor guidance; `docs/features/mutation-mode.md` for user workflow.

## Related

- [`mcp-server.md`](mcp-server.md) — Python server implementation details
- [`tcp-bridge.md`](tcp-bridge.md) — connection and framing lifecycle
- [`tools-reference.md`](tools-reference.md) — registration and discovery rules
- [`api-design-standards.md`](api-design-standards.md) — API conventions
- [`testing.md`](testing.md) — canonical verification policy
- [`agent-chat.md`](agent-chat.md) — chat relay and provider architecture
