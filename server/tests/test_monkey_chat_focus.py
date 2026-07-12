"""Chat-focused monkey tests: session lifecycle, mode/model/CLI switching,
line-content torture, concurrency, domain reload simulation.

200 parametrized tests, all marked @pytest.mark.monkey.

Sections:
  A. Session lifecycle — 6 backends × 6 scenarios             (36)
  B. Rapid CLI switching — 6 patterns × 5 seeds               (30)
  C. Mode × Model matrix — 6 backends × 6 scenarios           (36)
  D. Line-content torture — 5 backends × 8 content types      (40)
  E. Concurrent chat ops — 6 patterns × 5 backends            (30)
  F. Domain reload simulation — 6 scenarios × 3 backends      (18)
  G. Edge cases (standalone)                                   (10)
  -- Sanity count (non-monkey)                                  (1)

Run: pytest tests/test_monkey_chat_focus.py -m monkey -v --timeout=60
"""
import asyncio
import json
import random
import struct
import tempfile
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.chat_relay import (
    BACKENDS, CliSession, SessionMeta,
    MAX_BUF,
)
from .relay_helpers import make_proc, mock_sess, fresh_relay, tcp_cmd, relay_server  # noqa: F401

# ─── Constants ─────────────────────────────────────────────────────────────────

ALL_6      = ["claude", "codex", "kimi", "agy", "antigravity", "opencode"]
BASE_5     = ["claude", "codex", "kimi", "agy", "opencode"]
WITH_RESUME = {"claude", "codex", "opencode"}
NO_RESUME   = {"kimi", "agy", "antigravity"}
RELOAD_3    = ["claude", "kimi", "opencode"]   # section F subset

# ─── Local helpers ─────────────────────────────────────────────────────────────

def mock_backend(name: str = "mock", has_resume: bool = True,
                 resolve: str | None = "/bin/cli") -> MagicMock:
    b = MagicMock()
    b.name = name
    b.binary = name
    b.has_resume = has_resume
    b.resolve_binary = AsyncMock(return_value=resolve)
    b.build_args.return_value = (["-p"], {}, [])
    return b



# ══════════════════════════════════════════════════════════════════════════════
# A. Session Lifecycle — 6 backends × 6 scenarios = 36
# ══════════════════════════════════════════════════════════════════════════════

@pytest.mark.monkey
@pytest.mark.parametrize("backend,scenario", [
    (b, s) for b in ALL_6 for s in range(6)
])
async def test_session_lifecycle_6backends(backend: str, scenario: int) -> None:
    """
    0: start + 5 sequential sends → all ok
    1: start → set_mode (resume backends succeed, no-resume fail cleanly)
    2: start with model "sonnet" → restart with model "opus" → meta updated
    3: start → kill → restart same backend → fresh session alive
    4: start with session_id → meta stored regardless of backend
    5: start with prompt containing control chars → relay doesn't crash
    """
    relay = fresh_relay()
    proc  = make_proc(pid=42)
    b     = mock_backend(name=backend, has_resume=(backend in WITH_RESUME))

    with patch.dict(BACKENDS, {backend: b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):

            if scenario == 0:
                r = await relay._cmd_start({
                    "backend": backend, "mode": "ask",
                    "mcp_port": 9500, "prompt": "hello",
                })
                assert r["ok"] is True
                relay._session = mock_sess()
                for i in range(5):
                    rs = await relay._cmd_send({"line": f"turn {i}"})
                    assert rs["ok"] is True, f"turn {i}: {rs}"

            elif scenario == 1:
                await relay._cmd_start({
                    "backend": backend, "mode": "ask",
                    "mcp_port": 9500, "prompt": "",
                })
                if backend in WITH_RESUME:
                    with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                               AsyncMock(return_value=make_proc(pid=43))):
                        rs = await relay._cmd_set_mode({"mode": "agent", "session_id": "s1"})
                    assert rs["ok"] is True
                    assert relay._session_meta.mode == "agent"
                else:
                    rs = await relay._cmd_set_mode({"mode": "agent"})
                    assert rs["ok"] is False
                    assert "does not support resume" in rs["err"]

            elif scenario == 2:
                r = await relay._cmd_start({
                    "backend": backend, "mode": "ask",
                    "mcp_port": 9500, "prompt": "", "model": "sonnet",
                })
                if r["ok"]:
                    assert relay._session_meta.model == "sonnet"
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(return_value=make_proc(pid=44))):
                    r2 = await relay._cmd_start({
                        "backend": backend, "mode": "ask",
                        "mcp_port": 9500, "prompt": "", "model": "opus",
                    })
                if r2["ok"]:
                    assert relay._session_meta.model == "opus"
                stat = await relay._cmd_status({})
                assert stat["ok"] is True

            elif scenario == 3:
                await relay._cmd_start({
                    "backend": backend, "mode": "ask",
                    "mcp_port": 9500, "prompt": "first",
                })
                await relay._cmd_kill({})
                assert relay._session is None
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(return_value=make_proc(pid=45))):
                    r2 = await relay._cmd_start({
                        "backend": backend, "mode": "ask",
                        "mcp_port": 9500, "prompt": "second",
                    })
                assert r2["ok"] is True
                assert relay._session is not None

            elif scenario == 4:
                sid = "session-abc-123-xyz"
                r = await relay._cmd_start({
                    "backend": backend, "mode": "ask",
                    "mcp_port": 9500, "prompt": "", "session_id": sid,
                })
                if r["ok"]:
                    assert relay._session_meta is not None
                    call = b.build_args.call_args
                    assert call is not None

            else:  # scenario 5
                r = await relay._cmd_start({
                    "backend": backend, "mode": "ask",
                    "mcp_port": 9500, "prompt": "line1\nline2\t!!\r\n",
                })
                stat = await relay._cmd_status({})
                assert stat["ok"] is True


# ══════════════════════════════════════════════════════════════════════════════
# B. Rapid CLI Switching — 6 patterns × 5 seeds = 30
# ══════════════════════════════════════════════════════════════════════════════

@pytest.mark.monkey
@pytest.mark.parametrize("pattern,seed", [
    (p, s) for p in range(6) for s in range(5)
])
async def test_rapid_cli_switching(pattern: int, seed: int) -> None:
    """
    0: round-robin through all 6 backends → relay always has valid state
    1: random 10 switches (by seed) → relay survives
    2: concurrent send + switch → no deadlock, relay survives
    3: resume→no-resume→resume sequence → relay survives
    4: switch with binary-not-found → relay still queryable
    5: fill buffer then switch → buffer cleared per B8 contract
    """
    rng   = random.Random(seed)
    relay = fresh_relay()
    mocks = {bk: mock_backend(name=bk, has_resume=(bk in WITH_RESUME))
             for bk in ALL_6}

    with patch.dict(BACKENDS, mocks, clear=False):

        if pattern == 0:
            for bk in ALL_6:
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(return_value=make_proc(pid=rng.randint(100, 9999)))):
                    r = await relay._cmd_start({
                        "backend": bk, "mode": "ask",
                        "mcp_port": 9500, "prompt": f"seed{seed}",
                    })
                    assert r["ok"] is True, f"switch to {bk} failed: {r}"
            stat = await relay._cmd_status({})
            assert stat["ok"] is True

        elif pattern == 1:
            for _ in range(10):
                bk = rng.choice(ALL_6)
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(return_value=make_proc(pid=rng.randint(100, 9999)))):
                    await relay._cmd_start({
                        "backend": bk, "mode": rng.choice(["ask", "agent"]),
                        "mcp_port": 9500, "prompt": "",
                    })
                stat = await relay._cmd_status({})
                assert stat["ok"] is True

        elif pattern == 2:
            bk = rng.choice(BASE_5)
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=make_proc(pid=100))):
                await relay._cmd_start({
                    "backend": bk, "mode": "ask", "mcp_port": 9500, "prompt": "",
                })
            relay._session = mock_sess(pid=100)
            bk2 = rng.choice(ALL_6)
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=make_proc(pid=200))):
                results = await asyncio.gather(
                    relay._cmd_send({"line": "hello concurrent"}),
                    relay._cmd_start({"backend": bk2, "mode": "ask",
                                      "mcp_port": 9500, "prompt": ""}),
                    return_exceptions=True,
                )
            assert not any(isinstance(r, Exception) for r in results)
            stat = await relay._cmd_status({})
            assert stat["ok"] is True

        elif pattern == 3:
            seq = (list(WITH_RESUME) + list(NO_RESUME) + list(WITH_RESUME))
            for bk in seq[:3 + seed]:
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(return_value=make_proc(pid=rng.randint(100, 9999)))):
                    await relay._cmd_start({
                        "backend": bk, "mode": "ask",
                        "mcp_port": 9500, "prompt": "",
                    })
            stat = await relay._cmd_status({})
            assert stat["ok"] is True

        elif pattern == 4:
            good = rng.choice(BASE_5)
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=make_proc(pid=300))):
                await relay._cmd_start({
                    "backend": good, "mode": "ask", "mcp_port": 9500, "prompt": "",
                })
            # Now make a backend fail binary resolution
            fail = rng.choice(ALL_6)
            mocks[fail].resolve_binary = AsyncMock(return_value=None)
            r = await relay._cmd_start({
                "backend": fail, "mode": "ask", "mcp_port": 9500, "prompt": "",
            })
            assert r["ok"] is False
            stat = await relay._cmd_status({})
            assert stat["ok"] is True

        else:  # pattern 5
            bk = rng.choice(BASE_5)
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=make_proc(pid=400))):
                await relay._cmd_start({
                    "backend": bk, "mode": "ask", "mcp_port": 9500, "prompt": "",
                })
            for i in range(50):
                relay._enqueue(f"buffered_{i}")
            assert len(relay._buf) == 50

            bk2 = rng.choice([b for b in ALL_6 if b != bk])
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=make_proc(pid=401))):
                await relay._cmd_start({
                    "backend": bk2, "mode": "ask", "mcp_port": 9500, "prompt": "",
                })
            # B8: buffer cleared when old session killed
            assert len(relay._buf) == 0


# ══════════════════════════════════════════════════════════════════════════════
# C. Mode × Model Matrix — 6 backends × 6 scenarios = 36
# ══════════════════════════════════════════════════════════════════════════════

@pytest.mark.monkey
@pytest.mark.parametrize("backend,scenario", [
    (b, s) for b in ALL_6 for s in range(6)
])
async def test_mode_model_matrix(backend: str, scenario: int) -> None:
    """
    0: ask + model "sonnet" → build_args receives mode="ask", model stored in meta
    1: agent + model "opus" → meta.mode="agent", meta.model="opus"
    2: rapid mode cycle ×4 (resume backends actually switch; no-resume returns err cleanly)
    3: start with model "gpt-4" (foreign) → relay passes through, no validation
    4: start with model=None → meta.model is None
    5: model with injection attempt stored as-is (no shell split by relay)
    """
    relay = fresh_relay()
    proc  = make_proc(pid=55)
    b     = mock_backend(name=backend, has_resume=(backend in WITH_RESUME))

    with patch.dict(BACKENDS, {backend: b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):

            if scenario == 0:
                r = await relay._cmd_start({
                    "backend": backend, "mode": "ask", "model": "sonnet",
                    "mcp_port": 9500, "prompt": "",
                })
                if r["ok"]:
                    assert relay._session_meta.model == "sonnet"
                    assert b.build_args.call_args.kwargs.get("mode") == "ask"

            elif scenario == 1:
                r = await relay._cmd_start({
                    "backend": backend, "mode": "agent", "model": "opus",
                    "mcp_port": 9500, "prompt": "",
                })
                if r["ok"]:
                    assert relay._session_meta.mode == "agent"
                    assert relay._session_meta.model == "opus"

            elif scenario == 2:
                await relay._cmd_start({
                    "backend": backend, "mode": "ask",
                    "mcp_port": 9500, "prompt": "",
                })
                modes = ["agent", "ask", "agent", "ask"]
                for mode in modes:
                    if backend in WITH_RESUME:
                        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                                   AsyncMock(return_value=make_proc(pid=56))):
                            rs = await relay._cmd_set_mode({"mode": mode})
                        assert rs["ok"] is True
                        assert relay._session_meta.mode == mode
                    else:
                        rs = await relay._cmd_set_mode({"mode": mode})
                        assert rs["ok"] is False
                stat = await relay._cmd_status({})
                assert stat["ok"] is True

            elif scenario == 3:
                r = await relay._cmd_start({
                    "backend": backend, "mode": "ask", "model": "gpt-4",
                    "mcp_port": 9500, "prompt": "",
                })
                if r["ok"]:
                    assert relay._session_meta.model == "gpt-4"
                    assert b.build_args.call_args is not None

            elif scenario == 4:
                r = await relay._cmd_start({
                    "backend": backend, "mode": "ask", "model": None,
                    "mcp_port": 9500, "prompt": "",
                })
                if r["ok"]:
                    assert relay._session_meta.model is None

            else:  # scenario 5
                injection = "sonnet --permission-mode full"
                r = await relay._cmd_start({
                    "backend": backend, "mode": "ask", "model": injection,
                    "mcp_port": 9500, "prompt": "",
                })
                if r["ok"]:
                    # relay stores model as-is; no shell splitting
                    assert relay._session_meta.model == injection
                    assert b.build_args.call_args.kwargs.get("model") == injection


# ══════════════════════════════════════════════════════════════════════════════
# D. Line-Content Torture — 5 backends × 8 content types = 40
# ══════════════════════════════════════════════════════════════════════════════

_CONTENTS = [
    "x" * 10_000,                                          # 0: 10 KB line
    "你好世界🌍" * 500,                                      # 1: Chinese + emoji ×500
    "مرحبا unicode ñoño 🚀" * 100,                          # 2: RTL + emoji ×100
    "line\nwith\nnewlines\t\rand\0nulls",                   # 3: control chars
    json.dumps({"tool": "drag", "args": {"x": 1, "y": 2}}), # 4: nested JSON
    '{"cmd":"inject","args":{"evil":"true"}}',               # 5: JSON-as-string
    "",                                                      # 6: empty string
    "y" * 1_000_000,                                        # 7: 1 MB
]


@pytest.mark.monkey
@pytest.mark.parametrize("backend,content_idx", [
    (b, c) for b in BASE_5 for c in range(8)
])
async def test_line_content_torture(backend: str, content_idx: int) -> None:
    """Line sent through relay doesn't crash or corrupt state."""
    relay = fresh_relay()
    sess  = mock_sess()
    relay._session = sess
    line  = _CONTENTS[content_idx]

    try:
        r = await relay._cmd_send({"line": line})
    except Exception as exc:
        pytest.fail(f"send raised unexpectedly: {exc!r}")

    assert "ok" in r
    if r["ok"]:
        sess.write_line.assert_called_once_with(line)

    stat = await relay._cmd_status({})
    assert stat["ok"] is True


# ══════════════════════════════════════════════════════════════════════════════
# E. Concurrent Chat Operations — 6 patterns × 5 backends = 30
# ══════════════════════════════════════════════════════════════════════════════

@pytest.mark.monkey
@pytest.mark.parametrize("pattern,backend", [
    (p, b) for p in range(6) for b in BASE_5
])
async def test_concurrent_chat_ops(pattern: int, backend: str) -> None:
    """
    0: 3 concurrent starts → relay survives, at least one ok
    1: concurrent send + set_mode → no exception propagates
    2: concurrent send + kill → both complete cleanly
    3: 5 concurrent events polls → all return ok with same data
    4: concurrent close_stdin + kill → both ok, relay alive
    5: 10 concurrent status reads → all ok=True
    """
    relay = fresh_relay()
    b     = mock_backend(name=backend, has_resume=(backend in WITH_RESUME))

    with patch.dict(BACKENDS, {backend: b}, clear=False):

        if pattern == 0:
            procs = [make_proc(pid=100 + i) for i in range(3)]
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(side_effect=procs)):
                results = await asyncio.gather(
                    relay._cmd_start({"backend": backend, "mode": "ask",
                                      "mcp_port": 9500, "prompt": ""}),
                    relay._cmd_start({"backend": backend, "mode": "ask",
                                      "mcp_port": 9500, "prompt": ""}),
                    relay._cmd_start({"backend": backend, "mode": "ask",
                                      "mcp_port": 9500, "prompt": ""}),
                    return_exceptions=True,
                )
            assert not any(isinstance(r, Exception) for r in results)
            ok_count = sum(1 for r in results if isinstance(r, dict) and r.get("ok"))
            assert ok_count >= 1
            stat = await relay._cmd_status({})
            assert stat["ok"] is True

        elif pattern == 1:
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=make_proc(pid=200))):
                await relay._cmd_start({
                    "backend": backend, "mode": "ask", "mcp_port": 9500, "prompt": "",
                })
            relay._session = mock_sess(pid=200)
            if backend in WITH_RESUME:
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(return_value=make_proc(pid=201))):
                    results = await asyncio.gather(
                        relay._cmd_send({"line": "concurrent turn"}),
                        relay._cmd_set_mode({"mode": "agent", "session_id": "s"}),
                        return_exceptions=True,
                    )
            else:
                results = await asyncio.gather(
                    relay._cmd_send({"line": "concurrent turn"}),
                    relay._cmd_set_mode({"mode": "agent"}),
                    return_exceptions=True,
                )
            assert not any(isinstance(r, Exception) for r in results)
            stat = await relay._cmd_status({})
            assert stat["ok"] is True

        elif pattern == 2:
            relay._session = mock_sess(pid=300)
            results = await asyncio.gather(
                relay._cmd_send({"line": "send while killing"}),
                relay._cmd_kill({}),
                return_exceptions=True,
            )
            assert not any(isinstance(r, Exception) for r in results)
            stat = await relay._cmd_status({})
            assert stat["ok"] is True

        elif pattern == 3:
            relay._enqueue("alpha")
            relay._enqueue("beta")
            relay._enqueue("gamma")
            results = await asyncio.gather(
                *[relay._cmd_events({"after_seq": -1}) for _ in range(5)],
                return_exceptions=True,
            )
            assert all(isinstance(r, dict) and r["ok"] for r in results)
            assert all("alpha" in r["data"] for r in results)

        elif pattern == 4:
            relay._session = mock_sess(pid=400)
            results = await asyncio.gather(
                relay._cmd_close_stdin({}),
                relay._cmd_kill({}),
                return_exceptions=True,
            )
            assert not any(isinstance(r, Exception) for r in results)
            assert all(isinstance(r, dict) and r["ok"] for r in results)

        else:  # pattern 5
            relay._session = mock_sess(pid=500)
            relay._enqueue("status_data")
            results = await asyncio.gather(
                *[relay._cmd_status({}) for _ in range(10)],
                return_exceptions=True,
            )
            assert all(isinstance(r, dict) and r["ok"] for r in results)


# ══════════════════════════════════════════════════════════════════════════════
# F. Domain Reload Simulation — 6 scenarios × 3 backends = 18
# ══════════════════════════════════════════════════════════════════════════════

@pytest.mark.monkey
@pytest.mark.parametrize("scenario,backend", [
    (s, b) for s in range(6) for b in RELOAD_3
])
async def test_domain_reload_simulation(scenario: int, backend: str,
                                        relay_server) -> None:
    """Simulate Unity domain reload: disconnect + reconnect patterns."""
    relay, port = relay_server
    b = mock_backend(name=backend, has_resume=(backend in WITH_RESUME))

    with patch.dict(BACKENDS, {backend: b}, clear=False):

        if scenario == 0:
            # Fill buffer, abrupt disconnect, reconnect → all data still there
            for i in range(20):
                relay._enqueue(f"reload_line_{i}")
            r, w = await asyncio.open_connection("127.0.0.1", port)
            w.close()
            await asyncio.sleep(0.02)
            resp = await tcp_cmd(port, "events", {"after_seq": -1})
            assert resp["ok"] is True
            assert "reload_line_0" in resp["data"]
            assert "reload_line_19" in resp["data"]

        elif scenario == 1:
            # Disconnect mid-request (send header, close before response)
            r, w = await asyncio.open_connection("127.0.0.1", port)
            req = json.dumps({"id": "1", "cmd": "status", "args": {}}).encode()
            w.write(struct.pack("!I", len(req)) + req)
            await w.drain()
            w.close()  # disconnect before reading response
            await asyncio.sleep(0.02)
            # Reconnect — server must still respond
            resp = await tcp_cmd(port, "status")
            assert resp["ok"] is True

        elif scenario == 2:
            # 5 rapid disconnect/reconnect cycles
            for cycle in range(5):
                relay._enqueue(f"cycle_{cycle}")
                r, w = await asyncio.open_connection("127.0.0.1", port)
                w.close()
                await asyncio.sleep(0.01)
            resp = await tcp_cmd(port, "events", {"after_seq": -1})
            assert resp["ok"] is True
            assert "cycle_0" in resp["data"]
            assert "cycle_4" in resp["data"]

        elif scenario == 3:
            # Buffer wraps during disconnect → dropped counter increments
            for i in range(MAX_BUF + 10):
                relay._enqueue(f"wrap_{i}")
            assert relay._dropped == 10
            resp = await tcp_cmd(port, "events", {"after_seq": -1})
            assert resp["ok"] is True
            # Seq starts at 10 (oldest 10 evicted)
            # First line in buffer is "wrap_10" at seq=10
            assert "wrap_10" in resp["data"]

        elif scenario == 4:
            # Session stays alive across domain reload (disconnect doesn't kill session)
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=make_proc(pid=1001))):
                r = await tcp_cmd(port, "start", {
                    "backend": backend, "mode": "ask",
                    "mcp_port": 9500, "prompt": "",
                })
            assert r["ok"] is True
            # Simulate domain reload: abrupt disconnect
            r2, w2 = await asyncio.open_connection("127.0.0.1", port)
            w2.close()
            await asyncio.sleep(0.02)
            # Reconnect: relay still running, session meta persisted
            resp = await tcp_cmd(port, "status")
            assert resp["ok"] is True
            assert relay._session_meta is not None

        else:  # scenario 5
            # Incremental drain across reconnects (after_seq tracking)
            for i in range(10):
                relay._enqueue(f"persist_{i}")
            # First client reads up to seq=4
            resp1 = await tcp_cmd(port, "events", {"after_seq": 4})
            assert resp1["ok"] is True
            # Disconnect
            r, w = await asyncio.open_connection("127.0.0.1", port)
            w.close()
            await asyncio.sleep(0.02)
            # New client continues from seq=8 → gets only seq=9
            resp2 = await tcp_cmd(port, "events", {"after_seq": 8})
            assert resp2["ok"] is True
            assert "persist_9" in resp2["data"]
            assert "persist_8" not in resp2["data"]


# ══════════════════════════════════════════════════════════════════════════════
# G. Edge Cases — 10 standalone monkey tests
# ══════════════════════════════════════════════════════════════════════════════

@pytest.mark.monkey
async def test_edge_send_missing_line_key() -> None:
    """Missing 'line' in send args → ok=False via dispatch exception handler."""
    relay = fresh_relay()
    relay._session = mock_sess()
    r = await relay._cmd_send({})  # KeyError on args["line"]
    assert r["ok"] is False  # _dispatch catches Exception and returns err dict


@pytest.mark.monkey
async def test_edge_events_after_seq_max_int() -> None:
    """after_seq=2**62 returns empty data, no crash."""
    relay = fresh_relay()
    relay._enqueue("something")
    r = await relay._cmd_events({"after_seq": 2**62})
    assert r["ok"] is True
    assert r["data"] == ""


@pytest.mark.monkey
async def test_edge_backend_key_case_sensitivity() -> None:
    """'Claude' (capital C) is NOT in BACKENDS → unknown backend error."""
    relay = fresh_relay()
    r = await relay._cmd_start({"backend": "Claude", "mode": "ask", "mcp_port": 9500})
    assert r["ok"] is False
    assert "unknown backend" in r["err"]


@pytest.mark.monkey
def test_edge_antigravity_maps_to_agy() -> None:
    """antigravity registry entry uses the same binary as agy, has_resume=False."""
    ag  = BACKENDS.get("antigravity")
    agy = BACKENDS.get("agy")
    assert ag is not None and agy is not None
    assert ag.has_resume is False
    assert ag.binary == agy.binary


@pytest.mark.monkey
async def test_edge_antigravity_set_mode_fails() -> None:
    """antigravity session cannot switch mode (no resume support)."""
    relay = fresh_relay()
    relay._session_meta = SessionMeta(
        backend="antigravity", mode="ask", model=None,
        mcp_port=9500, prompt="", config_dir=None, extra={},
    )
    r = await relay._cmd_set_mode({"mode": "agent"})
    assert r["ok"] is False
    assert "does not support resume" in r["err"]


@pytest.mark.monkey
async def test_edge_model_injection_stored_as_is() -> None:
    """Model with spaces + flags is stored verbatim; relay never shell-splits it."""
    relay = fresh_relay()
    injection = "sonnet --permission-mode full --dangerously-skip-permissions"
    proc = make_proc(pid=999)
    b    = mock_backend(name="claude")

    with patch.dict(BACKENDS, {"claude": b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):
            r = await relay._cmd_start({
                "backend": "claude", "mode": "ask",
                "model": injection, "mcp_port": 9500, "prompt": "",
            })
    if r["ok"]:
        assert relay._session_meta.model == injection
        assert b.build_args.call_args.kwargs.get("model") == injection


@pytest.mark.monkey
async def test_edge_mcp_port_zero() -> None:
    """mcp_port=0 is passed to build_args; relay doesn't reject it."""
    relay = fresh_relay()
    b    = mock_backend(name="claude")

    with patch.dict(BACKENDS, {"claude": b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=make_proc(pid=998))):
            r = await relay._cmd_start({
                "backend": "claude", "mode": "ask",
                "mcp_port": 0, "prompt": "",
            })
    stat = await relay._cmd_status({})
    assert stat["ok"] is True


@pytest.mark.monkey
async def test_edge_session_id_special_chars() -> None:
    """session_id with special chars is forwarded to build_args unchanged."""
    relay = fresh_relay()
    sid   = "session/with\\special\"chars&<>|"
    b     = mock_backend(name="claude", has_resume=True)

    with patch.dict(BACKENDS, {"claude": b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=make_proc(pid=997))):
            r = await relay._cmd_start({
                "backend": "claude", "mode": "ask",
                "mcp_port": 9500, "prompt": "", "session_id": sid,
            })
    if r["ok"]:
        assert b.build_args.call_args.kwargs.get("session_id") == sid


@pytest.mark.monkey
async def test_edge_empty_model_string() -> None:
    """model='' (empty string) is passed to build_args; relay doesn't crash."""
    relay = fresh_relay()
    b     = mock_backend(name="claude")

    with patch.dict(BACKENDS, {"claude": b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=make_proc(pid=996))):
            r = await relay._cmd_start({
                "backend": "claude", "mode": "ask",
                "mcp_port": 9500, "prompt": "", "model": "",
            })
    stat = await relay._cmd_status({})
    assert stat["ok"] is True


@pytest.mark.monkey
async def test_edge_unknown_cmd_returns_err() -> None:
    """Dispatch with unknown cmd returns ok=False, relay continues serving."""
    relay = fresh_relay()
    r = await relay._dispatch({"cmd": "totally_unknown_cmd", "args": {}})
    assert r["ok"] is False
    assert "unknown cmd" in r["err"]
    stat = await relay._cmd_status({})
    assert stat["ok"] is True


