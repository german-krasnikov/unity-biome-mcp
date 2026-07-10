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
# Auto-discover port from ~/.unity-mcp/ports/*.port
await reconnect_unity()

# Manual port
await reconnect_unity(port=9500)
```

**Port discovery waterfall:**
1. Explicit `port` param (if > 0)
2. `UNITY_MCP_PORT` env var
3. First live `.port` file in `~/.unity-mcp/ports/`
4. Default: 9500

**Returns:** Connection status message or error.

**Post-connect behavior (on successful connection):**
- `gating.reset()` — clears session-enabled tools (only on manual reconnect; passive TCP reconnect does not wipe gating)
- `_refresh_tools_cache(bridge)` — rebuilds tool cache from current state
- `_push_catalog(bridge)` — synchronizes tool catalog with client
- `ctx.session.send_tool_list_changed()` — sends MCP notification, forcing client re-query of `ListTools` (no debounce; user explicitly asked for reconnect)

---

### get_aliases()

**Read-only.** Return all alias definitions from the `PlaytestConfig` ScriptableObject.

```python
# Returns one "name=path|comp|field" line per alias, or "no aliases"
await get_aliases()
# → "hp=Player/Health|HealthComponent|m_HP\nspeed=/Player|Rigidbody|m_Velocity"
```

**Returns:** Bare `name=path|component|field` lines (no header/footer), or `"no aliases"` when no `PlaytestConfig` asset exists or alias list is empty.

**Middleware behavior:** Python middleware auto-populates `_alias_cache` from this response. Subsequent tool calls with `$name` arg values auto-resolve against the cache. Also allowed during compile (`allowedDuringCompile=true`).

---

### set_client_label(label: str)

**Write.** Attach a human-readable label to the current connection slot.

```python
await set_client_label(label="Cursor session")
```

**Behavior:**
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

**Returns:** `"ok"` or error string.

---

### doctor(fix: bool = False)

**Read-only / Write-idempotent.** Health diagnostics with optional auto-fix.

```python
# Diagnosis only
result = await doctor()

# Auto-fix stale port files, retry connection
result = await doctor(fix=True)
```

**5 Checks:**

| Check | Tests | Auto-fix? |
|-------|-------|-----------|
| `python_version` | Python ≥ 3.10 | ❌ Manual: Install Python 3.10+ |
| `port_file` | ~/.unity-mcp/ports/*.port exist + PIDs alive | ✅ Remove stale files, signal if none live |
| `lockfile` | ~/.unity-mcp/*.lock holds live PID | ✅ Clean stale files |
| `tcp_connection` | 127.0.0.1:port reachable + responds to heartbeat | ⚠️ Reconnect attempt only |
| `unity_state` | Editor.log accessible + recent activity | ⚠️ Diagnose compile/domain-reload wedge |

**Returns:** Formatted report with summary + details.

---

## Port Discovery Waterfall

**Problem:** Multiple Unity instances running simultaneously.

**Solution:**
1. Read `UNITY_MCP_PORT` environment variable (set by setup wizard)
2. Scan `~/.unity-mcp/ports/{PID}.port` files (one per running instance)
3. Check each file: `{port}\n{timestamp}\n{session_id}`
4. Verify PID alive via `/proc/{PID}` (Linux) or `ps` (macOS) or WMI (Windows)
5. Fall back to default 9500

**Manual discovery:**

```bash
# List all running instance ports
ls -la ~/.unity-mcp/ports/

# Check single instance (macOS)
python3 -c "
import json,pathlib,os
port=int(os.environ.get('UNITY_MCP_PORT','0'))
if not port:
    for p in pathlib.Path.home().glob('.unity-mcp/ports/*.port'):
        try: port=int(p.read_text().split('\n')[0]); break
        except: pass
print(port or 9500)
"
```

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

1. **Check compile errors:**
   ```python
   await get_compile_errors()
   ```

2. **Check domain reload:**
   ```python
   await get_console(severity='error')
   ```

3. **Force reconnect with backoff:**
   ```python
   await reconnect_unity(port=9500)  # retries up to 3x
   ```

### "Stale assembly / tests fail but compile clean"

1. **Unity using cached DLL?**
   - Bump `package.json` version → forces reload
   - Or: Editor → `⌘R` (macOS) or `Ctrl+Shift+R` (Windows)

2. **Run compile check before tests:**
   ```python
   await run_tests(mode="EditMode")  # FAST gate
   ```

### "Reconnect spam (9 failed attempts)"

**Root cause:** PID file alive but editor crashed → socket orphaned.

**Fix:**
```bash
# Manual cleanup
rm ~/.unity-mcp/ports/{PID}.port
rm ~/.unity-mcp/{PID}.lock
# Then:
open -a Unity  # restart editor
```

Or auto:
```python
await doctor(fix=True)  # removes stale files
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
   ls -la ~/.unity-mcp/ports/
   cat ~/.unity-mcp/ports/{PID}.port  # see port
   ```

3. **Then reconnect:**
   ```python
   await reconnect_unity(port=9501)
   ```

---

## Connection Slot Architecture

**Single ConnectionSlot per MCP session:**
- Maintains TCP socket to one Unity instance
- Auto-reconnect on disconnect (5s backoff, max 60s exponential backoff with ±10% jitter)
- Heartbeat every 15s (via `_raw_ping()`) to detect stale connections; fast-path bypass of retry machinery
- Graceful shutdown: closes socket + cleanup on MCP exit
- `MaxClients`: 8 (raised from 4 in v0.79+)
- `volatile string Label`: human-readable client name; set via `set_client_label`, cleared on slot reset, appears in disconnect logs

**Blocking behavior:** All MCP tool calls block on socket I/O (TCP call-response).

**Timeout:** Default 25s per command (configurable via `UNITY_MCP_TIMEOUT`). Per-command overrides exist: `run_tests`/`run_playtest` use 130s; `batch` uses 65s; `wait_until`/`move_to`/`test_step` use 30s. Hard deadline (450s) applies to all send() retries.

---

## Integration with Tools

**Every tool uses `list_connections()` implicitly:**
- CORE tools check connection before executing
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

**See also:** CLAUDE.md § "Run MCP server", `.claude/skills/reload-recovery.md` (domain reload strategy).
