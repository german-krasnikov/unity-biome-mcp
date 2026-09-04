"""Wire tests for the optional cassette recorder behind UNITY_MCP_TRACE_FILE.

`UnityBridge.send()` can record every completed request/response pair (both
success and error) as a JSONL cassette line, replayable by
`FakeUnityServer.load_cassette`. Opt-in only: recording must be a strict
no-op unless the env var is set, and the recorded shape must round-trip
through load_cassette without hand-editing.
"""
import json
import logging

import pytest

from tests.wire.helpers.fake_server import FakeUnityServer
from unity_mcp import bridge_cassette
from unity_mcp.bridge import UnityBridge

pytestmark = pytest.mark.wire


@pytest.fixture(autouse=True)
def _reset_cassette_cache():
    """The trace-file path is cached at module scope (read once, not per
    call) — reset before and after every test so xdist workers and test
    order never leak one test's env var resolution into another's."""
    bridge_cassette.reset_for_tests()
    yield
    bridge_cassette.reset_for_tests()


async def test_send_appends_cassette_line_when_trace_file_set(
    wire_server: FakeUnityServer, monkeypatch, tmp_path
):
    trace_path = tmp_path / "trace.jsonl"
    monkeypatch.setenv(bridge_cassette.TRACE_FILE_ENV, str(trace_path))

    bridge = UnityBridge(host="127.0.0.1", port=wire_server.port)
    await bridge.connect()
    try:
        await bridge.send("ping", {})
    finally:
        await bridge.close()

    lines = trace_path.read_text(encoding="utf-8").splitlines()
    assert len(lines) == 1
    assert json.loads(lines[0])["cmd"] == "ping"


async def test_send_does_not_write_when_trace_file_unset(
    wire_server: FakeUnityServer, monkeypatch, tmp_path
):
    """Effect spy on `open` (not just a fixed path): a broken implementation
    that falls back to writing somewhere else when the env var is unset
    must still turn this red, not just when it happens to reuse `trace.jsonl`."""
    monkeypatch.delenv(bridge_cassette.TRACE_FILE_ENV, raising=False)
    trace_path = tmp_path / "trace.jsonl"
    open_calls = []
    real_open = open
    monkeypatch.setattr(
        bridge_cassette, "open",
        lambda *a, **kw: open_calls.append(a) or real_open(*a, **kw),
        raising=False,
    )

    bridge = UnityBridge(host="127.0.0.1", port=wire_server.port)
    await bridge.connect()
    try:
        await bridge.send("ping", {})
    finally:
        await bridge.close()

    assert open_calls == []
    assert not trace_path.exists()


async def test_recorded_line_replays_through_load_cassette(
    wire_server: FakeUnityServer, monkeypatch, tmp_path
):
    trace_path = tmp_path / "trace.jsonl"
    monkeypatch.setenv(bridge_cassette.TRACE_FILE_ENV, str(trace_path))

    bridge = UnityBridge(host="127.0.0.1", port=wire_server.port)
    await bridge.connect()
    try:
        await bridge.send("ping", {})
    finally:
        await bridge.close()

    async with FakeUnityServer() as replay_server:
        replay_server.load_cassette(trace_path)
        replay_bridge = UnityBridge(host="127.0.0.1", port=replay_server.port)
        await replay_bridge.connect()
        try:
            result = await replay_bridge.send("ping", {})
        finally:
            await replay_bridge.close()

    assert result["ok"] is True
    assert result["data"] == "pong"


async def test_send_records_error_response_with_cassette_error_key(
    wire_server: FakeUnityServer, monkeypatch, tmp_path
):
    """Wire protocol errors use the `err` key (test_protocol_shape.py);
    the cassette format load_cassette expects uses `error`. This is the
    one normalization the recorder must get right for round-trip fidelity."""
    trace_path = tmp_path / "trace.jsonl"
    monkeypatch.setenv(bridge_cassette.TRACE_FILE_ENV, str(trace_path))
    wire_server.set_response("trigger_err", ok=False, error="boom")

    bridge = UnityBridge(host="127.0.0.1", port=wire_server.port)
    await bridge.connect()
    try:
        await bridge.send("trigger_err", {})
    finally:
        await bridge.close()

    recorded = json.loads(trace_path.read_text(encoding="utf-8").splitlines()[0])
    assert recorded["response"]["ok"] is False
    assert recorded["response"]["error"] == "boom"

    async with FakeUnityServer() as replay_server:
        replay_server.load_cassette(trace_path)
        replay_bridge = UnityBridge(host="127.0.0.1", port=replay_server.port)
        await replay_bridge.connect()
        try:
            result = await replay_bridge.send("trigger_err", {})
        finally:
            await replay_bridge.close()

    assert result["ok"] is False
    assert result["err"] == "boom"


def test_record_swallows_unserializable_args(monkeypatch, tmp_path, caplog):
    """json.dumps on an unserializable value (e.g. a bare object() slipped
    into args) must not escape record() — the docstring's 'Never raises'
    promise has to cover serialization failures, not just OSError on write."""
    trace_path = tmp_path / "trace.jsonl"
    monkeypatch.setenv(bridge_cassette.TRACE_FILE_ENV, str(trace_path))

    with caplog.at_level(logging.WARNING, logger="unity_mcp.bridge_cassette"):
        bridge_cassette.record("cmd", {"p": object()}, {"ok": True, "data": ""})

    assert not trace_path.exists()
    assert len(caplog.records) == 1
