# Connection Tools & Diagnostics

TCP connection management, port discovery, health checks.

## Tools

### list_connections()

**Read-only.** Show current TCP slot status.

```python
await list_connections()
# → "port 9500 (connected)"
# → "port 9500 (reconnecting)"
# → "port 9500 (domain-reloading)"
# → "port 9500 (disconnected)"
```

**Returns:** Single-line `"port {N} ({status})"`. Status comes from `ConnectionSlot.status → UnityBridge.status`:

| Status | Meaning |
|--------|---------|
| `connected` | Writer open, not closing |
| `reconnecting` | Heartbeat loop attempting reconnect |
| `domain-reloading` | Unity domain reload in progress (`BridgeState.DOMAIN_RELOADING`) |
| `disconnected` | Startup grace expired (`BridgeState.FAILED`) |

---

### reconnect_unity(port: int = 0)

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
| `codex` | Codex session |
| `cursor` | Cursor session |
| `windsurf` | Windsurf session |
| `claude-desktop` | Claude Desktop session |

---

### doctor(fix: bool = False)

**Read-only / Write-idempotent.** Health diagnostics with optional auto-fix.

```python
# Diagnosis only
result = await doctor()

# Remove confirmed stale discovery files and retry connection
result = await doctor(fix=True)
```

**5 Checks:**

| Check | Tests | Auto-fix? |
|-------|-------|-----------|
| `python_version` | Python ≥ 3.10 | ❌ Manual: Install Python 3.10+ |
| `port_file` | ~/.unity-biome-mcp/ports/*.port exist + PIDs alive | ✅ Remove stale files, signal if none live |
| `lockfile` | ~/.unity-biome-mcp/*.lock holds live PID | ✅ Clean stale files |
| `tcp_connection` | 127.0.0.1:port reachable + responds to heartbeat | ⚠️ Reconnect attempt only |
| `unity_state` | Editor.log accessible + recent activity | ⚠️ Diagnose compile/domain-reload wedge |

**Returns:** Formatted report with summary + details.

---

## Port Discovery

The canonical order is documented once under
[`reconnect_unity`](#reconnect_unityport-int--0). Discovery files contain the
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
   - Open Unity → `MCP → Setup Wizard` → confirm plugin loaded
   - Check `Assets/Plugins/UnityMCP/` exists in project

3. **Port file stale?**
   ```bash
   doctor(fix=True)  # auto-clean
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
   await get_console(severity='error')
   ```

3. **Reconnect only when diagnostics identify a connection problem:**
   ```python
   await reconnect_unity()
   ```

### "Stale assembly / tests fail but compile clean"

Use the canonical reload sequence:

```python
await force_refresh()
# Wait 15 seconds, then:
await diagnose()
```

Run tests only after the verdict is clean. If the assembly MVID is unchanged,
continue with the recovery constraints in `AI/reload-reference.md`.

### "Reconnect spam (9 failed attempts)"

**Root cause:** PID file alive but editor crashed → socket orphaned.

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

The Unity listener independently accepts up to eight client slots. Each slot has a human-readable label set by the internal identification flow and cleared on reset.

**Blocking behavior:** All MCP tool calls block on socket I/O (TCP call-response).

**Timeouts:** The bridge provides the shared transport timeout. Tool-specific limits come from `ToolSpec` metadata or the typed wrapper, which adds operation-specific buffers for long-running calls.

---

## Integration with Tools

**Every tool uses the shared connection slot:**
- Tool calls check the shared connection before executing; they do not invoke `list_connections()`
- Disconnected state → auto-reconnect attempt
- 3 failed attempts → raise ToolError (user must `reconnect_unity()` explicitly)

**Force-reconnect in scripts:**

```python
await reconnect_unity(port=0)  # auto-discover
result = await get_hierarchy()  # now connected
```

---

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

**See also:** CLAUDE.md § "Run MCP server", `AI/reload-reference.md` (domain reload strategy).
