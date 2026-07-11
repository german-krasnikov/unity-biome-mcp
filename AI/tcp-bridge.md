# Feature: TCP Bridge

## Overview

TCP communication between Python MCP Server and Unity Editor Plugin. Includes heartbeat (sole reconnect mechanism), compile state probing, single-slot connection, and exclusive lockfile.

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

## Implementation Notes (for Developer)

### Python Client (bridge.py + bridge_heartbeat.py + bridge_reload_state.py)

**UnityBridge** — single TCP connection to one Unity instance.

Key features:
- **BridgeState enum** (v0.36.0): DISCONNECTED | CONNECTED | DOMAIN_RELOADING | FAILED (startup grace expired). Tracks connection lifecycle state explicitly.
- **DomainReloadTracker** (v0.36.0, `bridge_reload_state.py`): Dataclass with 90s expiry tracking domain reload state independently from compile probe (v0.42.1: increased from 30s to 90s for 9-assembly window). Three methods: `mark()` (called on DomainReloadError), `clear()` (on successful send), `is_active()` (checks expiry + elapsed). Shared between `send()` retry logic and heartbeat.
- **should_retry()** (v0.36.0): Pure decision function extracting retry logic from _send_with_retry. Signature: `(error: Exception, attempt: int, session_deadline: float) → (bool, float, str)` returning (retry yes/no, delay_s, reason). On DomainReloadError: marks reload + state→DOMAIN_RELOADING. On any error: checks reload.is_active() or probe_busy(), backoff 2^(attempt+1) sequence: 2s→4s→8s (restored in v0.37.0, was regressed to 1s→2s→4s). Testable without mocking networking.
- Socket options: `TCP_NODELAY`, `SO_KEEPALIVE` (macOS: idle=60s, interval=10s, count=3; detects dead peers within ~90s)
- Heartbeat: 15s interval `_raw_ping()` (bypasses retry machinery). Starts only in `_reconnect()`, stops only in `close()` — fire-and-forget spawn on every `send()` was deleted (caused 45+ GB memory leak). Disconnected polling: 5s if probe busy, 2s otherwise.
- `_raw_ping()`: lightweight ping (default timeout=10s, heartbeat calls with 20s) that acquires lock, sends framed message directly on socket, validates response ID match. Prevents heartbeat from consuming RPC responses. Sends `UNITY_MCP_CLIENT` env var as `role` field; empty value → `"mcp"` (default).
- **`_ping_stall_failures` counter (v0.78.8 exception split)**: `asyncio.TimeoutError` (Unity alive, unresponsive — App Nap/heavy compile) → apply stall counter. `Exception` (ConnectionReset, IncompleteRead, OSError = dead TCP) → close immediately and reconnect (no stall tolerance). This distinction prevents 6-min zombie bridges where a dead TCP connection was treated as a transient stall. Stall logic: 3 consecutive `TimeoutError`s + `is_process_dead()` False → increment `_ping_stall_failures`, reset `_ping_failures`. After 6 stall windows (~6 min total), force-close. `TimeoutError` + process dead → close immediately. Resets on: DomainReloadError, ProtocolDesyncError, successful ping, confirmed-dead close, reconnect.
- **Self-cancel guard in `close()`**: `if asyncio.current_task() is not self._heartbeat_task: self.stop_heartbeat()` — prevents the heartbeat task from cancelling itself when it calls `close()`.
- **`connected` property**: simplified to `self._writer is not None and not self._writer.is_closing()`. Previous `select() + MSG_PEEK` check caused false negatives that triggered unnecessary reconnects.
- **Reconnect with Exponential Backoff (v0.52.7)**: Throttles retries via exponential backoff (MIN=5s → MAX=60s) with jitter ±10%. Backoff doubles on each failure, resets to MIN on successful TCP connect. Cooldown `_last_reconnect_at` now re-armed BEFORE try block (not only on success), preventing retry spam when port discovery returns None. `_reconnect_cooldown_ok()` uses current backoff value for sleep duration instead of fixed interval, preventing thundering herd when multiple servers reconnect. **v0.52.6 regression:** Fixed silent 9500 fallback — `read_unity_port(skip_probe=True)` now returns `None` instead of default port when no live candidates found; bridge preserves current port; caller (`doctor.py`) explicitly handles None with fallback. **v0.53.0 backoff re-arm:** Cooldown re-armed BEFORE reconnect attempt (`_last_reconnect_at = time.monotonic()` line 122 before `try`). Exponential formula (line 134–137): `backoff = min(backoff * 2 * (1.0 + jitter[-0.1…+0.1]), 60.0)`, reset to 5s on success (line 130). **MIN_RECONNECT_INTERVAL:** Base interval is 5s (only applies to heartbeat disconnected polling and cooldown gate; does not override exponential backoff which is independent). Exponential backoff series when domain reload active: 2s → 4s → 8s (capped) within 90s window (v0.42.1).
- `DomainReloadError`: raised when Unity sends `going_away` event frame → immediate close (no wait), triggers `mark_recompile_issued()` state probe update, fast reconnect. **v0.36.0**: heartbeat now calls `_reload.mark()` to extend retry window (v0.42.1: 90s, increased from 30s for 9-assembly window).
- **Atomic reader/writer close** (v0.36.0): Both reader and writer closed atomically within lock during _reconnect() to prevent zombie reads after close.
- **Pin guard (v0.52.6)**: Bridge caches `_pinned_pid` from initial connection to stick to same Unity instance. `_reconnect()` explicitly checks `_pinned_pid is not None` before trusting pin, preventing silent re-bind to different instance if pinned process dies. Falls back to port discovery if pin invalid.
- **`_reload_gate` (asyncio.Event)**: replaces fixed `asyncio.sleep()` during DomainReloadError retry. Always cleared on `domain_reload` retry path (v0.78.8 removed the `if not self.connected:` guard that could skip the clear). Set on successful reconnect. Wakes retries immediately on reconnect instead of sleeping the full backoff window.
- `send()` retry logic: idle errors get 1 grace attempt; busy (domain reload / probe) gets full retries with exponential backoff (capped at 8s). Session deadline `SESSION_TIMEOUT=120s` prevents infinite retries (overridable via env UNITY_MCP_SESSION_TIMEOUT for reasoning models like Codex o3/o3-pro; chat backend sets 300s). **v0.31.1 Fix**: When `DomainReloadError` is caught, `domain_reload_in_progress` flag is pinned to True for all subsequent retries within that send() call, preventing `_probe_busy()` re-evaluation from returning False too early (Editor.log may clear "compiling" status before TCP 9700 is restored). Non-DomainReloadError exceptions still allow probe re-evaluation per attempt. **v0.36.0: SESSION_TIMEOUT env override** — CliBackendBase injects UNITY_MCP_SESSION_TIMEOUT=300 so Python bridge allows longer think time for reasoning models without timeout. **v0.57.0 RuntimeError**: send() now raises `RuntimeError(f"_send_with_retry exhausted {MAX_RETRIES} retries without result for cmd={cmd!r}")` on max retries (previously returned None silently); errors are explicit, no silent command failures.
- Lock per connection for thread safety

**P0 (Cycle 17+) Fix — Domain Reload Sticky Flag**
- `_domain_reload_in_progress` now auto-expires after `DOMAIN_RELOAD_EXPIRY_S=90s` (v0.42.1: increased from 30s to 90s for 9-assembly window)
- Cleared on successful reconnect (when TCP fully restores)
- Prevents permanent "busy" state if domain reload never completes (e.g., compile error during reload)
- Regression: flag was set but never cleared, causing -32000 errors on all subsequent commands

**P2 (Cycle 17+) Fix — Startup Grace Latch Recovery**
- `_startup_grace_expired` death latch: `send()` now attempts one reconnect before raising ConnectionError
- Allows recovery from permanent-death state (grace period elapsed without successful connection)
- If reconnect succeeds, clears flag and proceeds; if fails, raises original error

**P4 (Cycle 17+) Safety Comment — Lock Serialization**
- Both `_raw_ping()` and `send()` hold `self._lock` for their full write→read cycle
- asyncio.Lock serializes them — heartbeat ping can never interleave with a tool-call response
- ID collision is impossible by design

**v0.54.1 — Focus-Loss CPU Storm Fix (Multi-CLI Reconnect Storm Prevention)**
- **Root Cause:** All socket I/O awaits in `MCPServer.cs` captured `UnitySynchronizationContext` (18 awaits, no `ConfigureAwait(false)`). Focus loss → `EditorApplication.update` throttle → task continuations freeze → heartbeat timeout → reconnect storm on focus regain.
- **Fix — C# Threading Model (MCPServer.cs, v0.54.1):**
  - **Socket I/O (RunAcceptLoop, HandleClientAsync):** All 18 awaits now use `ConfigureAwait(false)`. Continuations execute on ThreadPool, not main thread.
  - **Invariant: Unity API only on main thread** — No direct Editor API calls after ThreadPool continuations. All `Debug.Log*`, `EditorApplication.QueuePlayerLoopUpdate()`, and `RefManager.Invalidate()` marshaled via `_mainThreadQueue.Enqueue()` lambda.
  - **Domain Stamp Cache:** Volatile `_domainStamp` field cached on main thread in `StartAsync()`, read on ThreadPool by fast-path `get_version` (SessionState not thread-safe).
  - **Comments:** Added `// WARNING: All awaits here use ConfigureAwait(false)...` at method boundary (RunAcceptLoop, HandleClientAsync).
- **Fix — Python Reconnect Cooldown (bridge.py, v0.54.1):**
  - **Dual-gate:** `send()` and `_send_with_retry()` both check `_reconnect_cooldown_ok()` before first reconnect attempt. Prevents burst storms when multiple CLIs reconnect simultaneously.
  - **Jitter:** Retry delays now include ±10% random jitter to desynchronize reconnect attempts across multiple bridge instances.
  - **Observability:** Crash log enriched with `bridge_id` (unique per instance), `reconnect_reason`, `path` ("send" vs "heartbeat"). METRICS.inc("reconnect.send_path") for telemetry.
- **Defense-in-depth:** Atomic `_on_port_change` lock swap in server.py prevents race during port re-discovery. No socket thundering herd (max 3 retries at 30s+ intervals per policy).

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
- `"reconnecting"` — all other states (attempting reconnect)

Used by `list_connections` to replace the binary connected/disconnected boolean.

### Port Discovery & TCP Probe (server_filtering.py) — v0.23.0, v0.36.0

Port discovery reads `~/.unity-mcp/ports/{pid}.port` files. **v0.23.0:** Adds `_tcp_probe(port, timeout=0.2)` — quick TCP handshake to verify port actually listens before returning. Filters out stale discovery files (port written but server not yet bound, or server crashed leaving orphan file). Candidates prioritized: env UNITY_MCP_PORT → CWD project path match → newest mtime → default 9500.

**v0.36.0:** `_is_pid_alive(pid)` cross-platform check (Windows: OpenProcess/CloseHandle, Unix: os.kill(pid,0)) replaces naive kill check. C# MCPServer writes `{pid}.port` discovery files.

### CompileStateProbe (compile_state.py)

Simplified detector for Unity C# compile/domain-reload:
- **State file**: reads `~/.unity-mcp/state/port-{port}.state` via `unity_state.py` (ready/compiling/reloading/restarting)
- `is_process_dead()` — cross-checks PID from port file
- `has_strong_busy_signal()` — state file (authoritative) then lock file fallback
- `_lock_file_exists()` — checks Unity's BeeDriver Lock file

### Lockfile (lockfile.py) — v0.23.0 Zombie Detection

Exclusive lock per port at `~/.unity-mcp/server-{port}.lock`:
- Uses `fcntl.flock` (POSIX exclusive)
- **Zombie Process Detection (v0.23.0):** New `_is_zombie(pid)` check via `/proc/{pid}/stat` (Linux) or `ps -p` status (macOS/Windows). Stale processes with zombie state are no longer treated as "live" — server startup proceeds without waiting for process cleanup. Fixes `-32000 (server error)` when a previous server process became a zombie.
- Auto-kills stale `unity_mcp` process (SIGTERM + poll)
- Prevents multiple MCP servers on same Unity instance

### Crash Logging (crash_log.py)

Append-only JSONL crash log for unhandled exceptions:
- `log_crash(exc, *, log_dir=None)`: module-level function that writes `{"ev":"crash", "exc":"Type", "msg":"...", "tb":"...", "t":timestamp}` to `crash.jsonl` (defaults to `~/.unity-mcp/crash.jsonl`)
- Auto-creates parent dir, silent on I/O failures
- Integrated into `main()`: outer try/except catches `BaseException` → calls `log_crash()` → re-raises (preserves clean shutdown for `KeyboardInterrupt`, `SystemExit`, EPIPE)
- **CrashLogger class**: JSONL append-only logger with rotation (500 entries max, 15MB size limit) — logs disconnect, reconnect events (older feature). Separate from module-level `log_crash()` used for unhandled server exceptions.

### Parent PID Monitoring (bridge_heartbeat.py)

**P3 (Cycle 17+) Fix — Double-Check Parent Death**
- PPID mismatch now requires 2 consecutive checks (not immediate kill)
- Prevents race-condition kills during rapid fork() activity (e.g., subprocess spawn)
- Counter resets to 0 if PPID matches on next check

**P5 (Cycle 18+) Fix — Graceful Heartbeat Stop on Parent Death**
- When parent dies (2 consecutive PPID mismatches), calls `self.stop_heartbeat()` instead of `raise SystemExit(0)`
- **Why:** `SystemExit` is `BaseException` — escapes `except Exception` safety net in `_heartbeat_loop`, kills anyio task group, closes stdio → -32000 errors on in-flight MCP calls
- Process now dies naturally from `BrokenPipeError` on next stdio write, leaving in-flight MCP operations intact

### C# Server (MCPServer.cs) — v0.23.0 SO_REUSEPORT Recovery

- **Main TCP listener** on port 9500 (configurable via `UNITY_MCP_PORT` env var)
- **Chat TCP listener** on port 9501 (or `main_port + 1`; configurable via `UNITY_MCP_CHAT_PORT` env var) — separate connection for in-Unity chat
- **Reload TCP listener** on port 9600 (independent compile-unit `com.unity-mcp.reload/`) — handles rapid recompilation without domain-reload blocking
- **State file** written to `~/.unity-mcp/state/port-{port}.state` with format: `state\ntimestamp\npid\nepoch` (e.g., "ready", "compiling", "reloading", "compile_failed")
- Max message size: 10MB
- SO_KEEPALIVE with platform-specific tuning (idle=60s, interval=10s, count=3; relaxed from 10s/5s to survive macOS App Nap timer coalescing)
- **SO_REUSEPORT (v0.23.0, macOS/Linux only):** Enables port reuse for rapid reconnect after server crash or process termination. Windows doesn't require it (already has soft TIME_WAIT). Prevents "address already in use" during recovery without waiting for kernel TIME_WAIT timer.
- Single client mode: new connection disconnects previous
- Client generation tracking: prevents stale handlers from clearing shared state
- Lifecycle hardening: `IsRunning` property guarded with try/catch for ObjectDisposedException; `Stop()` wraps listener teardown with try/catch; `OnBeforeReload()` wrapped with try/catch
- Socket shutdown: `Shutdown(Both)` before `Stop()` in OnBeforeReload and Stop (TCP_NODELAY + shutdown both directions → faster port release)

**Bind retry:**
- Up to 4 attempts (3 on same port, 1 fallback to free port)
- Linear backoff: 400ms × (attempt + 1)
- Re-registration of watchdog + heartbeat callback on success

**KillPhantoms (v0.56.0):**
- Cleans up stale TCP connections from dead processes at startup
- Scans port file lockfiles (server-{port}*.lock) for dead PIDs
- Forcibly closes zombie TcpClient entries that fail `IsSocketAlive()` check
- Atomic under ClientSlot._lock (per-connection ring buffer logic)
- Prevents hung-connection accumulation across rapid reconnects

**Watchdog (Cycle 16+):**
- Separate `EditorApplication.update` callback (WatchdogTick)
- Monitors server liveness, restarts if dead within 5 seconds
- Properly unregistered in Stop(), OnQuit(), OnBeforeReload()
- Re-registered in StartAsync() after bind succeeds

**Port discovery:**
- Writes `~/.unity-mcp/ports/{pid}.port` (port, project path, project name)
- Python auto-discovers port from these files

**State file:**
- Writes `~/.unity-mcp/state/port-{port}.state`
- States: `ready`, `compiling`, `reloading`, `restarting` (new in Cycle 16)
- "restarting": written when compilationFinished but server not yet running; indicates startup in progress (TCP client should wait)

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
- `ping`, `get_version`, `status`, `get_enabled_tools`

**Per-command timeouts (v0.57.0 — hard deadline separation):**
| Command | Timeout |
|---------|---------|
| `run_tests`, `run_playtest` | 130s (initial send; v0.32.0: short 8s fire-and-forget) |
| `batch` | 65s (atomic timeout: all-or-nothing with Undo rollback) |
| `wait_until`, `move_to`, `test_step` | 30s |
| Default | 25s |
| **Hard deadline (v0.57.0)** | **450s** (separate from per-command timeout; latches even while domain-reload busy; fires only if reconnect loop exhausted) |

**Heartbeat vs Command Deadline (v0.57.0):**
- **Heartbeat timeout:** 15s ping interval (keepalive only; cannot kill long-running commands)
- **Command deadline:** per-command timeout + hard deadline (450s) applies to send() → re-tries
- **Impact:** `run_playtest` (130s), `run_tests` (130s), `wait_until` (30s) no longer get killed by heartbeat idle timeout; heartbeat cannot block command I/O

**Batch Atomic Timeout Rollback (v0.57.0 — BatchHelper.cs):**
- `batch(atomic=true, timeoutMs=25000)` — all-or-nothing semantics with automatic Undo
- If ANY sub-command times out (elapsed > timeoutMs), entire batch atomically rolls back
- Opens named UndoGroup before first sub-command, reverts all ops on timeout/error
- Summary includes `ATOMIC_ROLLBACK: reverted ops 0..N` when rollback occurs
- Non-atomic batch (default) continues on errors, skips remaining ops on first failure
- Prevents partial state corruption when timeout interrupts mid-batch

**run_tests Fire-and-Forget (v0.32.0)**
- `run_tests()` returns immediately (8s send timeout) with message `"tests-started|{mode}|poll get_test_results every 5s for up to 2min"`
- Does NOT poll internally — caller must poll `get_test_results()` externally
- On `DomainReloadError`: returns immediately (no wait)
- **Why:** avoids TCP blocking when domain reload clears socket before port 9700 restored

**P1 (Cycle 17+) Fix — Compile Guard Allowlist for Test Results**
- `get_test_results` added to `IsAllowedDuringCompile` allowlist in CommandRouter.cs
- Reads SessionState only (no main thread dispatch) — safe during compile + domain reload
- Fixes blocking of test result polling during domain reload, causing -32000 errors

**P0 (Cycle 17+) Fix — Domain Reload Sticky Flag**
- `_domain_reload_in_progress` flag now auto-expires after 90s (v0.42.1: increased from 30s to 90s for 9-assembly window), cleared on successful reconnect
- Prevents permanent "busy" state if domain reload doesn't complete (e.g., compile error)

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
- Tests: `server/tests/test_bridge.py` (37 base + new tests v0.36.0), `server/tests/test_bridge_edge_cases.py` (6 new P0+P2 tests), `server/tests/test_bridge_reload_state.py` (8 new DomainReloadTracker tests, v0.36.0), `server/tests/test_bridge_should_retry.py` (8 new should_retry() tests, v0.36.0), `server/tests/test_bridge_reload_gate.py` (reload gate asyncio.Event tests), `server/tests/test_bridge_role.py` (client role in ping tests), `server/tests/test_heartbeat.py` (4 new P3+P5 tests: double-check + graceful stop), `server/tests/test_connection_slot.py` (8), `server/tests/test_lockfile.py` (17), `server/tests/test_crash_log.py` (10), `server/tests/test_server.py` (4 new main() crash tests), `server/tests/test_parent_death.py` (updated for P3+P5)

## Reconnection Strategy

**Problem:** Unity domain reload closes socket; compile/reloading blocks commands; stale state file blocks reconnect; state loss on rapid reconnect.

**Solution:** Multi-layered resilience (Cycles 14–16):

1. **Exclusive lockfile** (fcntl LOCK_EX): single MCP server per port, kill-old semantics prevent multiple processes interfering
2. **`_raw_ping()` heartbeat** (15s when connected; 5s/2s when disconnected based on probe): sole reconnect mechanism, validates ID match to prevent response misroute
3. **Reconnect cooldown** (`MIN_RECONNECT_INTERVAL=2.0s`): `_reconnect_cooldown_ok()` gate prevents thundering-herd storms
4. **Probe for timing, not gating**: compile-state heuristic used only to compute delay (5s vs 2s), NOT to block reconnect
5. **DomainReloadError immediate close**: going_away event triggers close immediately (no wait for 3 failures), fast reconnect path
6. **`_reload_gate` (asyncio.Event)**: domain-reload retries wait on gate instead of sleeping full backoff; gate is set on successful reconnect — wakes waiting senders immediately
7. **`_ping_stall_failures` threshold (v0.78.8)**: only `asyncio.TimeoutError` increments stall counter (Unity alive, App Nap/heavy compile); other Exceptions close immediately (dead TCP). Tolerates up to 6 stall windows before force-closing; prevents premature disconnect on App Nap AND prevents zombie bridges on dead TCP
8. **State file management**: MCPServer writes "ready" on `compilationFinished` handler (not just startup); prevents stale "compiling" blocking reconnect
9. **SO_KEEPALIVE**: OS-level dead peer detection (~90s: idle=60s + 3 probes at 10s intervals)
10. **Reconnect callbacks**: invalidate tool cache, re-probe capabilities
11. **CrashLogger**: JSONL append-only log at `~/.unity-mcp/crash.jsonl` (500 entries max, 15MB rotation) — logs disconnect, reconnect, exhausted events

```
send(cmd, args, timeout=30.0)
  → write frame + await response
  → on retry hint from Unity: sleep retry_ms, re-send (up to MAX_RETRIES=3)
  → idle errors get 1 grace attempt; busy (domain reload) gets full retries with exp backoff
  
_heartbeat_loop():
  when connected:
    → await 15s
    → skip if lock held (RPC in progress)
    → _raw_ping(timeout=20s) (direct socket, ID-match verify)
    → ping fails + is_process_dead() → force-close immediately
    → ping fails + process alive → _ping_stall_failures++ (App Nap / heavy compile)
      → after 6 stall windows (~6 min) → force-close
    → ping succeeds → _ping_stall_failures reset to 0
  when disconnected (domain reload path):
    → _reload_gate.clear()
    → await _reload_gate (wakes immediately on reconnect, no full backoff sleep)
  when disconnected (normal path):
    → await 5s (probe busy) or 2s (not busy)
    → if _reconnect_cooldown_ok() → reconnect() → _reload_gate.set()
  
On event frame {"ev":"going_away"}:
  → raise DomainReloadError → close immediately (no delay)
  
On asyncio.TimeoutError (Unity process alive):
  → _ping_failures++ → after 3: _ping_stall_failures++ (App Nap/heavy compile)
  → after 6 stall windows (~6 min): force-close
  → if is_process_dead(): close immediately
On Exception (ConnectionReset / OSError / dead TCP):
  → close immediately → heartbeat picks up disconnected state → reconnect on next poll
```

## TDD Scenarios (for Developer)

### Python Client (37 tests in test_bridge.py + 16 new tests in v0.36.0)

**Protocol / framing:**
- `test_send_encodes_header_as_big_endian_uint32`, `test_send_encodes_payload_as_utf8_json`
- `test_send_includes_incremental_id`, `test_read_response_decodes_json`
- `test_message_too_large_raises_error`, `test_send_concurrent_unique_ids`
- `test_close_cleans_up_writer`

**Port config:**
- `test_bridge_default_port`, `test_bridge_custom_port`, `test_bridge_env_port`
- `test_bridge_explicit_port_overrides_env`, `test_bridge_invalid_env_port_falls_back_to_default`

**Retry / error handling:**
- `test_send_fails_fast_on_connection_error`, `test_send_raises_on_connection_error`
- `test_bridge_auto_retry_on_retry_hint`, `test_bridge_no_retry_on_normal_error`
- `test_send_raises_timeout_error_after_max_retries`, `test_bridge_retry_respects_max_retries`
- `test_idle_retry_gets_one_grace_attempt`
- `test_domain_reload_pins_busy_for_all_retries` — DomainReloadError sets `domain_reload_in_progress=True` flag which persists across retries, preventing `_probe_busy()` from returning False early when Editor.log clears "compiling" status before TCP port 9700 is restored
- `test_non_domain_reload_still_re_evaluates_probe` — Non-DomainReloadError exceptions still allow `_probe_busy()` re-evaluation per attempt (existing behavior)

**P0 + P2 (Cycle 17+) — Edge case fixes:**
- `test_domain_reload_flag_auto_expires` — flag clears after DOMAIN_RELOAD_EXPIRY_S=30s even if domain reload never completes
- `test_startup_grace_latch_recovery` — send() retries reconnect instead of immediate failure when grace period expired
- `test_reconnect_clears_domain_reload_flags` — successful reconnect clears both `_domain_reload_in_progress` and `_domain_reload_since`

**Heartbeat:**
- `test_heartbeat_default_interval_is_15`, `test_heartbeat_detects_zombie_connection`
- `test_heartbeat_reconnects_when_disconnected`, `test_heartbeat_reconnects_when_busy`
- `test_heartbeat_stops_on_close`, `test_heartbeat_immediate_close_when_pid_dead`
- `test_heartbeat_immediate_close_on_domain_reload_error`
- `test_heartbeat_respects_reconnect_cooldown`
- `test_heartbeat_survives_tick_exception`

**P3 (Cycle 17+) — Parent Death Double-Check:**
- `test_ppid_mismatch_requires_two_checks` — single PPID change doesn't exit
- `test_ppid_mismatch_counter_resets_on_match` — counter zeroes if PPID matches next check
- `test_ppid_mismatch_exits_on_second_mismatch` — raises SystemExit(0) on consecutive mismatches
- `test_ppid_mismatch_allows_cleanup` — SystemExit permits finally blocks and atexit handlers

**Raw ping:**
- `test_raw_ping_bypasses_send_retry`, `test_raw_ping_raises_on_disconnected`

**Reconnect cooldown:**
- `test_reconnect_cooldown_default_2s`, `test_reconnect_cooldown_blocks_rapid_reconnect`
- `test_reconnect_cooldown_allows_after_interval`
- `test_reconnect_callback_debounce_skips_rapid_calls`, `test_reconnect_callback_debounce_allows_after_cooldown`

**Failure description:**
- `test_describe_failure_reports_crash_when_pid_dead`

### v0.36.0 Refactoring (16 new tests across 2 files)

**test_bridge_reload_state.py (8 tests for DomainReloadTracker):**
- `test_tracker_not_active_initially` — new instance has is_active()=False
- `test_tracker_mark_activates` — mark() sets _active=True
- `test_tracker_elapsed_increases` — elapsed() returns monotonic delta from mark time
- `test_tracker_is_active_true_while_within_expiry` — is_active()=True while < 90s (v0.42.1: increased from 30s)
- `test_tracker_is_active_false_after_expiry` — is_active()=False after 90s+ elapsed (v0.42.1: increased from 30s)
- `test_tracker_clear_deactivates` — clear() sets _active=False, _since=None
- `test_tracker_clear_without_mark_safe` — clear() on unmarked tracker is safe
- `test_tracker_elapsed_zero_when_unmarked` — elapsed()=0.0 when _since is None

**test_bridge_reload_gate.py (reload gate asyncio.Event):**
- Gate cleared on disconnect during domain reload, set on successful reconnect
- Retry wakes immediately when gate is set instead of sleeping full backoff window

**test_bridge_role.py (client role in ping):**
- `UNITY_MCP_CLIENT` env var present → sent as `role` field in ping payload
- `UNITY_MCP_CLIENT` empty or absent → `role` defaults to `"mcp"`

**test_bridge_should_retry.py (8 tests for should_retry() decision logic):**
- `test_should_retry_max_retries_gate` — attempt >= MAX_RETRIES returns (False, 0.0, "max_retries")
- `test_should_retry_deadline_gate` — time.monotonic() >= deadline returns (False, 0.0, "deadline")
- `test_should_retry_domain_reload_error_marks_reload` — DomainReloadError sets state→DOMAIN_RELOADING (90s window as of v0.42.1)
- `test_should_retry_domain_reload_gets_backoff` — DomainReloadError attempt<MAX_RETRIES returns (True, 2^attempt≤8, "domain_reload") within 90s window (v0.42.1)
- `test_should_retry_reload_active_retries` — reload.is_active()=True gets backoff within 90s window (v0.42.1), attempt < MAX_RETRIES
- `test_should_retry_probe_busy_retries` — probe_busy()=True gets backoff
- `test_should_retry_transient_attempt_0` — attempt < 1 with no busy returns (True, 1.0, "transient")
- `test_should_retry_grace_expired_attempt_1_plus` — attempt ≥ 1 with no busy returns (False, 0.0, "grace_expired")

### C# Server

1. **Test_AcceptsConnection**: start → client connects successfully
2. **Test_ParsesMessage**: receive bytes → command extracted
3. **Test_DispatchesToMainThread**: command → processed on main thread
4. **Test_GoingAwayOnDomainReload**: assembly reload → event frame sent
5. **Test_StateFileUpdated**: compile/reload → state file written
6. **Test_FastPathBypassesMainThread**: ping → response without main thread dispatch

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
- [ ] Reconnect cooldown (2s min) prevents thrashing
- [ ] Lifecycle guards: IsRunning, Stop(), OnBeforeReload() all try/catch protected

## Related

- Skill: `.claude/skills/tcp-protocol.md`
- MCP Server: `AI/mcp-server.md`
- Architecture: `AI/architecture.md`
