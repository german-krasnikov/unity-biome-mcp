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

## Related

- [`mcp-server.md`](mcp-server.md) — Python server implementation details
- [`tcp-bridge.md`](tcp-bridge.md) — connection and framing lifecycle
- [`tools-reference.md`](tools-reference.md) — registration and discovery rules
- [`api-design-standards.md`](api-design-standards.md) — API conventions
- [`testing.md`](testing.md) — canonical verification policy
- [`agent-chat.md`](agent-chat.md) — chat relay and provider architecture
