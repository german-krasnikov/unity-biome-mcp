"""Ask-mode monkey tests: permission flags, tool relay, model switching, multi-turn.

~100 parametrized tests complementing test_monkey_chat_focus.py (200) and
test_monkey_relay_stress.py (201). All marked @pytest.mark.monkey.

Sections:
  A. Ask mode per backend — 6 backends × 6 scenarios               (36)
  B. Chat tool relay simulation — 10 tools × 3 scenarios            (30)
  C. Model switching per backend — 6 backends × 4 models            (24)
  D. Multi-turn conversation simulation — 10 standalone             (10)
  -- Sanity count (non-monkey)                                        (1)

Key invariants verified:
  Claude ask→"plan", agent→"acceptEdits"
  Codex ask→"-s ask", agent→"-s danger-full-access"
  Kimi no mode flag at all
  Agy ask→no --dangerously-skip-permissions, agent→has it
  OpenCode always --dangerously-skip-permissions

Run: pytest tests/test_monkey_chat_askmode.py -m monkey -v --timeout=60
"""
import asyncio
import json
import struct
import tempfile
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.backend_def import BACKENDS, ClaudeDef, CodexDef, KimiDef, AgyDef, OpenCodeDef
from unity_mcp.chat_relay import ChatRelay, CliSession, SessionMeta, MAX_BUF
from .relay_helpers import make_proc, mock_sess, fresh_relay  # noqa: F401

pytestmark = pytest.mark.monkey

# ─── Constants ────────────────────────────────────────────────────────────────

ALL_6       = ["claude", "codex", "kimi", "agy", "antigravity", "opencode"]
WITH_RESUME = {"claude", "codex", "opencode"}

# Per-backend model strings that are "native" (non-generic)
NATIVE_MODEL = {
    "claude":      "claude-opus-4-5",
    "codex":       "gpt-4o",
    "kimi":        "kimi-k1-5",
    "agy":         "gemini-2.0-flash",
    "antigravity": "gemini-1.5-pro",
    "opencode":    "anthropic/claude-opus-4-5",
}

GENERIC_MODELS = ["sonnet", "haiku", "opus"]

# ─── Helpers ──────────────────────────────────────────────────────────────────

def real_argv(backend_name: str, mode: str, model: str | None = None,
              mcp_port: int = 9500, config_dir: str | None = None) -> list[str]:
    """Call the real build_args with mocked FS writers, return argv."""
    backend = BACKENDS[backend_name]
    config_dir = config_dir or tempfile.gettempdir()

    with patch("unity_mcp.mcp_config_writer.write_claude_config",
               return_value="/tmp/fake-config.json"), \
         patch("unity_mcp.mcp_config_writer.write_kimi_mcp_config"), \
         patch("unity_mcp.mcp_config_writer.write_agy_settings"), \
         patch("unity_mcp.mcp_config_writer.write_opencode_config",
               return_value="/tmp/fake-oc-config.json"), \
         patch("unity_mcp.mcp_config_writer.resolve_server_cmd",
               return_value=("/bin/python3", ["-m", "unity_mcp.server"])):
        argv, _, _ = backend.build_args(
            mode=mode, model=model, mcp_port=mcp_port,
            prompt="test prompt", config_dir=config_dir,
        )
    return argv


# ══════════════════════════════════════════════════════════════════════════════
# A. Ask Mode Per Backend — 6 backends × 6 scenarios = 36
# ══════════════════════════════════════════════════════════════════════════════
#
# Scenario 0: ask mode → correct permission flag in argv
# Scenario 1: agent mode → correct permission flag in argv
# Scenario 2: ask vs agent produce different argv (or same, backend-dependent)
# Scenario 3: mode stored in SessionMeta after _cmd_start
# Scenario 4: set_mode changes mode in meta (resume backends) / fails (no-resume)
# Scenario 5: mode flag position correct (model doesn't corrupt mode flag)

@pytest.mark.parametrize("backend,scenario", [
    (b, s) for b in ALL_6 for s in range(6)
])
async def test_ask_mode_per_backend(backend: str, scenario: int) -> None:
    """Verify ask/agent mode produces the correct permission flags per backend."""

    if scenario == 0:
        # ask mode: check expected permission arg
        argv = real_argv(backend, "ask")
        if backend == "claude":
            assert "--permission-mode" in argv
            idx = argv.index("--permission-mode")
            assert argv[idx + 1] == "plan", f"claude ask should use plan, got {argv[idx+1]}"
        elif backend == "codex":
            # T9: ask mode uses -s ask (not danger-full-access)
            assert "-s" in argv
            assert argv[argv.index("-s") + 1] == "ask", "codex ask should use -s ask"
        elif backend in ("kimi",):
            # kimi has no mode flag at all
            assert "--permission-mode" not in argv
            assert "--dangerously-skip-permissions" not in argv
        elif backend in ("agy", "antigravity"):
            assert "--dangerously-skip-permissions" not in argv, \
                f"{backend} ask must NOT have dangerously-skip-permissions"
        elif backend == "opencode":
            assert "--dangerously-skip-permissions" in argv, \
                "opencode always has dangerously-skip-permissions"

    elif scenario == 1:
        # agent mode: check expected permission arg
        argv = real_argv(backend, "agent")
        if backend == "claude":
            assert "--permission-mode" in argv
            idx = argv.index("--permission-mode")
            assert argv[idx + 1] == "acceptEdits", \
                f"claude agent should use acceptEdits, got {argv[idx+1]}"
        elif backend == "codex":
            assert "danger-full-access" in argv, "codex agent should still use danger-full-access"
        elif backend in ("kimi",):
            assert "--permission-mode" not in argv
            assert "--dangerously-skip-permissions" not in argv
        elif backend in ("agy", "antigravity"):
            assert "--dangerously-skip-permissions" in argv, \
                f"{backend} agent must have dangerously-skip-permissions"
        elif backend == "opencode":
            assert "--dangerously-skip-permissions" in argv

    elif scenario == 2:
        # ask vs agent produce different or same argv per backend contract
        argv_ask   = real_argv(backend, "ask")
        argv_agent = real_argv(backend, "agent")
        if backend == "claude":
            # only --permission-mode value differs
            assert argv_ask != argv_agent
            ask_idx   = argv_ask.index("--permission-mode")
            agent_idx = argv_agent.index("--permission-mode")
            assert argv_ask[ask_idx + 1]   == "plan"
            assert argv_agent[agent_idx + 1] == "acceptEdits"
        elif backend == "codex":
            # T9: ask and agent produce different approval flags
            assert argv_ask != argv_agent, "codex ask vs agent must differ in approval flag"
            assert argv_ask[argv_ask.index("-s") + 1] == "ask"
            assert argv_agent[argv_agent.index("-s") + 1] == "danger-full-access"
        elif backend in ("kimi",):
            # kimi ignores mode entirely
            assert argv_ask == argv_agent
        elif backend in ("agy", "antigravity"):
            assert argv_ask != argv_agent
            assert "--dangerously-skip-permissions" not in argv_ask
            assert "--dangerously-skip-permissions" in argv_agent
        elif backend == "opencode":
            # opencode always skips — same either way
            assert "--dangerously-skip-permissions" in argv_ask
            assert "--dangerously-skip-permissions" in argv_agent

    elif scenario == 3:
        # mode stored in SessionMeta after _cmd_start
        relay = fresh_relay()
        mock_b = MagicMock()
        mock_b.name = backend
        mock_b.binary = backend
        mock_b.has_resume = backend in WITH_RESUME
        mock_b.resolve_binary = AsyncMock(return_value="/bin/fake-cli")
        mock_b.build_args.return_value = (["-p"], {}, [])

        with patch.dict(BACKENDS, {backend: mock_b}, clear=False), \
             patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=make_proc())):
            r = await relay._cmd_start({
                "backend": backend, "mode": "ask", "mcp_port": 9500, "prompt": "",
            })
        assert r["ok"] is True
        assert relay._session_meta is not None
        assert relay._session_meta.mode == "ask"
        assert relay._session_meta.backend == backend

    elif scenario == 4:
        # set_mode: resume backends switch, no-resume fail
        relay = fresh_relay()
        mock_b = MagicMock()
        mock_b.name = backend
        mock_b.binary = backend
        mock_b.has_resume = backend in WITH_RESUME
        mock_b.resolve_binary = AsyncMock(return_value="/bin/fake-cli")
        mock_b.build_args.return_value = (["-p"], {}, [])

        with patch.dict(BACKENDS, {backend: mock_b}, clear=False), \
             patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=make_proc())):
            await relay._cmd_start({
                "backend": backend, "mode": "ask", "mcp_port": 9500, "prompt": "",
            })
            if backend in WITH_RESUME:
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(return_value=make_proc(pid=999))):
                    rs = await relay._cmd_set_mode({"mode": "agent"})
                assert rs["ok"] is True
                assert relay._session_meta.mode == "agent"
            else:
                rs = await relay._cmd_set_mode({"mode": "agent"})
                assert rs["ok"] is False
                assert "does not support resume" in rs["err"]

    else:  # scenario == 5
        # model doesn't corrupt mode flag; both present and correct
        argv = real_argv(backend, "ask", model="some-model")
        if backend == "claude":
            assert "--permission-mode" in argv
            idx = argv.index("--permission-mode")
            assert argv[idx + 1] == "plan"
            assert "--model" in argv
            midx = argv.index("--model")
            assert argv[midx + 1] == "some-model"
        elif backend in ("codex", "kimi", "agy", "antigravity", "opencode"):
            assert "--model" in argv
            midx = argv.index("--model")
            assert argv[midx + 1] == "some-model"


# ══════════════════════════════════════════════════════════════════════════════
# B. Chat Tool Relay Simulation — 10 tool types × 3 scenarios = 30
# ══════════════════════════════════════════════════════════════════════════════
#
# Scenario 0: relay forwards the line successfully (alive session)
# Scenario 1: relay returns error when session is dead
# Scenario 2: line contains valid JSON tool call structure (no corruption)

TOOL_LINES = [
    # (tool_name, sample line)
    ("create_object", json.dumps({"type": "tool_use", "name": "mcp__unity__create_object",
                                  "input": {"name": "Cube", "primitive": "Cube"}})),
    ("set_property",  json.dumps({"type": "tool_use", "name": "mcp__unity__set_property",
                                  "input": {"path": "/Cube", "component": "Transform",
                                            "property": "position", "value": [1, 2, 3]}})),
    ("get_hierarchy", json.dumps({"type": "tool_result", "content": "Cube\\n  Child"})),
    ("batch",         json.dumps({"type": "tool_use", "name": "mcp__unity__batch",
                                  "input": {"ops": [{"cmd": "create_object",
                                                     "args": {"name": f"O{i}"}}
                                                    for i in range(5)]}})),
    ("screenshot",    json.dumps({"type": "tool_result", "content": "data:image/png;base64,iVBOR"})),
    ("drag",          json.dumps({"type": "tool_use", "name": "mcp__unity__set_property",
                                  "input": {"path": "/Obj", "component": "Transform",
                                            "property": "position", "value": [3.5, 0, -2.0]}})),
    ("polygon",       json.dumps({"type": "tool_use", "name": "mcp__unity__execute_code",
                                  "input": {"code": "DrawPolygon(new[]{new(0,0),new(1,0),new(0,1)})"}})),
    ("execute_code",  json.dumps({"type": "tool_use", "name": "mcp__unity__execute_code",
                                  "input": {"code": "Debug.Log(\"hello\");"}})),
    ("set_active",    json.dumps({"type": "tool_use", "name": "mcp__unity__set_active",
                                  "input": {"path": "/Cube", "active": False}})),
    ("get_component", json.dumps({"type": "tool_result",
                                  "content": "Transform position=(0,0,0) rotation=(0,0,0)"})),
]


@pytest.mark.parametrize("tool_idx,scenario", [
    (t, s) for t in range(len(TOOL_LINES)) for s in range(3)
])
async def test_chat_tool_relay(tool_idx: int, scenario: int) -> None:
    """Relay forwards chat tool lines correctly; alive/dead session handling."""
    tool_name, line = TOOL_LINES[tool_idx]
    relay = fresh_relay()

    if scenario == 0:
        # alive session: send succeeds
        relay._session = mock_sess(alive=True)
        r = await relay._cmd_send({"line": line})
        assert r["ok"] is True
        relay._session.write_line.assert_called_once_with(line)

    elif scenario == 1:
        # no session: error
        relay._session = None
        r = await relay._cmd_send({"line": line})
        assert r["ok"] is False
        assert "no session" in r["err"]

    else:
        # dead session: RuntimeError from write_line
        relay._session = mock_sess(alive=False)
        relay._session.write_line = AsyncMock(
            side_effect=RuntimeError("process dead (exit=1)")
        )
        r = await relay._cmd_send({"line": line})
        assert r["ok"] is False
        assert "process dead" in r["err"]


# ══════════════════════════════════════════════════════════════════════════════
# C. Model Switching Per Backend — 6 backends × 4 models = 24
# ══════════════════════════════════════════════════════════════════════════════
#
# Models tested: "sonnet", "haiku", "opus", backend-native model

@pytest.mark.parametrize("backend,model_idx", [
    (b, m) for b in ALL_6 for m in range(4)
])
def test_model_switch_per_backend(backend: str, model_idx: int) -> None:
    """--model flag appears correctly in argv for all backends and model names."""
    generic = GENERIC_MODELS[model_idx] if model_idx < 3 else NATIVE_MODEL[backend]
    argv = real_argv(backend, "ask", model=generic)

    assert "--model" in argv, f"{backend} model={generic}: --model missing from argv={argv}"
    midx = argv.index("--model")
    assert argv[midx + 1] == generic, \
        f"{backend} model flag value wrong: expected {generic!r}, got {argv[midx+1]!r}"

    # also verify mode isn't broken when model present
    if backend == "claude":
        assert "--permission-mode" in argv
        pidx = argv.index("--permission-mode")
        assert argv[pidx + 1] == "plan"
    elif backend in ("agy", "antigravity"):
        assert "--dangerously-skip-permissions" not in argv  # ask mode
    elif backend == "codex":
        # T9: model+ask mode uses -s ask
        assert argv[argv.index("-s") + 1] == "ask"


# ══════════════════════════════════════════════════════════════════════════════
# D. Multi-Turn Conversation Simulation — 10 standalone scenarios
# ══════════════════════════════════════════════════════════════════════════════

async def test_multiturn_5turn_conversation() -> None:
    """5-turn conversation: each turn is a send + events poll."""
    relay = fresh_relay()
    relay._session = mock_sess()

    for turn in range(5):
        msg = json.dumps({"type": "user", "content": f"turn {turn} message"})
        rs = await relay._cmd_send({"line": msg})
        assert rs["ok"] is True

        # simulate a response arriving
        relay._enqueue(f'{{"type":"assistant","turn":{turn}}}')
        ev = await relay._cmd_events({"after_seq": turn - 1})
        assert ev["ok"] is True
        assert f'"turn":{turn}' in ev["data"]


async def test_multiturn_10turn_with_3_tool_calls() -> None:
    """10-turn conversation with 3 tool-call events interspersed."""
    relay = fresh_relay()
    relay._session = mock_sess()

    tool_turns = {2, 5, 8}
    for turn in range(10):
        if turn in tool_turns:
            line = json.dumps({"type": "tool_use", "name": "mcp__unity__get_hierarchy",
                               "input": {}})
        else:
            line = json.dumps({"type": "user", "content": f"msg {turn}"})
        rs = await relay._cmd_send({"line": line})
        assert rs["ok"] is True, f"turn {turn} send failed: {rs}"

    # verify all tool turns were forwarded
    assert relay._session.write_line.call_count == 10


async def test_multiturn_mode_switch_at_turn3() -> None:
    """ask→agent mode switch mid-conversation (claude/resume backend)."""
    relay = fresh_relay()
    mock_b = MagicMock()
    mock_b.name = "claude"
    mock_b.binary = "claude"
    mock_b.has_resume = True
    mock_b.resolve_binary = AsyncMock(return_value="/bin/claude")
    mock_b.build_args.return_value = (["-p", "--permission-mode", "plan"], {}, [])

    with patch.dict(BACKENDS, {"claude": mock_b}, clear=False), \
         patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=make_proc(pid=10))):
        await relay._cmd_start({
            "backend": "claude", "mode": "ask", "mcp_port": 9500, "prompt": "",
        })

    relay._session = mock_sess(pid=10)

    # turns 0-2 in ask mode
    for t in range(3):
        rs = await relay._cmd_send({"line": f"ask turn {t}"})
        assert rs["ok"] is True

    # switch to agent at turn 3
    mock_b.build_args.return_value = (["-p", "--permission-mode", "acceptEdits"], {}, [])
    with patch.dict(BACKENDS, {"claude": mock_b}, clear=False), \
         patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=make_proc(pid=11))):
        rs = await relay._cmd_set_mode({"mode": "agent", "session_id": "s-abc"})
    assert rs["ok"] is True
    assert relay._session_meta.mode == "agent"


async def test_multiturn_model_switch_mid_conversation() -> None:
    """Model switch mid-conversation restarts session with new model in meta."""
    relay = fresh_relay()
    mock_b = MagicMock()
    mock_b.name = "claude"
    mock_b.binary = "claude"
    mock_b.has_resume = True
    mock_b.resolve_binary = AsyncMock(return_value="/bin/claude")
    mock_b.build_args.return_value = (["-p", "--model", "sonnet"], {}, [])

    with patch.dict(BACKENDS, {"claude": mock_b}, clear=False), \
         patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=make_proc(pid=20))):
        r = await relay._cmd_start({
            "backend": "claude", "mode": "ask", "mcp_port": 9500,
            "prompt": "", "model": "sonnet",
        })
    assert r["ok"] is True
    assert relay._session_meta.model == "sonnet"

    # restart with opus
    with patch.dict(BACKENDS, {"claude": mock_b}, clear=False), \
         patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=make_proc(pid=21))):
        r2 = await relay._cmd_start({
            "backend": "claude", "mode": "ask", "mcp_port": 9500,
            "prompt": "", "model": "opus",
        })
    assert r2["ok"] is True
    assert relay._session_meta.model == "opus"


async def test_multiturn_long_50turns_no_overflow() -> None:
    """50-turn conversation stays within MAX_BUF ring buffer without crash."""
    relay = fresh_relay()
    relay._session = mock_sess()

    for i in range(50):
        await relay._cmd_send({"line": f"turn {i}"})
        relay._enqueue(f'{{"type":"assistant","seq":{i}}}')

    # buffer shouldn't exceed maxlen, dropped counter may be nonzero
    assert len(relay._buf) <= MAX_BUF

    stat = await relay._cmd_status({})
    assert stat["ok"] is True
    # dropped is tracked in status data
    assert "dropped=" in stat["data"]


async def test_multiturn_error_recovery_restart_at_turn3() -> None:
    """Session dies at turn 3, relay restarts cleanly for next turns."""
    relay = fresh_relay()
    mock_b = MagicMock()
    mock_b.name = "kimi"
    mock_b.binary = "kimi"
    mock_b.has_resume = False
    mock_b.reads_stdin = True  # explicit: testing stdin-write failure path
    mock_b.resolve_binary = AsyncMock(return_value="/bin/kimi")
    mock_b.build_args.return_value = (["-p", "msg"], {}, [])

    with patch.dict(BACKENDS, {"kimi": mock_b}, clear=False), \
         patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=make_proc(pid=30))):
        await relay._cmd_start({
            "backend": "kimi", "mode": "ask", "mcp_port": 9500, "prompt": "hello",
        })
        relay._session = mock_sess(pid=30)

        for t in range(3):
            rs = await relay._cmd_send({"line": f"msg {t}"})
            assert rs["ok"] is True

        # simulate process death
        relay._session = mock_sess(alive=False, exit_code=1)
        relay._session.write_line = AsyncMock(side_effect=RuntimeError("process dead (exit=1)"))
        rs = await relay._cmd_send({"line": "turn 3 fails"})
        assert rs["ok"] is False

    # restart
    with patch.dict(BACKENDS, {"kimi": mock_b}, clear=False), \
         patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
               AsyncMock(return_value=make_proc(pid=31))):
        r2 = await relay._cmd_start({
            "backend": "kimi", "mode": "ask", "mcp_port": 9500, "prompt": "hello",
        })
        assert r2["ok"] is True
        relay._session = mock_sess(pid=31)

        rs = await relay._cmd_send({"line": "turn 4 after recovery"})
        assert rs["ok"] is True


async def test_multiturn_domain_reload_disconnect_reconnect() -> None:
    """Buffer survives disconnect; reconnecting client sees buffered events."""
    relay = fresh_relay()
    srv   = await asyncio.start_server(relay._handle_client, "127.0.0.1", 0)
    port  = srv.sockets[0].getsockname()[1]

    # inject buffered lines simulating output before disconnect
    for i in range(5):
        relay._enqueue(f'{{"type":"assistant","idx":{i}}}')

    try:
        # first "connection": read events after seq -1
        r, w = await asyncio.wait_for(asyncio.open_connection("127.0.0.1", port), timeout=3)
        req = json.dumps({"id": "1", "cmd": "events", "args": {"after_seq": -1}}).encode()
        w.write(struct.pack("!I", len(req)) + req)
        await w.drain()
        hdr  = await asyncio.wait_for(r.readexactly(4), timeout=3)
        body = await asyncio.wait_for(r.readexactly(struct.unpack("!I", hdr)[0]), timeout=3)
        resp = json.loads(body)
        w.close()
        await w.wait_closed()

        assert resp["ok"] is True
        # all 5 buffered events visible after reconnect
        for i in range(5):
            assert f'"idx":{i}' in resp["data"]

        # second connection (simulated reconnect after reload)
        r2, w2 = await asyncio.wait_for(asyncio.open_connection("127.0.0.1", port), timeout=3)
        req2 = json.dumps({"id": "2", "cmd": "events", "args": {"after_seq": 2}}).encode()
        w2.write(struct.pack("!I", len(req2)) + req2)
        await w2.drain()
        hdr2  = await asyncio.wait_for(r2.readexactly(4), timeout=3)
        body2 = await asyncio.wait_for(r2.readexactly(struct.unpack("!I", hdr2)[0]), timeout=3)
        resp2 = json.loads(body2)
        w2.close()
        await w2.wait_closed()

        assert resp2["ok"] is True
        # only events after seq 2 (idx 3 and 4)
        assert '"idx":3' in resp2["data"]
        assert '"idx":4' in resp2["data"]
        assert '"idx":2' not in resp2["data"]
    finally:
        srv.close()
        await srv.wait_closed()
        await relay._kill_current()


async def test_multiturn_empty_turns() -> None:
    """Empty string turns don't crash relay."""
    relay = fresh_relay()
    relay._session = mock_sess()

    for _ in range(5):
        rs = await relay._cmd_send({"line": ""})
        assert rs["ok"] is True


async def test_multiturn_very_long_turn_10kb() -> None:
    """10KB user message is forwarded without truncation."""
    relay = fresh_relay()
    relay._session = mock_sess()

    big_msg = "x" * 10_000
    line = json.dumps({"type": "user", "content": big_msg})
    rs = await relay._cmd_send({"line": line})
    assert rs["ok"] is True
    relay._session.write_line.assert_called_once_with(line)


async def test_multiturn_whitespace_only_turns() -> None:
    """Whitespace-only turns forwarded as-is."""
    relay = fresh_relay()
    relay._session = mock_sess()

    for ws in ["   ", "\t", "\n", "\r\n", "  \t  \n"]:
        rs = await relay._cmd_send({"line": ws})
        assert rs["ok"] is True

    assert relay._session.write_line.call_count == 5


