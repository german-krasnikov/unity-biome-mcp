# Feature: TCP Bridge

## Overview

TCP communication between Python MCP Server and Unity Editor Plugin. Includes heartbeat-driven reconnects, compile state probing, one Python connection slot per MCP session, and the server lockfile.

## Architecture (for Architect)

```
Python (AsyncIO)                          C# (Unity)
┌────────────────────┐                   ┌──────────────────┐
│ ConnectionSlot     │                   │ MCPServer         │
│  └─ UnityBridge    │ ←── TCP:9500 ──→ │  ├─ HandleClient  │
│                     │                   │  ├─ ProcessQueue  │
│ CompileStateProbe   │                   │  ├─ PortDiscovery │
│ Lockfile (fcntl)    │                   │  └─ StateFile     │
│ CrashLogger         │                   │                   │
│ Heartbeat (15s/5s/  │                   │ going_away event  │
│   2s, reconnect)    │                   │ SO_KEEPALIVE      │
│                     │                   │ Per-cmd timeouts  │
└────────────────────┘                   └──────────────────┘
```

### Protocol

```
[4 bytes: uint32 BE length][UTF-8 JSON payload]
```

Max message size: 10MB.

### Request Format

```json
{"id": "a1b2", "cmd": "get_hierarchy", "args": {"depth": 3}}
```

### Response Format

```json
{"id": "a1b2", "ok": true, "data": "..."}
{"id": "a1b2", "ok": false, "err": "Not found"}
```

### Event Frame (server → client, no id)

```json
{"ev": "going_away", "reason": "domain_reload"}
```

### Command: search_context

**Purpose:** Return searchable scene objects and assets for MCP dynamic resource registration and chat mention indexing.

**Request:**
```json
{"id": "abc123", "cmd": "search_context", "args": {"query": "", "limit": 200, "types": null}}
```

**Args:**
- `query` (string, optional): filter text (empty = all)
- `limit` (int, optional): max results returned (default 30, capped at 200)
- `types` (string, optional): restrict to type codes (`"go"` for GameObjects only, or comma-separated: `"go,cs,pfb"`)

**Response Format (TSV):**
```
go	/Root/Player	Player
cs	Assets/Scripts/Player.cs	Player
pfb	Assets/Prefabs/Enemy	Enemy
mat	Assets/Materials/Default	Default
so	Assets/Config/Settings	Settings
```

Columns:
1. Type code: `go` (GameObject), `cs` (C# script), `pfb` (prefab), `mat` (material), `so` (scriptable object), `scene`, `tex`, `model`, `audio`, `anim`, `shader`, `folder`, `asset`
2. Path: hierarchy path (for go), asset path (for others)
3. Display name: friendly label

**Usage:** Python MCP Server calls this during `refresh_dynamic()` to populate the `biome://` resource catalog.

## Implementation Notes (for Developer)

### Python Client (bridge.py + bridge_heartbeat.py + bridge_reload_state.py)

**UnityBridge** — single TCP connection to one Unity instance.

Current invariants:

- `BridgeState` is `DISCONNECTED`, `CONNECTED`, `DOMAIN_RELOADING`, `FAILED`,
  `DORMANT`, or `WAKING`. [`connection-tools.md`](connection-tools.md) owns the
  user-facing status meanings.
- `send()` queues requests through one serial consumer. Each request carries an
  operation ID; a retry after a committed write carries `retry_op_id` so Unity
  can suppress duplicate execution.
- `DomainReloadTracker` marks a `going_away` event for up to 90 seconds and is
  cleared by a successful response or reconnect. A new send fails fast while
  that marker is active; callers use `sync_unity` for the compile/reload
  lifecycle instead of waiting inside an unrelated tool call.
- `RetryPolicy` permits retries only for commands classified retry-safe. Busy or
  connection-refused attempts use bounded 2/4/8-second delays; an ordinary
  transient failure receives one grace retry. `SESSION_TIMEOUT` bounds the
  complete request lifecycle.
- Connected bridges ping every 15 seconds with a 5-second ping timeout. The
  connection lock serializes ping and request write/read cycles. Protocol
  mismatch or a dead socket closes immediately; repeated timeouts from a live
  process use the bounded stall policy.
- A disconnected heartbeat polls after 5 seconds when Unity is busy and 2
  seconds otherwise. Reconnect attempts have a separate 5-to-60-second
  exponential cooldown with jitter. `DORMANT` disables heartbeat reconnect;
  the next request transitions through `WAKING`.
- Reconnect candidates must prove the expected project and protocol version.
  A live PID/port pin is reused only while it still identifies that project;
  refusal or identity mismatch returns to project-aware discovery.
- Sockets use `TCP_NODELAY` and `SO_KEEPALIVE`. `close()` atomically detaches the
  reader/writer and uses the platform-appropriate shutdown direction.

### ConnectionSlot (connection_slot.py)

Single-connection manager (replaces former multi-connection BridgeManager):
- `connect(port, host)` — create/connect a bridge (closes previous if any)
- `close()` — stop heartbeat and close bridge
- `bridge` property — the single UnityBridge instance
- `connected` property — shortcut for bridge.connected
- `status` property (v0.78.10) — delegates to `bridge.status`; returns `"disconnected"` when bridge is None
- No `reconnect()` method — reconnection handled by UnityBridge heartbeat loop

**UnityBridge.status (v0.78.10):** Semantic connection state for user-facing display:
- `"connected"` — writer is open and not closing
- `"domain-reloading"` — `BridgeState.DOMAIN_RELOADING`
- `"disconnected"` — `BridgeState.FAILED` (startup grace expired)
- `"dormant"` — idle suspension intentionally closed the TCP connection
- `"waking"` — an incoming request is reconnecting a dormant bridge
- `"reconnecting"` — all other states (attempting reconnect)

Used by `list_connections` to replace the binary connected/disconnected boolean.

### Port Discovery & TCP Probe (server_filtering.py) — v0.23.0, v0.36.0

Port discovery reads `~/.unity-biome-mcp/ports/{pid}.port` files. **v0.23.0:** Adds `_tcp_probe(port, timeout=0.2)` — quick TCP handshake to verify port actually listens before returning. Filters out stale discovery files (port written but server not yet bound, or server crashed leaving orphan file). Candidates prioritized: env UNITY_MCP_PORT → CWD project path match → newest mtime → default 9500.

**v0.36.0:** `_is_pid_alive(pid)` cross-platform check (Windows: OpenProcess/CloseHandle, Unix: os.kill(pid,0)) replaces naive kill check. C# MCPServer writes `{pid}.port` discovery files.

**v0.96.1 Legacy fallback (`paths.py`):** `iter_port_files(pattern, primary_dir)` yields port files from primary `~/.unity-biome-mcp/ports/` AND legacy `~/.unity-mcp/ports/`. Deduplicates by filename (new dir wins). `config/resolver.py:find_port()` uses this iterator so port discovery works for users who haven't migrated to the new path yet.

### CompileStateProbe (compile_state.py)

Simplified detector for Unity C# compile/domain-reload:
- **State file**: reads `~/.unity-biome-mcp/state/port-{port}.state` via `unity_state.py` (ready/compiling/reloading/restarting)
- `is_process_dead()` — cross-checks PID from port file
- `has_strong_busy_signal()` — state file (authoritative) then lock file fallback
- `_lock_file_exists()` — checks Unity's BeeDriver Lock file

### Session Presence Locks (lockfile.py)

Each MCP session owns
`~/.unity-biome-mcp/server-{port}-{pid}.lock`:
- Multiple sessions can coexist on the same Unity port.
- The process takes a non-blocking lock on its own presence file.
- `cleanup_stale_locks()` deletes files whose PID is no longer alive.
- No process is signaled or terminated by lock acquisition or cleanup.
- Windows locks a sentinel byte; POSIX uses `flock`.

### Crash Logging (crash_log.py)

Append-only JSONL crash log for unhandled exceptions:
- `log_crash(exc, *, log_dir=None)`: module-level function that writes `{"ev":"crash", "exc":"Type", "msg":"...", "tb":"...", "t":timestamp}` to `crash.jsonl` (defaults to `~/.unity-biome-mcp/crash.jsonl`)
- Auto-creates parent dir, silent on I/O failures
- Integrated into `main()`: outer try/except catches `BaseException` → calls `log_crash()` → re-raises (preserves clean shutdown for `KeyboardInterrupt`, `SystemExit`, EPIPE)
- **CrashLogger class**: JSONL append-only logger with rotation (500 entries max, 15MB size limit) — logs disconnect, reconnect events (older feature). Separate from module-level `log_crash()` used for unhandled server exceptions.

### Parent PID Monitoring (bridge_heartbeat.py)

The heartbeat compares the current parent PID with the value captured at import.
On mismatch it reads `GlobalConfig`: permanent-bridge mode keeps running, while
termination mode waits the configured orphan grace period (two minutes by
default), schedules a bounded hard exit, and stops the heartbeat. Returning to
the original parent clears the orphan timer. Environment overrides are owned by
`global_config.py`.

### C# Server (MCPServer.cs) — v0.23.0 SO_REUSEPORT Recovery

- **Main TCP listener** on port 9500 (configurable via `UNITY_MCP_PORT` env var)
- **Chat TCP listener** on port 9501 (or `main_port + 1`; configurable via `UNITY_MCP_CHAT_PORT` env var) — separate connection for in-Unity chat
- **Reload TCP listener** on port 9600 (independent compile-unit `com.unity-biome-mcp.reload/`) — handles rapid recompilation without domain-reload blocking
- **State file** written to `~/.unity-biome-mcp/state/port-{port}.state` with format: `state\ntimestamp\npid\nepoch` (e.g., "ready", "compiling", "reloading", "compile_failed")
- Max message size: 10MB
- SO_KEEPALIVE with platform-specific tuning (idle=60s, interval=10s, count=3; relaxed from 10s/5s to survive macOS App Nap timer coalescing)
- **Windows `LingerOption(true, 0)` (v1.0.2, `#if UNITY_EDITOR_WIN`):** Set on every accepted client socket in `ClientConnectionHandler.cs` and on evicted sockets in `ClientSlot.cs`. Sends RST on `Dispose()` instead of FIN, preventing TIME_WAIT accumulation. Required because the Windows listener uses `ExclusiveAddressUse`, which blocks rebind if any local socket is in TIME_WAIT — a domain reload disconnect could leave a socket in TIME_WAIT and prevent the server from restarting on the same port.
- **SO_REUSEPORT (v0.23.0, macOS/Linux only):** Enables port reuse for rapid reconnect after server crash or process termination. Windows doesn't require it (already has soft TIME_WAIT). Prevents "address already in use" during recovery without waiting for kernel TIME_WAIT timer.
- Up to 8 concurrent Unity-side client slots; when all slots are occupied, slot eviction is handled by `ClientSlot`
- Client generation tracking: prevents stale handlers from clearing shared state
- Lifecycle hardening: `IsRunning` property guarded with try/catch for ObjectDisposedException; `Stop()` wraps listener teardown with try/catch; `OnBeforeReload()` wrapped with try/catch
- Socket shutdown: `Shutdown(Both)` before `Stop()` in OnBeforeReload and Stop (TCP_NODELAY + shutdown both directions → faster port release)

**Bind retry:**
- Up to 4 attempts (3 on same port, 1 fallback to free port)
- Linear backoff: 400ms × (attempt + 1)
- Re-registration of watchdog + heartbeat callback on success

**KillPhantoms:** `MCPServer.KillPhantoms()` is an explicit status-menu action.
It asks each `ClientSlot` to close inactive client entries while holding the
slot lock. It does not scan PID lockfiles or run automatically at startup.

**Watchdog (Cycle 16+):**
- Separate `EditorApplication.update` callback (WatchdogTick)
- Monitors server liveness, restarts if dead within 5 seconds
- Properly unregistered in Stop(), OnQuit(), OnBeforeReload()
- Re-registered in StartAsync() after bind succeeds

**Port discovery:**
- Writes `~/.unity-biome-mcp/ports/{pid}.port` (port, project path, project name)
- Python auto-discovers port from these files

**State file:**
- Writes `~/.unity-biome-mcp/state/port-{port}.state`
- A compilation start writes `compiling`. A failed compilation writes
  `compile_failed`; a successful compilation deliberately remains `compiling`
  until the reloaded server binds and `StartAsync` writes `ready`.
- Reload and bind lifecycle paths also write `reloading` and `bind_failed`.

**Domain reload handling (OnBeforeReload):**
1. Sets `_shuttingDown = true`
2. Writes "reloading" to state file
3. Sends `{"ev":"going_away","reason":"domain_reload"}` synchronously
4. Cancels CTS tokens, closes client + listener
5. Does NOT delete port file (port survives reload)
6. Re-starts via `[InitializeOnLoad]` static ctor `delayCall` after reload

**MCPSettings OnWantsToQuit Flush (v0.57.0 — MCPSettings.cs):**
- Registers `EditorApplication.wantsToQuit += OnWantsToQuit` callback in static ctor
- Ensures all EditorPrefs (tool enabled flags, catalog) flushed before Editor quit
- **Impact:** prevents unsaved settings loss on unclean shutdown (e.g., force-kill)

**Fast-path commands** (bypass main thread dispatch):
- `client_hello`, `ping`, `get_version`, `status`, `get_enabled_tools`

### Timeout Layers

| Layer | Current contract |
|---|---|
| Python retry session | `SESSION_TIMEOUT=120s`, override with `UNITY_MCP_SESSION_TIMEOUT`; checked between attempts |
| Typed wrapper response wait | Tool-specific: for example `batch` defaults to 75s and `run_playtest` waits for its internal timeout plus a 20s transport buffer |
| Unity request deadline | `run_tests`/`run_playtest`: 130s; `batch`: 65s; `wait_until`/`move_to`/`test_step`: 30s; default: 25s |
| Operation-internal timeout | Passed in command arguments; it cannot extend the Unity request deadline |

The heartbeat uses a separate 15s ping interval and does not replace command
timeouts. Its disconnected-startup guard is not a tool-call deadline.

**Batch Atomic Timeout Rollback (v0.57.0 — BatchHelper.cs):**
- `batch(atomic=true, timeoutMs=25000)` — automatic rollback for Undo-recorded Unity changes
- If any sub-command times out (elapsed > timeoutMs), the batch stops and reverts its Undo group
- Opens named UndoGroup before first sub-command, reverts all ops on timeout/error
- Summary includes `ATOMIC_ROLLBACK: reverted ops 0..N` when rollback occurs
- Non-atomic batch follows `on_error`; the default `continue` processes remaining operations
- Prevents partial state corruption when timeout interrupts mid-batch

**Durable test dispatch**
- Consumer agents use `run_tests_wait()`; repository/disposable-worker runs use `run_unity_tests.py`
- Direct `run_tests()` is low-level nonblocking API and returns `request_id`, `run_id`, `utf_guid`, and `state`
- An explicit protocol caller polls only `get_test_run(run_id)` for that exact run
- `run_playtest()` is synchronous and returns its final playtest report; do not poll `get_test_results()` for it
- On an uncertain start, resolve the original `request_id`; never create a replacement request
- Timeout, disconnect, or port movement is nonterminal until the exact durable snapshot reconciles

Legacy `get_test_results` and `get_test_progress` remain diagnostic facades. An
uncorrelated latest result is never acceptance evidence.

## Code Locations

- Python bridge: `server/src/unity_mcp/bridge.py` (UnityBridge TCP client, BridgeState enum, should_retry() v0.36.0, RuntimeError raising v0.57.0)
- Python bridge heartbeat: `server/src/unity_mcp/bridge_heartbeat.py` (HeartbeatMixin, 15s ping loop, startup grace deadline, hard deadline timer separation v0.57.0)
- Python domain reload tracker: `server/src/unity_mcp/bridge_reload_state.py` (DomainReloadTracker v0.36.0, 90s expiry as of v0.42.1, increased from 30s for 9-assembly window)
- Python connection slot: `server/src/unity_mcp/connection_slot.py`
- Python compile probe: `server/src/unity_mcp/compile_state.py`
- Python unity state: `server/src/unity_mcp/unity_state.py`
- Python lockfile: `server/src/unity_mcp/lockfile.py` (with v0.23.0 zombie detection)
- Python crash log: `server/src/unity_mcp/crash_log.py`
- Python server filtering: `server/src/unity_mcp/server_filtering.py` (with v0.23.0 TCP probe)
- Python server wrapper: `server/src/unity_mcp/server.py` (main() crash handler)
- C#: `unity-plugin/Editor/CommandRouter.cs`, `unity-plugin/Editor/MCPServer.cs`, `unity-plugin/Editor/BatchHelper.cs` (atomic timeout rollback v0.57.0), `unity-plugin/Editor/MCPSettings.cs` (OnWantsToQuit flush v0.57.0)
- Tests: see the source-backed [Tests](#tests) section and `AI/testing.md`.

## Reconnection Strategy

Unity domain reload closes the socket. Recovery is coordinated by the event,
state file, heartbeat, and project-aware port discovery:

1. Unity writes `reloading`, sends `going_away`, and closes the listener.
2. Python marks `DomainReloadTracker`, closes the socket, and fails new sends
   fast while the marker is active.
3. The disconnected heartbeat uses compile state only to choose its polling
   delay. The reconnect cooldown prevents concurrent retry storms.
4. A candidate must pass project identity and protocol checks before the bridge
   swaps its reader/writer pair and clears the reload marker.
5. Reconnect callbacks refresh the Unity tool cache and other session state.

Each Python process owns a per-PID presence lock. Multiple MCP sessions may use
the same Unity port; lock acquisition rejects only a duplicate from the same
PID, and stale dead-PID files are removed without terminating processes.

```
send(cmd, args, timeout=30.0)
  → enqueue one operation-id-bearing request
  → serial consumer writes frame and awaits the matching response
  → retry only when RetryPolicy classifies the command as safe
  
_heartbeat_loop():
  when connected:
    → await 15s
    → skip if lock held (RPC in progress)
    → _raw_ping(timeout=5s), with response-ID validation
    → dead socket/protocol mismatch closes immediately
    → live-process timeouts use the bounded stall policy
  when dormant:
    → do not reconnect until a request transitions the bridge to waking
  when disconnected:
    → await 5s (probe busy) or 2s (not busy)
    → reconnect only after the current 5–60s cooldown
```

## Tests

Python coverage is split by responsibility under `server/tests/test_bridge*.py`,
with focused lifecycle coverage in `test_connection_slot.py`, `test_heartbeat.py`,
`test_lockfile.py`, and `test_parent_death.py`. Unity-side framing, connection,
and startup behavior is covered by `unity-plugin/Editor/Tests/ClientConnectionHandlerTests.cs`,
`MCPServerSplitTests.cs`, and `MCPServerStartGuardTests.cs`.

Use [`AI/testing.md`](testing.md) for current commands and acceptance policy.
Do not copy test names or counts into this protocol document.
## Review Checklist (for Reviewer)

- [ ] Big-endian byte order (4-byte prefix)
- [ ] Max message size validation (10MB both sides)
- [ ] Lock on Python writes (asyncio.Lock)
- [ ] NoDelay = true on both sides
- [ ] SO_KEEPALIVE configured (idle=60s, interval=10s, count=3; ~90s dead peer detect; relaxed from 10s/5s to survive App Nap)
- [ ] Domain reload expiry window (90s as of v0.42.1, increased from 30s for 9-assembly compilations)
- [ ] Main thread dispatch via ConcurrentQueue
- [ ] Graceful shutdown (going_away event before close)
- [ ] Heartbeat reconnect logic correct
- [ ] Lockfile released on shutdown
- [ ] Port file cleaned up on exit
- [ ] State file written before compile/reload
- [ ] Heartbeat interval (15s) appropriate
- [ ] Reconnect cooldown (5s minimum by default) prevents thrashing
- [ ] Lifecycle guards: IsRunning, Stop(), OnBeforeReload() all try/catch protected

## Related

- Server composition: `AI/mcp-server.md`
- MCP Server: `AI/mcp-server.md`
- Architecture: `AI/architecture.md`
