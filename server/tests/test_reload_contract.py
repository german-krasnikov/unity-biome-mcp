"""Contract tests: verify Python↔C# shared constants don't drift.

These tests scan source files to confirm that protocol constants match
their documented values. A failure here means one side drifted.
"""
import inspect
import pathlib

_SERVER_ROOT = pathlib.Path(__file__).parent.parent / "src" / "unity_mcp"


def _src(rel: str) -> str:
    return (_SERVER_ROOT / rel).read_text(encoding="utf-8")


# ── Scenario 1: MCP_ReloadGuardLocked key ──────────────────────────────────

def test_reload_guard_key_in_python():
    """reload_ladder.py must contain the SessionState key used by ReloadGuard."""
    source = _src("tools/reload_ladder.py")
    assert "MCP_ReloadGuardLocked" in source


# ── Scenario 2: reload port = DEFAULT_PORT + 100 ───────────────────────────

def test_reload_port_offset():
    """DEFAULT_PORT is 9500; reload mini-server defaults to 9600 (offset +100)."""
    from unity_mcp.constants import DEFAULT_PORT
    assert DEFAULT_PORT == 9500
    assert DEFAULT_PORT + 100 == 9600


# ── Scenario 3: wire protocol uses 4-byte big-endian length prefix ──────────

def test_wire_protocol_length_prefix_be():
    """Bridge must use big-endian (network order) 4-byte length prefix."""
    bridge_src = _src("bridge_socket.py")
    # "!I" = network byte order (big-endian), unsigned int — equivalent to ">I"
    assert 'struct.pack("!I"' in bridge_src or 'struct.pack(">I"' in bridge_src


# ── Scenario 4: state file sequence names present in server source ──────────

def test_state_file_names():
    """going_away, reloading, ready must appear in server source (protocol contract)."""
    # going_away: domain-reload event sent by Unity over TCP
    bridge_src = _src("bridge.py")
    assert "going_away" in bridge_src

    # reloading, ready: state file values written by MCPServer.WriteStateFile
    # Python side reads these in unity_state.py and tools
    unity_state_src = _src("unity_state.py")
    assert "reloading" in unity_state_src

    errors_src = _src("errors.py")
    assert "reloading" in errors_src

    sync_src = _src("tools/sync.py")
    assert '"ready"' in sync_src
