# Connection Tools & Diagnostics

TCP connection management, port discovery, health checks.

## Tools

### list_connections()

**Read-only.** Show current TCP slot status.

```python
await list_connections()
# → "port 9500 | tcp:connected | stdio:alive"
```

**Returns:** `"port {N} | {tcp-status} | {stdio-status}"`. The two status
fields distinguish the Unity socket from the MCP client's stdio transport:

| Status | Meaning |
|---|---|
| `tcp:none` | The session slot has no bridge instance |
| `tcp:connected` | Unity TCP writer is live |
| `tcp:reconnecting` | No live writer; the bridge may retry |
| `tcp:failed` | Startup/reconnect grace expired |
| `tcp:dormant` / `tcp:waking` | Idle suspension or its reconnect transition |
| `stdio:alive` / `stdio:dead` | MCP client transport health |

---

### reconnect_unity

```python
await reconnect_unity(port=0)
```

**Write-idempotent.** Reconnect to Unity via TCP.

```python
# Auto-discover port from ~/.unity-biome-mcp/ports/*.port
await reconnect_unity()

# Manual port
await reconnect_unity(port=9500)
```

**Port discovery waterfall:**
1. Explicit `port` param (if > 0)
2. `UNITY_MCP_PORT` env var
3. Live `.port` file whose project path best matches `UNITY_MCP_PROJECT_DIR`, `CLAUDE_PROJECT_DIR`, or the current working directory
4. Newest live `.port` file
5. Default: 9500

**Returns:** Connection status message or error.

**Post-connect behavior (on successful connection):**
- `gating.reset()` — clears session-enabled tools (only on manual reconnect; passive TCP reconnect does not wipe gating)
- `_refresh_tools_cache(bridge)` — rebuilds tool cache from current state
- `_push_catalog(bridge)` — synchronizes tool catalog with client
- `ctx.session.send_tool_list_changed()` — sends MCP notification, forcing client re-query of `ListTools` (no debounce; user explicitly asked for reconnect)

---

## Internal Protocol Commands

`get_aliases` and `set_client_label` are Unity TCP protocol commands, not public MCP tools. Agents must not call them directly.

### get_aliases

Returns bare `name=path|component|field` lines to the Python middleware, or `"no aliases"` when no `PlaytestConfig` asset exists or its alias list is empty.

The middleware uses it to populate `_alias_cache`; normal tool calls then resolve `$name` values through that cache. It is allowed during compile.

---

### set_client_label

Internal connection-identification command:
- Sets `MCPServer._mainSlot.Label = label` on the C# side.
- Always allowed: registered as `alwaysAllowed` + `allowedDuringCompile`.
- Label appears in disconnect logs: `slot.Label ?? label`.
- Cleared to `null` on the first message of each new connection (slot reset).

**Called automatically** by the MCP `InitializedNotification` hook in `server_filtering.py:install_initialized_hook`. After the MCP handshake, the hook reads `session.client_params.clientInfo.name` and sends `set_client_label`. Skips "Claude Code" (default). Failures logged at DEBUG.

**RoleToLabel mapping (C# `CommandRouter`):**

| Role string | Label |
|-------------|-------|
| `mcp` | Claude Code session |
| `chat-relay` | Chat relay |
| `codex` | Codex session |
| `cursor` | Cursor session |
| `windsurf` | Windsurf session |
| `claude-desktop` | Claude Desktop session |

Unrecognized non-empty role strings are used as their own label.

---

### doctor(fix: bool = False)

**Read-only / Write-idempotent.** Health diagnostics with optional auto-fix.

```python
# Diagnosis only
result = await doctor()

# Remove confirmed stale discovery/lock files while running the health checks
result = await doctor(fix=True)
```

**5 Checks:**

| Check | Tests | Auto-fix? |
|-------|-------|-----------|
| `python_version` | Python ≥ 3.10 | ❌ Manual: Install Python 3.10+ |
| `port_file` | ~/.unity-biome-mcp/ports/*.port exist + PIDs alive | ✅ Remove stale files, report if none live |
| `lockfile` | ~/.unity-biome-mcp/*.lock holds live PID | ✅ Clean stale files |
| `tcp_connection` | A TCP connection to the discovered port can be opened | ❌ Diagnostic only |
| `unity_state` | The direct TCP `diagnose` response reports a healthy editor state | ❌ Diagnostic only |

**Returns:** Formatted report with summary + details.

---

## Port Discovery

The canonical order is documented once under
[`reconnect_unity`](#reconnect_unity). Discovery files contain the
port and project identity recorded by Unity; do not select the first file from
the directory when multiple Editors are open.

---

## Troubleshooting Cheatsheet

### "No connections"

1. **Is Unity running?**
   ```bash
   lsof -i :9500  # check port bound
   ```

2. **Plugin installed?**
   - Open Unity → `🧬MCP → Setup Wizard` and confirm the package is loaded
   - Check the project Package Manager entry for `com.unity-biome-mcp.editor`

3. **Port file stale?**
   ```python
   await doctor(fix=True)  # removes confirmed stale discovery files
   ```

4. **Firewall blocking?**
   ```bash
   # Test socket locally (127.0.0.1 only)
   python3 -c "
   import socket
   s = socket.socket(); s.connect(('127.0.0.1', 9500))
   print('OK'); s.close()
   "
   ```

### "Connected but commands hang"

1. **Classify the connection and reload state:**
   ```python
   await diagnose()
   ```

2. **Cross-check errors if the verdict is unexpected:**
   ```python
   await get_console(level='error')
   ```

3. **Reconnect only when diagnostics identify a connection problem:**
   ```python
   await reconnect_unity()
   ```

### "Stale assembly / tests fail but compile clean"

Use the canonical reload sequence:

```python
await sync_unity()
```

`sync_unity` refreshes assets, waits for compilation/domain reload, and invokes
the recovery ladder when the assembly stays stale. Run tests only after it
returns a clean result. If it returns `STOP:`, `REIMPORT-NEEDED:`, or
`MANUAL-REQUIRED:`, continue with the constraints in
`AI/reload-reference.md`.

### "Repeated reconnect failures"

One common cause is a stale discovery entry after the Editor exits. Confirm the
diagnosis before deleting discovery files.

**Fix:** Confirm the discovery entries are stale, then use:

```python
await doctor(fix=True)
await reconnect_unity()
```

### "Multiple Unity instances, wrong port"

1. **Set explicitly:**
   ```bash
   export UNITY_MCP_PORT=9501
   ```

2. **Or discover by project name:**
   ```bash
   # Lists all active instances with ports
   ls -la ~/.unity-biome-mcp/ports/
   cat ~/.unity-biome-mcp/ports/{PID}.port  # see port
   ```

3. **Then reconnect:**
   ```python
   await reconnect_unity(port=9501)
   ```

---

## Connection Slot Architecture

**One Python ConnectionSlot per MCP session:**
- Maintains TCP socket to one Unity instance
- Auto-reconnect on disconnect (5s backoff, max 60s exponential backoff with ±10% jitter)
- Heartbeat every 15s (via `_raw_ping()`) to detect stale connections; fast-path bypass of retry machinery
- Graceful shutdown: closes socket + cleanup on MCP exit

Each Unity main/chat listener owns a `ClientSlot` with capacity for eight
simultaneous clients. Each entry has a human-readable label set by the internal
identification flow and cleared on reset.

**Blocking behavior:** Unity-bound tool calls wait for a TCP response. Pure
Python discovery and session helpers do not necessarily touch the Unity socket.

**Timeouts:** The bridge provides the shared transport timeout. Tool-specific limits come from `ToolSpec` metadata or the typed wrapper, which adds operation-specific buffers for long-running calls.

---

## Integration with Tools

**Unity-bound tools use the shared connection slot:**
- Tool calls check the shared connection before executing; they do not invoke `list_connections()`
- Disconnected state → bounded retry according to command safety, session
  deadline, and `UNITY_MCP_MAX_RETRIES`
- Exhausted or unsafe retry → surface the connection error; use
  `reconnect_unity()` only after diagnosing the target port

**Force-reconnect in scripts:**

```python
await reconnect_unity(port=0)  # auto-discover
result = await get_hierarchy()  # now connected
```

---

## Client Identification

Two paths for labeling the active connection slot:

| Path | Mechanism | When used |
|------|-----------|-----------|
| MCP hook | `install_initialized_hook` reads `clientInfo.name`, calls `set_client_label` | Cursor, Windsurf, Claude Desktop (any non-"Claude Code" MCP client) |
| Env var | `UNITY_MCP_CLIENT` read in `bridge.py:_reconnect()`, sent as ping `role` field | Codex and other non-MCP tools that launch bridge directly |

**`UNITY_MCP_CLIENT` flow:**
```bash
export UNITY_MCP_CLIENT=codex  # set before bridge launch
```
Bridge reads it in `_reconnect()` and includes `"role": "codex"` in the ping payload. C# `RoleToLabel` maps it to `"Codex session"` and stores on the slot.

**See also:** `CONTRIBUTING.md` (server and test commands) and
`AI/reload-reference.md` (domain reload strategy).
