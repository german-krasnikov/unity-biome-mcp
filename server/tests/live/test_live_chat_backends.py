"""Live tests for all CLI backends via relay TCP.

Run on demand: UNITY_MCP_RUN_LIVE_CLI=1 UNITY_MCP_PORT=<port>
pytest tests/live/test_live_chat_backends.py -m live_cli -v

Each test makes ~1 real API call (~$0.001-0.01).
Requires the respective CLI tool installed and a valid API key.
"""
from __future__ import annotations

import shutil

import pytest

from .relay_test_helpers import (
    MCP_PORT, PROMPT_TEXT,
    _relay_cmd, _parse_events, _poll_until_done, _turn_line, relay_port,
)

pytestmark = [pytest.mark.live, pytest.mark.live_cli, pytest.mark.live_chat]


def _skip_if_missing(binary: str) -> None:
    if not shutil.which(binary):
        pytest.skip(f"{binary!r} not in PATH")


async def test_claude_ask_responds_hello(relay_port: int) -> None:
    """Claude backend: start → send prompt via stdin → get t| + d| events."""
    _skip_if_missing("claude")

    resp = await _relay_cmd(relay_port, "start", {
        "backend": "claude", "mode": "ask", "mcp_port": MCP_PORT, "prompt": "",
    })
    assert resp["ok"], f"start failed: {resp.get('err')}"

    send_resp = await _relay_cmd(relay_port, "send", {"line": _turn_line(PROMPT_TEXT)})
    assert send_resp["ok"], f"send failed: {send_resp.get('err')}"

    await _relay_cmd(relay_port, "close_stdin", {})

    events = await _poll_until_done(relay_port)
    texts = [e for e in events if e.startswith("t|")]
    dones = [e for e in events if e.startswith("d|")]

    assert texts, f"No text events received: {events}"
    assert dones, f"No done event received: {events}"
    full = "".join(e[2:] for e in texts).lower()
    assert "hello" in full, f"Expected 'hello' in output: {full!r}"


async def test_kimi_ask_responds_hello(relay_port: int) -> None:
    """Kimi backend: deferred start → send triggers spawn with -p <prompt> → get t| + d| events."""
    _skip_if_missing("kimi")

    # reads_stdin=False: start with empty prompt → deferred
    resp = await _relay_cmd(relay_port, "start", {
        "backend": "kimi", "mode": "ask", "mcp_port": MCP_PORT, "prompt": "",
    })
    assert resp["ok"], f"start failed: {resp.get('err')}"

    # send extracts text and respawns kimi with -p <prompt>
    send_resp = await _relay_cmd(relay_port, "send", {"line": _turn_line(PROMPT_TEXT)})
    assert send_resp["ok"], f"send failed: {send_resp.get('err')}"

    events = await _poll_until_done(relay_port)
    texts = [e for e in events if e.startswith("t|")]
    dones = [e for e in events if e.startswith("d|")]

    assert texts, f"No text events received: {events}"
    assert dones, f"No done event received: {events}"
    full = "".join(e[2:] for e in texts).lower()
    assert "hello" in full, f"Expected 'hello' in output: {full!r}"


async def test_agy_ask_responds_hello(relay_port: int) -> None:
    """Agy (Gemini) backend: start with prompt → get t| + d|/e| events."""
    _skip_if_missing("agy")

    resp = await _relay_cmd(relay_port, "start", {
        "backend": "agy", "mode": "ask", "mcp_port": MCP_PORT, "prompt": PROMPT_TEXT,
    })
    assert resp["ok"], f"start failed: {resp.get('err')}"

    events = await _poll_until_done(relay_port)
    texts = [e for e in events if e.startswith("t|")]
    terminal = [e for e in events if e.startswith("d|") or e.startswith("e|")]

    assert texts or terminal, f"No events received at all: {events}"
    if texts:
        full = "".join(e[2:] for e in texts).lower()
        assert "hello" in full, f"Expected 'hello' in output: {full!r}"


async def test_opencode_ask_responds_hello(relay_port: int) -> None:
    """OpenCode backend: start with prompt → parse NDJSON → get t| + d| events."""
    _skip_if_missing("opencode")

    resp = await _relay_cmd(relay_port, "start", {
        "backend": "opencode", "mode": "ask", "mcp_port": MCP_PORT, "prompt": PROMPT_TEXT,
    })
    assert resp["ok"], f"start failed: {resp.get('err')}"

    events = await _poll_until_done(relay_port, timeout=120.0)
    texts = [e for e in events if e.startswith("t|")]
    dones = [e for e in events if e.startswith("d|")]

    assert texts, f"No text events received: {events}"
    assert dones, f"No done event received: {events}"
    full = "".join(e[2:] for e in texts).lower()
    assert "hello" in full, f"Expected 'hello' in output: {full!r}"


async def test_codex_ask_responds_hello(relay_port: int) -> None:
    """Codex backend: start (deferred) → send prompt → get t| + d| events."""
    _skip_if_missing("codex")

    # Codex reads_stdin=False → deferred spawn until send provides prompt
    resp = await _relay_cmd(relay_port, "start", {
        "backend": "codex", "mode": "ask", "mcp_port": MCP_PORT, "prompt": "",
    })
    assert resp["ok"], f"start failed: {resp.get('err')}"

    # send triggers respawn with real prompt
    send_resp = await _relay_cmd(relay_port, "send", {"line": _turn_line(PROMPT_TEXT)})
    assert send_resp["ok"], f"send failed: {send_resp.get('err')}"

    events = await _poll_until_done(relay_port)
    texts = [e for e in events if e.startswith("t|")]
    dones = [e for e in events if e.startswith("d|")]

    assert texts, f"No text events received: {events}"
    assert dones, f"No done event received: {events}"
    full = "".join(e[2:] for e in texts).lower()
    assert "hello" in full, f"Expected 'hello' in output: {full!r}"
