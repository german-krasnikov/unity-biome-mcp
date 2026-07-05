"""Tests for chat_relay.py — standalone CLI sidecar. All tests are not live (no Unity)."""
import asyncio
import gc
import inspect
import json
import os
import struct
import sys
import pytest
from collections import deque
from unittest.mock import AsyncMock, MagicMock, patch, ANY

from unity_mcp.chat_relay import (
    BufLine, CliSession, ChatRelay, SessionMeta, BACKENDS,
    _esc, _find_free_port, main,
    MAX_BUF, KILL_WAIT, PPID_POLL,
)
from unity_mcp.stream_transform import (
    _transform_plain_text_line, _transform_line,
    _transform_codex_line, _transform_kimi_line, _transform_opencode_line,
)
from unity_mcp.backend_def import (
    OUTPUT_FORMAT_STREAM_JSON, OUTPUT_FORMAT_PLAIN_TEXT,
    OUTPUT_FORMAT_CODEX_JSON, OUTPUT_FORMAT_OPENCODE_JSON, OUTPUT_FORMAT_KIMI_JSON,
)


# ─── Helpers ────────────────────────────────────────────────────────────────

def make_proc(pid=9999, returncode=None, stdout_lines=None):
    """Build a mock asyncio subprocess process."""
    proc = MagicMock()
    proc.pid = pid
    proc.returncode = returncode
    proc.stdin = MagicMock()
    proc.stdin.write = MagicMock()
    proc.stdin.drain = AsyncMock()
    proc.stdin.close = MagicMock()
    lines = (stdout_lines or []) + [b""]
    proc.stdout.readline = AsyncMock(side_effect=[ln if isinstance(ln, bytes) else ln.encode() for ln in lines])
    proc.stderr = MagicMock()
    proc.stderr.readline = AsyncMock(return_value=b"")
    proc.terminate = MagicMock()
    proc.kill = MagicMock()
    proc.wait = AsyncMock()
    return proc


def mock_sess(pid=1234, alive=True, exit_code=None):
    """Build a mock CliSession."""
    sess = MagicMock()
    sess.alive = alive
    sess.pid = pid
    sess.exit_code = exit_code
    sess.kill = AsyncMock()
    sess.write_line = AsyncMock()
    sess.wait = AsyncMock()
    sess.drain_stderr = AsyncMock(return_value="")
    sess._binary = "/bin/cli"
    sess._proc = MagicMock()
    sess._proc.stdin = MagicMock()
    sess._proc.stdin.close = MagicMock()
    return sess


async def tcp_cmd(port, cmd, args=None, req_id="1"):
    """Send one framed JSON command and return parsed response."""
    r, w = await asyncio.open_connection("127.0.0.1", port)
    req = json.dumps({"id": req_id, "cmd": cmd, "args": args or {}}).encode()
    w.write(struct.pack("!I", len(req)) + req)
    await w.drain()
    hdr = await r.readexactly(4)
    length = struct.unpack("!I", hdr)[0]
    resp = json.loads(await r.readexactly(length))
    w.close()
    await w.wait_closed()
    return resp


@pytest.fixture
async def running_relay():
    """Start a real ChatRelay TCP server on a free port."""
    port = _find_free_port()
    relay = ChatRelay()
    server_task = asyncio.create_task(relay.serve(port))
    await asyncio.sleep(0.05)  # let server bind
    yield relay, port
    server_task.cancel()
    try:
        await server_task
    except asyncio.CancelledError:
        pass


# ─── Buffer (8 tests) ───────────────────────────────────────────────────────

def test_buf_seq_monotonic():
    relay = ChatRelay()
    relay._enqueue("a")
    relay._enqueue("b")
    relay._enqueue("c")
    seqs = [b.seq for b in relay._buf]
    assert seqs == [0, 1, 2]


def test_buf_deque_maxlen():
    relay = ChatRelay()
    for i in range(MAX_BUF + 1):
        relay._enqueue(str(i))
    assert len(relay._buf) == MAX_BUF
    # oldest (seq=0) evicted, first now is seq=1
    assert relay._buf[0].seq == 1


def test_buf_seq_no_reset_after_kill():
    relay = ChatRelay()
    relay._enqueue("before")
    relay._enqueue("before2")
    relay._session = None  # simulate kill without actually killing
    relay._enqueue("after")
    assert relay._buf[-1].seq == 2  # monotonic, not reset


async def test_events_all_when_minus1():
    relay = ChatRelay()
    relay._enqueue("x")
    relay._enqueue("y")
    resp = await relay._cmd_events({"after_seq": -1})
    assert resp["ok"] is True
    assert "0\nx\n" in resp["data"]
    assert "1\ny\n" in resp["data"]


async def test_events_filter_after_seq():
    relay = ChatRelay()
    for i in range(8):
        relay._enqueue(f"line{i}")
    resp = await relay._cmd_events({"after_seq": 5})
    assert resp["ok"] is True
    data = resp["data"]
    assert "6\nline6\n" in data
    assert "7\nline7\n" in data
    assert "line5" not in data


async def test_events_empty_when_caught_up():
    relay = ChatRelay()
    relay._enqueue("only")
    resp = await relay._cmd_events({"after_seq": 0})
    assert resp["ok"] is True
    assert resp["data"] == ""


def test_events_format():
    relay = ChatRelay()
    relay._enqueue("hello")
    relay._enqueue("world")
    # Verify exact text format: seq\nline\nseq\nline\n
    lines = list(relay._buf)
    assert lines[0].seq == 0 and lines[0].text == "hello"
    assert lines[1].seq == 1 and lines[1].text == "world"


def test_events_gap_detectable():
    """When buffer wraps, seq is discontinuous — detectable by C#."""
    relay = ChatRelay()
    for i in range(MAX_BUF + 5):
        relay._enqueue(str(i))
    seqs = [b.seq for b in relay._buf]
    # First element should be seq=5 (5 evicted), next seq=6 etc.
    assert seqs[0] == 5
    assert seqs[1] == 6  # no gap within kept range
    # Total count equals maxlen
    assert len(seqs) == MAX_BUF


# ─── Protocol Framing (4 tests) ─────────────────────────────────────────────

async def test_frame_read_big_endian(running_relay):
    relay, port = running_relay
    resp = await tcp_cmd(port, "status")
    assert resp["ok"] is True  # proves BE framing round-trips


async def test_frame_write_big_endian(running_relay):
    """Manually verify response uses 4-byte BE length prefix."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    req = json.dumps({"id": "x", "cmd": "status", "args": {}}).encode()
    w.write(struct.pack("!I", len(req)) + req)
    await w.drain()
    hdr = await r.readexactly(4)
    length = struct.unpack("!I", hdr)[0]
    assert length > 0
    body = await r.readexactly(length)
    parsed = json.loads(body)
    assert parsed["id"] == "x"
    w.close()
    await w.wait_closed()


async def test_frame_max_size_rejected(running_relay):
    """Frame length > 10MB causes server to close connection."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    w.write(struct.pack("!I", 10_000_001))  # just header, oversized
    await w.drain()
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)
    w.close()


async def test_frame_zero_length_rejected(running_relay):
    """Frame length == 0 causes server to close connection."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    w.write(struct.pack("!I", 0))
    await w.drain()
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)
    w.close()


# ─── Spawn (7 tests) ────────────────────────────────────────────────────────

async def test_spawn_starts_subprocess():
    proc = make_proc()
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        relay = ChatRelay()
        result = await relay._cmd_spawn({"binary": "/usr/bin/claude", "argv": ["-p"], "env_set": {}, "env_strip": []})
    assert result["ok"] is True
    assert "pid=9999" in result["data"]


async def test_spawn_env_set_injected():
    proc = make_proc()
    mock_exec = AsyncMock(return_value=proc)
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", mock_exec):
        relay = ChatRelay()
        await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {"MY_KEY": "MY_VAL"}, "env_strip": []})
    env_arg = mock_exec.call_args.kwargs["env"]
    assert env_arg.get("MY_KEY") == "MY_VAL"


async def test_spawn_env_strip_removed():
    proc = make_proc()
    mock_exec = AsyncMock(return_value=proc)
    os.environ["RELAY_TEST_STRIP_ME"] = "secret"
    try:
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", mock_exec):
            relay = ChatRelay()
            await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": ["RELAY_TEST_STRIP_ME"]})
        env_arg = mock_exec.call_args.kwargs["env"]
        assert "RELAY_TEST_STRIP_ME" not in env_arg
    finally:
        os.environ.pop("RELAY_TEST_STRIP_ME", None)


async def test_spawn_returns_pid():
    proc = make_proc(pid=4242)
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        relay = ChatRelay()
        result = await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
    assert "pid=4242" in result["data"]


async def test_spawn_binary_not_found():
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(side_effect=FileNotFoundError("no such file"))):
        relay = ChatRelay()
        result = await relay._cmd_spawn({"binary": "/nonexistent", "argv": [], "env_set": {}, "env_strip": []})
    assert result["ok"] is False
    assert "spawn failed" in result["err"]


async def test_spawn_kills_existing():
    relay = ChatRelay()
    old = mock_sess()
    relay._session = old
    proc = make_proc()
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
    old.kill.assert_called_once()


async def test_spawn_seq_continues():
    """Seq counter is monotonic across spawns."""
    relay = ChatRelay()
    relay._enqueue("pre_spawn_line")  # seq=0
    proc = make_proc()
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
    relay._enqueue("post_spawn_line")
    assert relay._buf[-1].seq == 1  # continued from 0


# ─── Send (4 tests) ─────────────────────────────────────────────────────────

async def test_send_writes_stdin():
    relay = ChatRelay()
    sess = mock_sess()
    relay._session = sess
    result = await relay._cmd_send({"line": "hello relay"})
    assert result["ok"] is True
    sess.write_line.assert_called_once_with("hello relay")


async def test_send_no_session_error():
    relay = ChatRelay()
    result = await relay._cmd_send({"line": "anything"})
    assert result["ok"] is False
    assert result["err"] == "no session"


async def test_send_dead_process_error():
    relay = ChatRelay()
    sess = mock_sess(alive=False, exit_code=1)
    sess.write_line = AsyncMock(side_effect=RuntimeError("process dead (exit=1)"))
    relay._session = sess
    result = await relay._cmd_send({"line": "oops"})
    assert result["ok"] is False
    assert "dead" in result["err"]


async def test_send_utf8():
    relay = ChatRelay()
    sess = mock_sess()
    relay._session = sess
    await relay._cmd_send({"line": "日本語テスト"})
    sess.write_line.assert_called_once_with("日本語テスト")


# ─── _extract_text_from_turn ─────────────────────────────────────────────────

def test_extract_text_single_block():
    from unity_mcp.chat_relay import _extract_text_from_turn
    line = '{"type":"user","message":{"role":"user","content":[{"type":"text","text":"hello!"}]}}'
    assert _extract_text_from_turn(line) == "hello!"

def test_extract_text_fallback_on_bad_json():
    from unity_mcp.chat_relay import _extract_text_from_turn
    assert _extract_text_from_turn("not json") == "not json"

def test_extract_text_multi_blocks_joined():
    from unity_mcp.chat_relay import _extract_text_from_turn
    line = '{"type":"user","message":{"role":"user","content":[{"type":"text","text":"a"},{"type":"image"},{"type":"text","text":"b"}]}}'
    assert _extract_text_from_turn(line) == "a\nb"


# ─── _cmd_start deferred for reads_stdin=False ───────────────────────────────

async def test_cmd_start_agy_empty_prompt_deferred(monkeypatch):
    """_cmd_start with reads_stdin=False backend + empty prompt must NOT spawn."""
    from unity_mcp.backend_def import AgyDef
    relay = ChatRelay()

    fake_backend = AgyDef()
    async def fake_resolve():
        return "/usr/bin/agy"
    monkeypatch.setattr(fake_backend, "resolve_binary", fake_resolve)
    monkeypatch.setitem(BACKENDS, "agy", fake_backend)

    resp = await relay._cmd_start({
        "backend": "agy", "mode": "ask", "model": None,
        "mcp_port": 0, "prompt": "", "config_dir": "/tmp"
    })
    assert resp["ok"] is True
    assert "deferred" in resp["data"]
    assert relay._session is None          # no process spawned
    assert relay._session_meta is not None


# ─── _cmd_send respawn for reads_stdin=False ─────────────────────────────────

async def test_cmd_send_agy_respawns_with_extracted_prompt(monkeypatch):
    """_cmd_send with reads_stdin=False calls _cmd_start with extracted plain text."""
    relay = ChatRelay()
    relay._session_meta = SessionMeta(
        backend="agy", mode="ask", model=None, mcp_port=0,
        prompt="", config_dir="/tmp", extra={}
    )

    spawned_prompts = []
    async def fake_start(args):
        spawned_prompts.append(args.get("prompt", ""))
        return {"ok": True, "data": "spawned pid=999"}
    monkeypatch.setattr(relay, "_cmd_start", fake_start)

    turn_json = '{"type":"user","message":{"role":"user","content":[{"type":"text","text":"fix the bug"}]}}'
    resp = await relay._cmd_send({"line": turn_json})

    assert resp["ok"] is True
    assert spawned_prompts == ["fix the bug"]


# ─── Kill (3 tests) ─────────────────────────────────────────────────────────

async def test_kill_calls_terminate():
    proc = make_proc()
    session = CliSession("/bin/cli", [], {}, [])
    session._proc = proc
    await session.kill()
    proc.terminate.assert_called_once()


async def test_kill_escalates_to_sigkill(monkeypatch):
    monkeypatch.setattr("unity_mcp.chat_relay.KILL_WAIT", 0.01)
    proc = make_proc()
    proc.wait = AsyncMock(side_effect=asyncio.TimeoutError())
    session = CliSession("/bin/cli", [], {}, [])
    session._proc = proc
    await session.kill()
    proc.kill.assert_called_once()


async def test_kill_idempotent():
    relay = ChatRelay()
    await relay._cmd_kill({})  # no session — should not raise
    result = await relay._cmd_kill({})
    assert result["ok"] is True
    assert result["data"] == "killed"


# ─── Status (3 tests) ───────────────────────────────────────────────────────

async def test_status_alive():
    relay = ChatRelay()
    relay._session = mock_sess(pid=777)
    relay._enqueue("x")
    result = await relay._cmd_status({})
    assert result["ok"] is True
    data = result["data"]
    assert data.startswith("alive|pid=777|")
    assert "buf=1" in data


async def test_status_dead():
    relay = ChatRelay()
    relay._session = mock_sess(pid=888, alive=False, exit_code=1)
    result = await relay._cmd_status({})
    assert result["ok"] is True
    assert result["data"].startswith("dead|exit=1|")


async def test_status_no_session():
    relay = ChatRelay()
    result = await relay._cmd_status({})
    assert result["ok"] is True
    assert result["data"].startswith("no_session|seq=")
    assert "buf=0" in result["data"]


# ─── Switch (3 tests) ───────────────────────────────────────────────────────

async def test_switch_kills_old():
    relay = ChatRelay()
    old = mock_sess(pid=1)
    relay._session = old
    proc = make_proc(pid=2)
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        await relay._cmd_switch({"binary": "/bin/new", "argv": [], "env_set": {}, "env_strip": []})
    old.kill.assert_called_once()


async def test_switch_resets_seq_after_kill():
    """C1 FIX: switch kills old session → buf cleared, but seq stays monotonic."""
    relay = ChatRelay()
    relay._enqueue("before_switch")  # seq=0
    old = mock_sess()
    relay._session = old
    proc = make_proc()
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        await relay._cmd_switch({"binary": "/bin/new", "argv": [], "env_set": {}, "env_strip": []})
    relay._enqueue("after_switch")
    assert relay._buf[-1].seq == 1  # C1: seq continues from 1, not reset to 0


async def test_switch_new_session_alive():
    relay = ChatRelay()
    old = mock_sess()
    relay._session = old
    proc = make_proc(pid=99, returncode=None)
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        result = await relay._cmd_switch({"binary": "/bin/new", "argv": [], "env_set": {}, "env_strip": []})
    assert result["ok"] is True
    assert "pid=99" in result["data"]


# ─── Close Stdin (2 tests) ──────────────────────────────────────────────────

async def test_close_stdin_called():
    relay = ChatRelay()
    sess = mock_sess()
    relay._session = sess
    result = await relay._cmd_close_stdin({})
    assert result["ok"] is True
    sess.close_stdin.assert_called_once()


async def test_close_stdin_no_session_ok():
    relay = ChatRelay()
    result = await relay._cmd_close_stdin({})
    assert result["ok"] is True
    assert "stdin closed" in result["data"]


# ─── CLI Crash Handling (3 tests) ───────────────────────────────────────────

async def test_crash_nonzero_exit_enqueues_error():
    relay = ChatRelay()
    sess = mock_sess(alive=False, exit_code=2)
    sess.read_stdout_line = AsyncMock(return_value=None)
    sess._binary = "/bin/claude"
    relay._session = sess

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._buf]
    # EOF error events are pipe-format: e|<message>
    assert any(t.startswith("e|") for t in texts)


async def test_crash_zero_exit_enqueues_done():
    """B3 FIX: exit code 0 now enqueues a done event (previously silent, leaving spinner forever)."""
    relay = ChatRelay()
    sess = mock_sess(alive=False, exit_code=0)
    sess.read_stdout_line = AsyncMock(return_value=None)
    relay._session = sess

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._buf]
    assert len(texts) == 1
    # EOF done events are pipe-format: d|<sid>|<cost>|<in>|<out>
    assert texts[0].startswith("d|")


async def test_crash_error_event_format():
    relay = ChatRelay()
    sess = mock_sess(alive=False, exit_code=3)
    sess.read_stdout_line = AsyncMock(return_value=None)
    sess._binary = "/usr/bin/claude"
    relay._session = sess

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._buf]
    assert len(texts) == 1
    # Pipe format: e|Process claude exited 3
    assert texts[0].startswith("e|")
    assert "claude" in texts[0]


# ─── PPID Watchdog (3 tests) ────────────────────────────────────────────────

async def test_watchdog_dormant_while_alive(monkeypatch):
    relay = ChatRelay()
    exit_called = []
    monkeypatch.setattr("unity_mcp.chat_relay.os._exit", lambda c: exit_called.append(c))
    monkeypatch.setattr("unity_mcp.chat_relay.os.getppid", lambda: relay._orig_ppid)
    monkeypatch.setattr("unity_mcp.chat_relay.PPID_POLL", 0)

    task = asyncio.create_task(relay._ppid_watchdog())
    await asyncio.sleep(0.02)
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        pass

    assert exit_called == []


async def test_watchdog_kills_session_on_orphan(monkeypatch):
    relay = ChatRelay()
    sess = mock_sess()
    relay._session = sess

    def mock_exit(code):
        raise KeyboardInterrupt

    monkeypatch.setattr("unity_mcp.chat_relay.os._exit", mock_exit)
    monkeypatch.setattr("unity_mcp.chat_relay.os.getppid", lambda: relay._orig_ppid + 999)
    monkeypatch.setattr("unity_mcp.chat_relay.PPID_POLL", 0)

    with pytest.raises(KeyboardInterrupt):
        await relay._ppid_watchdog()

    sess.kill.assert_called_once()


async def test_watchdog_exits_process(monkeypatch):
    relay = ChatRelay()
    exit_codes = []

    def mock_exit(code):
        exit_codes.append(code)
        raise KeyboardInterrupt

    monkeypatch.setattr("unity_mcp.chat_relay.os._exit", mock_exit)
    monkeypatch.setattr("unity_mcp.chat_relay.os.getppid", lambda: relay._orig_ppid + 999)
    monkeypatch.setattr("unity_mcp.chat_relay.PPID_POLL", 0)

    with pytest.raises(KeyboardInterrupt):
        await relay._ppid_watchdog()

    assert exit_codes == [0]


# ─── Integration TCP (5 tests) ──────────────────────────────────────────────

async def test_tcp_spawn_send_events_cycle(running_relay):
    relay, port = running_relay
    proc = make_proc(pid=5555, stdout_lines=[b"stream line one\n", b"stream line two\n"])

    # Use plain-text transform so raw lines land as t| events in the buffer
    relay._transform_fn = _transform_plain_text_line
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        spawn_resp = await relay._cmd_spawn({  # direct call — spawn removed from TCP dispatch
            "binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": [],
        })

    assert spawn_resp["ok"] is True
    assert "pid=5555" in spawn_resp["data"]

    await asyncio.sleep(0.05)  # drain loop processes stdout lines

    events_resp = await tcp_cmd(port, "events", {"after_seq": -1})
    assert events_resp["ok"] is True
    assert "t|stream line one" in events_resp["data"]
    assert "t|stream line two" in events_resp["data"]


async def test_reconnect_replay(running_relay):
    relay, port = running_relay
    relay._enqueue("alpha")
    relay._enqueue("beta")
    relay._enqueue("gamma")

    # First connection: get all
    resp1 = await tcp_cmd(port, "events", {"after_seq": -1})
    assert "alpha" in resp1["data"]

    # Second connection (reconnect): replay from after seq=0
    resp2 = await tcp_cmd(port, "events", {"after_seq": 0})
    assert "beta" in resp2["data"]
    assert "gamma" in resp2["data"]
    assert "alpha" not in resp2["data"]


async def test_unknown_cmd_error(running_relay):
    relay, port = running_relay
    resp = await tcp_cmd(port, "bogus_cmd", {})
    assert resp["ok"] is False
    assert "unknown cmd" in resp["err"]


async def test_concurrent_spawn_no_orphan(running_relay):
    """Two spawns in sequence: no zombie (first killed before second)."""
    relay, port = running_relay
    proc1 = make_proc(pid=101)
    proc2 = make_proc(pid=102)

    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(side_effect=[proc1, proc2])):
        r1 = await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
        r2 = await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})

    assert r1["ok"] is True
    assert r2["ok"] is True
    assert "pid=102" in r2["data"]
    # First process got terminate call
    proc1.terminate.assert_called_once()


def test_free_port_finds_available():
    """_find_free_port() returns a port that can be bound."""
    import socket
    port = _find_free_port()
    assert 1024 < port < 65536
    # Verify we can actually bind it
    with socket.socket() as s:
        s.bind(("127.0.0.1", port))  # should not raise


# ─── Protocol Edge Cases (3 tests) ──────────────────────────────────────────

async def test_malformed_json_closes_connection():
    """Bug 1: JSONDecodeError must be caught in _handle_client, not propagate."""
    relay = ChatRelay()
    reader = AsyncMock()
    writer = MagicMock()
    writer.close = MagicMock()

    body = b"NOT_VALID_JSON!!!"
    reader.readexactly = AsyncMock(side_effect=[
        struct.pack("!I", len(body)),  # header
        body,                           # malformed body
    ])

    # Must NOT raise — JSONDecodeError must be swallowed
    await relay._handle_client(reader, writer)
    writer.close.assert_called_once()


async def test_large_valid_message(running_relay):
    """Frame just under MAX_FRAME round-trips correctly."""
    from unity_mcp.chat_relay import MAX_FRAME
    relay, port = running_relay
    large_val = "A" * 100_000  # ~100KB, well under 10MB
    r, w = await asyncio.open_connection("127.0.0.1", port)
    req = json.dumps({"id": "big", "cmd": "status", "args": {"ignore": large_val}}).encode()
    assert len(req) < MAX_FRAME
    w.write(struct.pack("!I", len(req)) + req)
    await w.drain()
    hdr = await r.readexactly(4)
    length = struct.unpack("!I", hdr)[0]
    resp = json.loads(await r.readexactly(length))
    assert resp["ok"] is True
    w.close()
    await w.wait_closed()


async def test_events_without_after_seq_defaults_minus1():
    """Missing after_seq key defaults to -1 (return all events)."""
    relay = ChatRelay()
    relay._enqueue("alpha")
    relay._enqueue("beta")
    resp = await relay._cmd_events({})  # no after_seq
    assert resp["ok"] is True
    assert "alpha" in resp["data"]
    assert "beta" in resp["data"]


# ─── Multi-command Connection (1 test) ───────────────────────────────────────

async def test_multi_command_single_connection(running_relay):
    """Multiple commands on a single TCP connection all receive responses."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)

    async def send_recv(cmd, args, req_id):
        req = json.dumps({"id": req_id, "cmd": cmd, "args": args}).encode()
        w.write(struct.pack("!I", len(req)) + req)
        await w.drain()
        hdr = await r.readexactly(4)
        length = struct.unpack("!I", hdr)[0]
        return json.loads(await r.readexactly(length))

    r1 = await send_recv("status", {}, "1")
    r2 = await send_recv("kill", {}, "2")
    r3 = await send_recv("status", {}, "3")

    assert r1["ok"] is True and r1["id"] == "1"
    assert r2["ok"] is True and r2["id"] == "2"
    assert r3["ok"] is True and r3["id"] == "3"
    w.close()
    await w.wait_closed()


# ─── Connection Lifecycle (3 tests) ──────────────────────────────────────────

async def test_session_survives_client_disconnect(running_relay):
    """Buffer data persists after a client disconnects and reconnects."""
    relay, port = running_relay
    relay._enqueue("persist_me")

    # Abrupt disconnect
    r, w = await asyncio.open_connection("127.0.0.1", port)
    w.close()
    await asyncio.sleep(0.05)

    # Reconnect — data still in buffer
    resp = await tcp_cmd(port, "events", {"after_seq": -1})
    assert "persist_me" in resp["data"]


async def test_server_accepts_after_disconnect(running_relay):
    """Server accepts 3 sequential connections without issue."""
    relay, port = running_relay
    for _ in range(3):
        resp = await tcp_cmd(port, "status")
        assert resp["ok"] is True


async def test_drain_loop_stops_on_session_replace():
    """Old drain loop exits when _session reference is replaced."""
    relay = ChatRelay()
    original = mock_sess()
    new_sess = mock_sess(pid=2)

    call_count = 0

    async def mock_read():
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            # Replace session between iterations — loop should exit at next check
            relay._session = new_sess
            return "some_line"
        return None  # should never reach here

    original.read_stdout_line = mock_read
    relay._session = original

    await asyncio.wait_for(relay._drain_stdout_loop(), timeout=0.5)
    assert call_count == 1  # loop exited without second call


# ─── CliSession Unit (5 tests) ───────────────────────────────────────────────

def test_cli_session_unstarted_properties():
    """Before start(), alive=False, pid=None, exit_code=None."""
    sess = CliSession("/bin/cli", [], {}, [])
    assert sess.alive is False
    assert sess.pid is None
    assert sess.exit_code is None


async def test_write_line_appends_newline_and_encodes():
    """write_line sends line + newline as UTF-8 bytes to stdin."""
    proc = make_proc()
    sess = CliSession("/bin/cli", [], {}, [])
    sess._proc = proc
    await sess.write_line("hello")
    proc.stdin.write.assert_called_once_with(b"hello\n")


async def test_spawn_argv_forwarded():
    """argv elements are passed positionally to create_subprocess_exec."""
    proc = make_proc()
    mock_exec = AsyncMock(return_value=proc)
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", mock_exec):
        relay = ChatRelay()
        await relay._cmd_spawn({
            "binary": "/bin/cli",
            "argv": ["--flag", "val"],
            "env_set": {}, "env_strip": [],
        })
    call_args = mock_exec.call_args.args
    assert call_args[0] == "/bin/cli"
    assert "--flag" in call_args
    assert "val" in call_args


async def test_spawn_missing_binary_returns_error(running_relay):
    """Nonexistent binary → ok=False (spawn failed)."""
    relay, port = running_relay
    resp = await relay._cmd_spawn({
        "binary": "/nonexistent/binary_xyz", "argv": [], "env_set": {}, "env_strip": []
    })
    assert resp["ok"] is False
    assert "spawn failed" in resp["err"]


async def test_read_stdout_line_on_incomplete_read():
    """IncompleteReadError from stdout → read_stdout_line returns None."""
    proc = make_proc()
    proc.stdout.readline = AsyncMock(
        side_effect=asyncio.IncompleteReadError(b"", 10)
    )
    sess = CliSession("/bin/cli", [], {}, [])
    sess._proc = proc
    result = await sess.read_stdout_line()
    assert result is None


# ─── Real Subprocess Integration (1 test) ────────────────────────────────────

async def test_real_subprocess_stdout_buffered(running_relay):
    """Real subprocess stdout lines land in the event buffer."""
    relay, port = running_relay
    # Use plain-text transform so raw stdout lands as t| events
    relay._transform_fn = _transform_plain_text_line
    spawn_resp = await relay._cmd_spawn({  # direct call — spawn removed from TCP dispatch
        "binary": sys.executable,
        "argv": ["-c", "import sys; print('hello_relay'); sys.stdout.flush()"],
        "env_set": {},
        "env_strip": [],
    })
    assert spawn_resp["ok"] is True
    await asyncio.sleep(0.15)  # drain loop processes stdout
    events_resp = await tcp_cmd(port, "events", {"after_seq": -1})
    assert "hello_relay" in events_resp["data"]


# ─── SIGTERM / Shutdown (1 test) ─────────────────────────────────────────────

async def test_shutdown_kills_session():
    """Bug 2: _shutdown() kills child process before exiting."""
    relay = ChatRelay()
    sess = mock_sess()
    relay._session = sess
    with patch("unity_mcp.chat_relay.os._exit"):
        await relay._shutdown()
    sess.kill.assert_called_once()


# ─── Bug Fixes (B1-B8) ──────────────────────────────────────────────────────

async def test_send_large_stdin_no_deadlock():
    """B1: drain() hang must not block write_line indefinitely (timeout=5s)."""
    proc = make_proc()
    async def hanging_drain():
        await asyncio.Event().wait()  # hangs forever
    proc.stdin.drain = hanging_drain
    sess = CliSession("/bin/cli", [], {}, [])
    sess._proc = proc
    # Must complete in <6s despite hanging drain
    await asyncio.wait_for(sess.write_line("x" * 1000), timeout=6.0)


async def test_exit_code_zero_enqueues_done_event():
    """B3: Clean exit (code 0) must enqueue a done event so C# spinner clears."""
    relay = ChatRelay()
    sess = mock_sess(alive=False, exit_code=0)
    sess.read_stdout_line = AsyncMock(return_value=None)
    relay._session = sess

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._buf]
    assert len(texts) == 1
    # EOF clean exit → pipe-format done event d|<sid>|<cost>|<in>|<out>
    assert texts[0].startswith("d|")


async def test_kill_clears_buffer_and_resets_seq():
    """C1: _kill_current() clears buffer but seq stays monotonic (no reset)."""
    relay = ChatRelay()
    relay._session = mock_sess()
    relay._enqueue("event1")
    relay._enqueue("event2")
    assert len(relay._buf) == 2

    await relay._kill_current()

    assert len(relay._buf) == 0
    assert relay._next_seq == 2  # C1: seq continues, not reset to 0


async def test_buffer_overflow_increments_dropped_counter():
    """B2: Silent deque eviction must increment _dropped counter."""
    relay = ChatRelay()
    for i in range(MAX_BUF + 5):
        relay._enqueue(str(i))
    assert relay._dropped == 5


async def test_status_includes_dropped_count():
    """B2: status response includes dropped=N field."""
    relay = ChatRelay()
    for i in range(MAX_BUF + 3):
        relay._enqueue(str(i))
    result = await relay._cmd_status({})
    assert result["ok"] is True
    assert "dropped=3" in result["data"]


async def test_second_client_displaces_first(running_relay):
    """B4: New TCP connection closes the previous client's connection."""
    relay, port = running_relay

    # Client A: open but don't close
    rA, wA = await asyncio.open_connection("127.0.0.1", port)

    # Client B: connects → should displace A
    rB, wB = await asyncio.open_connection("127.0.0.1", port)
    await asyncio.sleep(0.05)  # give relay time to close A

    # A should get EOF (relay closed its writer)
    data = await asyncio.wait_for(rA.read(4), timeout=1.0)
    assert data == b""  # EOF — A was displaced

    # B still works normally
    req = json.dumps({"id": "z", "cmd": "status", "args": {}}).encode()
    wB.write(struct.pack("!I", len(req)) + req)
    await wB.drain()
    hdr = await rB.readexactly(4)
    length = struct.unpack("!I", hdr)[0]
    resp = json.loads(await rB.readexactly(length))
    assert resp["ok"] is True

    wA.close()
    wB.close()


# ─── _cmd_start / _cmd_set_mode (6 tests) ───────────────────────────────────

def _mock_backend(resolve="/bin/claude", has_resume=True,
                  argv=None, env_set=None, env_strip=None):
    """Create a minimal mock BackendDef."""
    b = MagicMock()
    b.binary = "claude"
    b.has_resume = has_resume
    b.resolve_binary = AsyncMock(return_value=resolve)
    b.build_args.return_value = (argv or ["-p"], env_set or {}, env_strip or [])
    return b


async def test_cmd_start_unknown_backend():
    relay = ChatRelay()
    result = await relay._cmd_start({"backend": "no_such_backend"})
    assert result["ok"] is False
    assert "unknown backend" in result["err"]


async def test_cmd_start_binary_not_found():
    relay = ChatRelay()
    with patch.dict(BACKENDS, {"claude": _mock_backend(resolve=None)}, clear=False):
        result = await relay._cmd_start({"backend": "claude", "mode": "ask",
                                         "mcp_port": 9500})
    assert result["ok"] is False
    assert "not found" in result["err"]


async def test_cmd_start_spawns_and_stores_meta():
    relay = ChatRelay()
    proc = make_proc(pid=1111)
    with patch.dict(BACKENDS, {"claude": _mock_backend()}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            result = await relay._cmd_start({"backend": "claude", "mode": "ask",
                                             "mcp_port": 9500})
    assert result["ok"] is True
    assert "pid=1111" in result["data"]
    assert relay._session_meta is not None
    assert relay._session_meta.backend == "claude"
    assert relay._session_meta.mode == "ask"


async def test_cmd_set_mode_no_session():
    relay = ChatRelay()
    result = await relay._cmd_set_mode({"mode": "agent"})
    assert result["ok"] is False
    assert "no active session" in result["err"]


async def test_cmd_set_mode_no_resume_backend():
    relay = ChatRelay()
    relay._session_meta = SessionMeta(
        backend="kimi", mode="ask", model=None,
        mcp_port=9500, prompt="", config_dir=None, extra={},
    )
    result = await relay._cmd_set_mode({"mode": "agent", "session_id": "abc"})
    assert result["ok"] is False
    assert "does not support resume" in result["err"]


async def test_cmd_set_mode_kills_and_respawns():
    relay = ChatRelay()
    relay._session_meta = SessionMeta(
        backend="claude", mode="ask", model=None,
        mcp_port=9500, prompt="", config_dir="/tmp", extra={},
    )
    old = mock_sess()
    relay._session = old
    proc = make_proc(pid=2222)
    with patch.dict(BACKENDS, {"claude": _mock_backend()}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            result = await relay._cmd_set_mode({"mode": "agent", "session_id": "s1"})
    old.kill.assert_called_once()
    assert result["ok"] is True
    assert "pid=2222" in result["data"]
    assert relay._session_meta.mode == "agent"


async def test_set_mode_passes_session_id_to_build_args():
    """set_mode must forward session_id so CLI receives --resume <id>."""
    relay = ChatRelay()
    relay._session_meta = SessionMeta(
        backend="claude", mode="ask", model=None,
        mcp_port=9500, prompt="", config_dir="/tmp", extra={},
    )
    old = mock_sess()
    relay._session = old
    proc = make_proc(pid=3333)
    mock_be = _mock_backend()
    with patch.dict(BACKENDS, {"claude": mock_be}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            result = await relay._cmd_set_mode({"mode": "agent", "session_id": "sess-xyz"})

        assert result["ok"] is True
        # build_args must have been called with the forwarded session_id
        mock_be.build_args.assert_called_once_with(
            mode="agent", model=None, mcp_port=9500,
            prompt="", session_id="sess-xyz", config_dir="/tmp",
        )


# ─── C1: seq monotonic across set_mode ──────────────────────────────────────

async def test_seq_continues_across_set_mode():
    """C1: seq must be monotonic across _kill_current so C# _lastSeq filter works."""
    relay = ChatRelay()
    for i in range(5):
        relay._enqueue(f"before_{i}")
    assert relay._next_seq == 5

    old = mock_sess()
    relay._session = old
    await relay._kill_current()

    assert len(relay._buf) == 0
    assert relay._next_seq == 5  # seq continues, not reset

    for i in range(3):
        relay._enqueue(f"after_{i}")  # seqs 5, 6, 7

    resp = await relay._cmd_events({"after_seq": 4})
    assert resp["ok"] is True
    assert "after_0" in resp["data"]
    assert "after_1" in resp["data"]
    assert "after_2" in resp["data"]


# ─── M4: _esc() whitespace escapes ──────────────────────────────────────────

def test_esc_newline():
    assert _esc("line1\nline2") == "line1\\nline2"


def test_esc_carriage_return():
    assert _esc("a\rb") == "a\\rb"


def test_esc_tab():
    assert _esc("a\tb") == "a\\tb"


def test_esc_combined():
    assert _esc('say "hi\nthere"') == 'say \\"hi\\nthere\\"'


# ─── M5: CodexDef injects mcp_port ──────────────────────────────────────────

def test_codex_build_args_includes_mcp_port():
    from unity_mcp.backend_def import BACKENDS
    with patch("unity_mcp.backend_def.mcp_config_writer.resolve_server_cmd",
               return_value=("/bin/python", ["-m", "unity_mcp.server"])):
        argv, env_set, env_strip = BACKENDS["codex"].build_args(
            mode="ask", model=None, mcp_port=9500, prompt="hi",
        )
    flat = " ".join(str(a) for a in argv)
    assert "UNITY_MCP_PORT" in flat
    assert "9500" in flat


# ─── M3: create_task refs stored ────────────────────────────────────────────

async def test_drain_task_stored_on_spawn():
    proc = make_proc()
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
        relay = ChatRelay()
        await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
    assert relay._drain_task is not None
    assert isinstance(relay._drain_task, asyncio.Task)


async def test_drain_task_stored_on_start():
    relay = ChatRelay()
    proc = make_proc(pid=7777)
    with patch.dict(BACKENDS, {"claude": _mock_backend()}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", AsyncMock(return_value=proc)):
            await relay._cmd_start({"backend": "claude", "mode": "ask", "mcp_port": 9500})
    assert relay._drain_task is not None
    assert isinstance(relay._drain_task, asyncio.Task)


async def test_watchdog_task_stored(running_relay):
    relay, port = running_relay
    assert relay._watchdog_task is not None
    assert isinstance(relay._watchdog_task, asyncio.Task)


# ─── C3: resolve_binary is async ────────────────────────────────────────────

async def test_resolve_binary_does_not_block_event_loop():
    """C3: resolve_binary must be awaitable and not block other tasks."""
    from dataclasses import dataclass as _dc
    from unity_mcp.backend_def import BackendDef

    @_dc
    class _NeverFound(BackendDef):
        name:       str  = "test"
        binary:     str  = "definitely_not_a_real_binary_xyz987"
        has_resume: bool = False
        def build_args(self, **_): return [], {}, []

    ran = asyncio.Event()

    async def background():
        ran.set()

    task = asyncio.create_task(background())
    result = await _NeverFound().resolve_binary()
    assert ran.is_set()   # must be set before await task — proves concurrent execution
    await task
    assert result is None


async def test_which_via_login_shell_returns_none_for_unknown():
    from unity_mcp.backend_def import _which_via_login_shell
    result = await _which_via_login_shell("definitely_not_a_binary_xyz123")
    assert result is None


# ─── m3: Long-poll via asyncio.Event ────────────────────────────────────────

async def test_events_long_poll_returns_when_data_arrives():
    """m3: timeout_ms > 0 blocks until enqueue, then returns data."""
    relay = ChatRelay()

    async def delayed():
        await asyncio.sleep(0.05)
        relay._enqueue("late_line")

    asyncio.create_task(delayed())
    resp = await asyncio.wait_for(
        relay._cmd_events({"after_seq": -1, "timeout_ms": 500}),
        timeout=1.0,
    )
    assert resp["ok"] is True
    assert "late_line" in resp["data"]


async def test_events_long_poll_returns_on_timeout():
    """m3: timeout_ms expires with no data → returns empty string."""
    relay = ChatRelay()
    resp = await asyncio.wait_for(
        relay._cmd_events({"after_seq": -1, "timeout_ms": 50}),
        timeout=1.0,
    )
    assert resp["ok"] is True
    assert resp["data"] == ""


async def test_events_zero_timeout_still_returns_existing():
    """m3: timeout_ms=0 (or absent) returns existing data immediately."""
    relay = ChatRelay()
    relay._enqueue("existing")
    resp = await relay._cmd_events({"after_seq": -1, "timeout_ms": 0})
    assert resp["ok"] is True
    assert "existing" in resp["data"]


# ─── C4: _enqueue escapes embedded newlines ──────────────────────────────────

def test_enqueue_escapes_embedded_newlines():
    """C4: embedded \\n in stdout lines must be escaped before buffering."""
    relay = ChatRelay()
    relay._enqueue("line1\nline2")
    assert relay._buf[-1].text == "line1\\nline2"


# ─── T2: resume_session_id key mismatch fix ──────────────────────────────────

async def test_cmd_start_resume_session_id_reaches_build_args():
    """T2: C# sends resume_session_id → build_args receives session_id=SID."""
    relay = ChatRelay()
    proc = make_proc(pid=1111)
    backend = _mock_backend()
    with patch.dict(BACKENDS, {"claude": backend}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            await relay._cmd_start({
                "backend": "claude", "mode": "ask",
                "mcp_port": 9500, "resume_session_id": "SID42",
            })
    call_kwargs = backend.build_args.call_args.kwargs
    assert call_kwargs.get("session_id") == "SID42"


async def test_cmd_start_resume_session_id_not_in_extra():
    """T2: resume_session_id excluded from extra kwargs (not leaked to build_args as extra)."""
    relay = ChatRelay()
    proc = make_proc(pid=1111)
    with patch.dict(BACKENDS, {"claude": _mock_backend()}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            await relay._cmd_start({
                "backend": "claude", "mode": "ask",
                "mcp_port": 9500, "resume_session_id": "SID42",
            })
    assert "resume_session_id" not in relay._session_meta.extra


# ─── T5: _kill_current drain task cancellation ───────────────────────────────

async def test_kill_current_cancels_drain_task():
    """T5: _kill_current cancels drain task and sets _drain_task to None."""
    relay = ChatRelay()

    async def long_task():
        await asyncio.sleep(100)

    relay._drain_task = asyncio.create_task(long_task())
    relay._session = mock_sess()

    await relay._kill_current()

    assert relay._drain_task is None


async def test_kill_current_no_spurious_event():
    """T5: After kill, buf is empty — drain task cannot enqueue after buf.clear()."""
    relay = ChatRelay()

    async def long_task():
        await asyncio.sleep(100)

    relay._drain_task = asyncio.create_task(long_task())
    relay._session = mock_sess()
    relay._enqueue("pre_kill")

    await relay._kill_current()

    assert len(relay._buf) == 0
    assert relay._drain_task is None


# ─── Synthetic exit events (pipe-format regardless of backend transform) ──────

@pytest.mark.asyncio
async def test_error_exit_synthetic_event_is_pipe_format():
    """Drain loop wraps error-exit as e| pipe event even for plain-text backends."""
    from unity_mcp.stream_transform import _transform_plain_text_line
    relay = ChatRelay()
    relay._transform_fn = _transform_plain_text_line
    sess = mock_sess(alive=False, exit_code=1)
    sess.read_stdout_line = AsyncMock(return_value=None)
    relay._session = sess

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._buf]
    assert len(texts) == 1
    assert texts[0].startswith("e|")
    assert "cli" in texts[0]
    assert "1" in texts[0]


@pytest.mark.asyncio
async def test_clean_exit_synthetic_event_is_done_pipe():
    """Drain loop wraps clean exit as d||0|0|0 regardless of backend transform."""
    from unity_mcp.stream_transform import _transform_plain_text_line
    relay = ChatRelay()
    relay._transform_fn = _transform_plain_text_line
    sess = mock_sess(alive=False, exit_code=0)
    sess.read_stdout_line = AsyncMock(return_value=None)
    relay._session = sess

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._buf]
    assert len(texts) == 1
    assert texts[0] == "d||0|0|0"


# ─── Monkey: Category A — Protocol Torture (10 tests) ───────────────────────

async def test_frame_3byte_partial_header(running_relay):
    """A01: 3-byte partial header → server can't readexactly(4) → closes connection."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    w.write(b"\x00\x00\x00")  # only 3 of 4 header bytes
    await w.drain()
    w.close()
    await asyncio.sleep(0.05)
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)


async def test_frame_exactly_max_frame_then_partial_body(running_relay):
    """A02: header=MAX_FRAME, only 4 bytes of body → server reads MAX_FRAME, gets EOF → closes."""
    from unity_mcp.chat_relay import MAX_FRAME
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    w.write(struct.pack("!I", MAX_FRAME))
    w.write(b"x" * 4)  # far less than MAX_FRAME
    await w.drain()
    w.close()
    await asyncio.sleep(0.05)
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)


async def test_frame_0xFFFFFFFF_header(running_relay):
    """A03: 4-byte 0xFF = 4_294_967_295 > MAX_FRAME → server rejects immediately."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    w.write(struct.pack("!I", 0xFFFFFFFF))
    await w.drain()
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)
    w.close()


async def test_frame_valid_header_null_bytes_body(running_relay):
    """A04: body = b'\\x00\\x00\\x00\\x00' → json.loads raises JSONDecodeError → closes."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    body = b"\x00\x00\x00\x00"
    w.write(struct.pack("!I", len(body)) + body)
    await w.drain()
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)
    w.close()


async def test_frame_valid_header_1byte_body(running_relay):
    """A05: body = b'{' (1 byte, incomplete JSON) → JSONDecodeError → closes."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    body = b"{"
    w.write(struct.pack("!I", len(body)) + body)
    await w.drain()
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)
    w.close()


async def test_frame_valid_json_null_payload(running_relay):
    """A06: body=b'null' → json valid but None.get() AttributeError → connection closes.
    BUG-FOUND: AttributeError propagates unhandled (not in except list), server logs error.
    Observable behavior: connection still closes cleanly via finally block.
    """
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    body = b"null"
    w.write(struct.pack("!I", len(body)) + body)
    await w.drain()
    await asyncio.sleep(0.05)
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)
    w.close()


async def test_frame_valid_json_array_payload(running_relay):
    """A07: body=b'[]' → [].get() AttributeError → same as A06."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    body = b"[]"
    w.write(struct.pack("!I", len(body)) + body)
    await w.drain()
    await asyncio.sleep(0.05)
    with pytest.raises((asyncio.IncompleteReadError, ConnectionResetError, EOFError)):
        await r.readexactly(4)
    w.close()


async def test_frame_two_valid_then_disconnect(running_relay):
    """A08: two valid commands then abrupt close — both responses received before disconnect."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)

    async def send_recv(cmd, req_id):
        req = json.dumps({"id": req_id, "cmd": cmd, "args": {}}).encode()
        w.write(struct.pack("!I", len(req)) + req)
        await w.drain()
        hdr = await r.readexactly(4)
        length = struct.unpack("!I", hdr)[0]
        return json.loads(await r.readexactly(length))

    r1 = await send_recv("status", "a1")
    r2 = await send_recv("kill", "a2")
    assert r1["ok"] is True and r1["id"] == "a1"
    assert r2["ok"] is True and r2["id"] == "a2"
    w.close()
    await w.wait_closed()


async def test_frame_id_echo_preserved(running_relay):
    """A09: request id is echoed verbatim in the response."""
    relay, port = running_relay
    resp = await tcp_cmd(port, "status", req_id="trace-99")
    assert resp["id"] == "trace-99"


async def test_frame_missing_id_field(running_relay):
    """A10: request with no 'id' key → response['id'] == '?' (default)."""
    relay, port = running_relay
    r, w = await asyncio.open_connection("127.0.0.1", port)
    req = json.dumps({"cmd": "status", "args": {}}).encode()  # no "id" key
    w.write(struct.pack("!I", len(req)) + req)
    await w.drain()
    hdr = await r.readexactly(4)
    length = struct.unpack("!I", hdr)[0]
    resp = json.loads(await r.readexactly(length))
    assert resp["id"] == "?"
    w.close()
    await w.wait_closed()


# ─── Monkey: Category B — Command Chaos (10 tests) ──────────────────────────

async def test_cmd_send_missing_line_key():
    """B01: _cmd_send with no 'line' key → KeyError caught → ok=False."""
    relay = ChatRelay()
    relay._session = mock_sess()
    result = await relay._cmd_send({"not_line": "x"})
    assert result["ok"] is False
    assert "line" in result["err"]


async def test_cmd_events_string_after_seq():
    """B02: after_seq='not_a_number' → int() ValueError → dispatch catches → ok=False."""
    relay = ChatRelay()
    result = await relay._dispatch({"cmd": "events", "args": {"after_seq": "not_a_number"}})
    assert result["ok"] is False


async def test_cmd_events_float_timeout():
    """B03: timeout_ms='3.5' → int('3.5') ValueError → ok=False."""
    relay = ChatRelay()
    result = await relay._dispatch({"cmd": "events", "args": {"after_seq": -1, "timeout_ms": "3.5"}})
    assert result["ok"] is False


async def test_cmd_kill_extra_args_ignored():
    """B04: kill with extra unknown args → ok=True (no strict arg checking)."""
    relay = ChatRelay()
    result = await relay._cmd_kill({"extra": "ignored", "garbage": 99})
    assert result["ok"] is True


async def test_cmd_status_extra_args_ignored():
    """B05: status with extra args → ok=True."""
    relay = ChatRelay()
    result = await relay._cmd_status({"noise": True, "another": 42})
    assert result["ok"] is True


async def test_cmd_close_stdin_extra_args_ignored():
    """B06: close_stdin with extra args and no session → ok=True."""
    relay = ChatRelay()
    result = await relay._cmd_close_stdin({"x": 1, "y": 2})
    assert result["ok"] is True


async def test_cmd_start_build_args_exception_caught():
    """B07: backend.build_args raises → ok=False, 'build_args failed' in err."""
    relay = ChatRelay()
    be = _mock_backend()
    be.build_args.side_effect = ValueError("bad mode")
    with patch.dict(BACKENDS, {"claude": be}, clear=False):
        result = await relay._cmd_start({"backend": "claude", "mode": "ask", "mcp_port": 9500})
    assert result["ok"] is False
    assert "build_args failed" in result["err"]


async def test_cmd_set_mode_mode_none_falls_back_to_ask():
    """B08: set_mode({"mode": None}) → mode=None or "ask" → spawns with mode='ask'."""
    relay = ChatRelay()
    relay._session_meta = SessionMeta(
        backend="claude", mode="ask", model=None,
        mcp_port=9500, prompt="", config_dir="/tmp", extra={},
    )
    relay._session = mock_sess()
    proc = make_proc(pid=100)
    be = _mock_backend()
    with patch.dict(BACKENDS, {"claude": be}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            result = await relay._cmd_set_mode({"mode": None})
    assert result["ok"] is True
    assert relay._session_meta.mode == "ask"


async def test_cmd_switch_kills_and_respawns_with_same_binary():
    """B09: switch with identical binary → kill called once, new session started."""
    relay = ChatRelay()
    old = mock_sess(pid=1)
    relay._session = old
    proc = make_proc(pid=2)
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=proc)):
        result = await relay._cmd_switch({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
    old.kill.assert_called_once()
    assert result["ok"] is True
    assert "pid=2" in result["data"]


async def test_cmd_unknown_with_args():
    """B10: unknown command with args → ok=False, error names the command."""
    relay = ChatRelay()
    resp = await relay._dispatch({"cmd": "xyzzy", "args": {"foo": "bar"}, "id": "x"})
    assert resp["ok"] is False
    assert "xyzzy" in resp["err"]


# ─── Monkey: Category C — Session Lifecycle (8 tests) ───────────────────────

async def test_rapid_spawn_x3_only_last_alive():
    """C01: three spawns in sequence → first two killed, only third survives."""
    proc1 = make_proc(pid=1)
    proc2 = make_proc(pid=2)
    proc3 = make_proc(pid=3)
    relay = ChatRelay()
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(side_effect=[proc1, proc2, proc3])):
        await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
        await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
        await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
    proc1.terminate.assert_called_once()
    proc2.terminate.assert_called_once()
    assert relay._session._proc is proc3


async def test_spawn_then_immediate_kill_clears_session():
    """C03: spawn → kill → _session is None."""
    proc = make_proc()
    relay = ChatRelay()
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=proc)):
        await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
    assert relay._session is not None
    await relay._cmd_kill({})
    assert relay._session is None


async def test_start_then_set_mode_updates_meta():
    """C04: start (mode=ask) → set_mode (mode=agent) → meta.mode updated."""
    relay = ChatRelay()
    proc1 = make_proc(pid=1)
    proc2 = make_proc(pid=2)
    be = _mock_backend()
    with patch.dict(BACKENDS, {"claude": be}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(side_effect=[proc1, proc2])):
            await relay._cmd_start({"backend": "claude", "mode": "ask", "mcp_port": 9500})
            assert relay._session_meta.mode == "ask"
            relay._session = mock_sess()  # replace so kill() is a clean mock
            await relay._cmd_set_mode({"mode": "agent", "session_id": "s1"})
    assert relay._session_meta.mode == "agent"


async def test_spawn_failure_on_second_spawn_leaves_no_session():
    """C06: first spawn ok, second spawn FileNotFoundError → _session is None."""
    proc1 = make_proc(pid=1)
    relay = ChatRelay()
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(side_effect=[proc1, FileNotFoundError("nope")])):
        r1 = await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
        assert r1["ok"] is True
        r2 = await relay._cmd_spawn({"binary": "/bin/cli", "argv": [], "env_set": {}, "env_strip": []})
    assert r2["ok"] is False
    assert relay._session is None


async def test_session_dead_between_status_calls():
    """C07: alive mock → status='alive'; swap dead mock → status='dead|exit=2'."""
    relay = ChatRelay()
    relay._session = mock_sess(pid=10, alive=True)
    r1 = await relay._cmd_status({})
    assert "alive" in r1["data"]
    relay._session = mock_sess(pid=10, alive=False, exit_code=2)
    r2 = await relay._cmd_status({})
    assert "dead|exit=2" in r2["data"]


async def test_kill_seq_monotonic_after_three_spawns():
    """C08: enqueue 5 → kill → enqueue 3 → last.seq == 7 (not reset to 2)."""
    relay = ChatRelay()
    for i in range(5):
        relay._enqueue(str(i))
    relay._session = mock_sess()
    await relay._kill_current()
    for i in range(3):
        relay._enqueue(str(i))
    assert relay._buf[-1].seq == 7


async def test_start_meta_stores_extra_keys():
    """C09: extra keys in _cmd_start args land in _session_meta.extra."""
    relay = ChatRelay()
    proc = make_proc(pid=1)
    with patch.dict(BACKENDS, {"claude": _mock_backend()}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            await relay._cmd_start({"backend": "claude", "mode": "ask",
                                    "mcp_port": 9500, "extra_key": "val"})
    assert relay._session_meta.extra.get("extra_key") == "val"


async def test_switch_is_alias_for_spawn():
    """C10: switch() kills old session and spawns new — functionally identical to spawn."""
    relay = ChatRelay()
    old = mock_sess(pid=1)
    relay._session = old
    proc = make_proc(pid=42)
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=proc)):
        result = await relay._cmd_switch({"binary": "/bin/new", "argv": [], "env_set": {}, "env_strip": []})
    old.kill.assert_called_once()
    assert result["ok"] is True
    assert "pid=42" in result["data"]


# ─── Monkey: Category D — Buffer Stress (10 tests) ──────────────────────────

def test_buf_overflow_exact_boundary():
    """D01: enqueue MAX_BUF items → dropped==0; one more → dropped==1, seq[0]==1."""
    relay = ChatRelay()
    for i in range(MAX_BUF):
        relay._enqueue(str(i))
    assert len(relay._buf) == MAX_BUF
    assert relay._dropped == 0
    relay._enqueue("overflow")
    assert relay._dropped == 1
    assert relay._buf[0].seq == 1  # seq=0 was evicted


def test_buf_seq_after_100_overflow():
    """D02: enqueue MAX_BUF+100 items → buf[0].seq==100, buf[-1].seq==MAX_BUF+99."""
    relay = ChatRelay()
    for i in range(MAX_BUF + 100):
        relay._enqueue(str(i))
    assert relay._buf[0].seq == 100
    assert relay._buf[-1].seq == MAX_BUF + 99
    assert relay._dropped == 100


async def test_events_after_seq_future_returns_empty():
    """D03: events(after_seq=9999) when buf has only 5 items → data==''."""
    relay = ChatRelay()
    for i in range(5):
        relay._enqueue(str(i))
    resp = await relay._cmd_events({"after_seq": 9999})
    assert resp["ok"] is True
    assert resp["data"] == ""


async def test_events_after_wrapped_buf():
    """D04: after overflow, after_seq=9 → first returned item has seq==10 (not 6)."""
    relay = ChatRelay()
    for i in range(MAX_BUF + 10):  # seqs 0..MAX_BUF+9; buf keeps seqs 10..MAX_BUF+9
        relay._enqueue(str(i))
    resp = await relay._cmd_events({"after_seq": 9})
    assert resp["ok"] is True
    first_seq = int(resp["data"].split("\n")[0])
    assert first_seq == 10  # seq 6-9 were evicted — no discontinuity visible in buf


def test_enqueue_carriage_return_escaped():
    """D05: embedded \\r must be escaped like \\n."""
    relay = ChatRelay()
    relay._enqueue("line\r")
    assert relay._buf[-1].text == "line\\r"


def test_enqueue_tab_passes_through():
    """D06: tab chars are NOT escaped by _enqueue — pass through unchanged."""
    relay = ChatRelay()
    relay._enqueue("a\tb")
    assert relay._buf[-1].text == "a\tb"


def test_enqueue_empty_string():
    """D07: _enqueue('') → buf[-1].text=='' and seq==0."""
    relay = ChatRelay()
    relay._enqueue("")
    assert relay._buf[-1].text == ""
    assert relay._buf[-1].seq == 0


def test_new_data_event_set_after_enqueue():
    """D08: _new_data event is clear initially, set after first enqueue."""
    relay = ChatRelay()
    assert relay._new_data.is_set() is False
    relay._enqueue("x")
    assert relay._new_data.is_set() is True


async def test_events_long_poll_1ms_returns_fast():
    """D09: timeout_ms=1 on empty buf completes in <0.5s (not stuck)."""
    import time
    relay = ChatRelay()
    t0 = time.monotonic()
    resp = await asyncio.wait_for(
        relay._cmd_events({"after_seq": -1, "timeout_ms": 1}),
        timeout=0.5,
    )
    assert time.monotonic() - t0 < 0.5
    assert resp["ok"] is True
    assert resp["data"] == ""


def test_buf_type_is_deque_with_maxlen():
    """D10: _buf is a collections.deque with maxlen==MAX_BUF."""
    relay = ChatRelay()
    assert type(relay._buf) is deque
    assert relay._buf.maxlen == MAX_BUF


# ─── C2: spawn/switch removed from TCP dispatch ──────────────────────────────

async def test_spawn_cmd_rejected_via_tcp(running_relay):
    """C2: spawn no longer in dispatch — arbitrary binary exec blocked via TCP."""
    relay, port = running_relay
    resp = await tcp_cmd(port, "spawn", {"binary": "/bin/sh", "argv": ["-c", "id"]})
    assert resp["ok"] is False
    assert "unknown cmd" in resp["err"]


async def test_switch_cmd_rejected_via_tcp(running_relay):
    """C2: switch delegates to spawn — removed from dispatch too."""
    relay, port = running_relay
    resp = await tcp_cmd(port, "switch", {"binary": "/bin/sh", "argv": []})
    assert resp["ok"] is False
    assert "unknown cmd" in resp["err"]


async def test_start_rejects_unknown_backend_via_tcp(running_relay):
    """C2: start validates via BACKENDS registry — arbitrary binary impossible."""
    relay, port = running_relay
    resp = await tcp_cmd(port, "start", {"backend": "/bin/sh", "mode": "ask", "mcp_port": 9500})
    assert resp["ok"] is False
    assert "unknown backend" in resp["err"]


# ─── T1/T2: _transform_fn set by _cmd_start ─────────────────────────────────

async def test_cmd_start_codex_sets_codex_transform_fn():
    """T1: _cmd_start(backend=codex) must set _transform_fn = _transform_codex_line."""
    relay = ChatRelay()
    proc = make_proc(pid=5001)
    backend = _mock_backend()
    backend.output_format = OUTPUT_FORMAT_CODEX_JSON
    with patch.dict(BACKENDS, {"codex": backend}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            result = await relay._cmd_start({"backend": "codex", "mode": "ask", "mcp_port": 9601})
    assert result["ok"] is True
    assert relay._transform_fn is _transform_codex_line


@pytest.mark.parametrize("backend_name,output_format,expected_fn", [
    ("claude",    OUTPUT_FORMAT_STREAM_JSON,   _transform_line),
    ("codex",     OUTPUT_FORMAT_CODEX_JSON,    _transform_codex_line),
    ("kimi",      OUTPUT_FORMAT_KIMI_JSON,     _transform_kimi_line),
    ("agy",       OUTPUT_FORMAT_PLAIN_TEXT,    _transform_plain_text_line),
    ("opencode",  OUTPUT_FORMAT_OPENCODE_JSON, _transform_opencode_line),
])
async def test_cmd_start_sets_correct_transform_per_backend(
        backend_name, output_format, expected_fn):
    """T2: every backend maps to the correct transform function."""
    relay = ChatRelay()
    proc = make_proc(pid=5002)
    backend = _mock_backend()
    backend.output_format = output_format
    with patch.dict(BACKENDS, {backend_name: backend}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            await relay._cmd_start({"backend": backend_name, "mode": "ask", "mcp_port": 9500})
    assert relay._transform_fn is expected_fn


# ─── T4: env_set overrides inherited UNITY_MCP_PORT ─────────────────────────

async def test_cli_session_env_set_overrides_inherited_port(monkeypatch):
    """T4: env_set UNITY_MCP_PORT=9601 wins over inherited os.environ UNITY_MCP_PORT=9999."""
    monkeypatch.setenv("UNITY_MCP_PORT", "9999")
    proc = make_proc(pid=5004)
    mock_exec = AsyncMock(return_value=proc)
    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec", mock_exec):
        relay = ChatRelay()
        await relay._cmd_spawn({
            "binary": "/bin/cli",
            "argv": [],
            "env_set": {"UNITY_MCP_PORT": "9601"},
            "env_strip": [],
        })
    env_arg = mock_exec.call_args.kwargs["env"]
    assert env_arg["UNITY_MCP_PORT"] == "9601"


# ─── T5: main() — sync console-script entrypoint ────────────────────────────

def test_main_is_not_a_coroutine_function():
    """Console-script entrypoints must be plain sync functions."""
    assert inspect.iscoroutinefunction(main) is False


def test_main_invokes_asyncio_run_with_main_coroutine():
    """main() should drive the async _main() coroutine via asyncio.run."""
    with patch("unity_mcp.chat_relay.asyncio.run") as mock_run:
        main()

    mock_run.assert_called_once()
    coro = mock_run.call_args.args[0]
    assert inspect.iscoroutine(coro)
    coro.close()  # avoid "coroutine was never awaited" warning
