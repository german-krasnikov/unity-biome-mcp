"""Tests for wrap_send dict-response extraction — specifically the file+data path."""
import os
from unittest.mock import AsyncMock, patch
from unity_mcp.middleware_pipeline import wrap_send


# ── Item 2: guards see ORIGINAL cmd, speculation sees REROUTED cmd ────────────

async def test_guards_see_original_cmd_before_reroute(monkeypatch):
    """check_blast_radius must receive the original cmd, NOT the rerouted one.

    Scenario: reroute_cmd renames 'set_property' → 'set_runtime_property' in play mode.
    Guards must still evaluate 'set_property' (the original intent), not the rerouted name.
    """
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send as mw_wrap_send

    mw = Middleware()
    mw.known_paths.add("/P")
    mw.is_playing = True  # triggers reroute set_property → set_runtime_property

    blast_received = []
    original_blast = mw.check_blast_radius

    def capturing_blast(cmd, args=None):
        blast_received.append(cmd)
        return original_blast(cmd, args)

    mw.check_blast_radius = capturing_blast

    async def fake_send(cmd, args, timeout=30.0):
        return "ok"

    wrapped = mw_wrap_send(fake_send, mw)
    await wrapped("set_property", {"path": "/P", "prop": "x", "value": "1"})

    assert blast_received, "check_blast_radius was never called"
    assert blast_received[0] == "set_property", (
        f"Guards must see original 'set_property', got '{blast_received[0]}'"
    )


async def test_speculation_sees_rerouted_cmd(monkeypatch):
    """speculation.record_actual_next must receive the rerouted cmd, not the original.

    Speculation tracks what was ACTUALLY sent to Unity — after rerouting.
    """
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unittest.mock import MagicMock
    from unity_mcp.middleware import Middleware, wrap_send as mw_wrap_send

    mw = Middleware()
    mw.known_paths.add("/P")
    mw.is_playing = True  # triggers reroute set_property → set_runtime_property

    recorded = []
    spec = MagicMock()
    spec.record_actual_next.side_effect = lambda cmd: recorded.append(cmd)
    spec.maybe_prefetch = AsyncMock(side_effect=lambda cmd, args, result: result)
    mw.speculation = spec

    async def fake_send(cmd, args, timeout=30.0):
        return "ok"

    wrapped = mw_wrap_send(fake_send, mw)
    await wrapped("set_property", {"path": "/P", "prop": "x", "value": "1"})

    assert recorded, "record_actual_next was never called"
    assert recorded[0] == "set_runtime_property", (
        f"Speculation must see rerouted 'set_runtime_property', got '{recorded[0]}'"
    )


async def test_wrap_send_file_and_data_combined():
    """wrap_send must return both manifest text AND file path when response has both."""
    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": True, "data": "FRONT:Player(vis)\nLEFT:Player(vis)", "file": "/tmp/mv.png"}

    wrapped = wrap_send(fake_send)
    result = await wrapped("screenshot", {})
    assert "FRONT:Player(vis)" in result
    assert "Data saved to: /tmp/mv.png" in result


async def test_wrap_send_file_only_no_data():
    """wrap_send with only 'file' key (no data) must return just the path string."""
    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": True, "file": "/tmp/mv.png"}

    wrapped = wrap_send(fake_send)
    result = await wrapped("screenshot", {})
    assert result == "Data saved to: /tmp/mv.png"


# --- C1: wrapped()'s own default must not shadow _send_raw's category guard ---


async def test_wrap_send_forwards_zero_not_stale_thirty():
    """wrapped() called with no explicit timeout must forward timeout=0 — the
    sentinel _send_raw's `timeout <= 0` guard resolves via get_timeout(cmd) —
    not a stale hardcoded 30.0 that would shadow the category lookup."""
    received = {}

    async def fake_send(cmd, args, timeout=30.0):
        received["timeout"] = timeout
        return {"ok": True, "data": "ok"}

    wrapped = wrap_send(fake_send)
    await wrapped("ping", {})

    assert received["timeout"] == 0


async def test_wrapped_send_unwraps_raw_dict_result_not_ok():
    """wrapped() must surface the err message (not the raw dict) when ok=False,
    and must classify it as a protocol error (drives dedup_error/recorder)."""
    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": False, "err": "boom"}

    wrapped = wrap_send(fake_send)
    result = await wrapped("get_console", {})
    assert result == "boom"
