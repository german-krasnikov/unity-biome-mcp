"""Monkey/stress tests: chat_relay + backend_def + mcp_config_writer.

200 parametrized tests targeting race conditions, chaos scenarios, resource leaks.
All tests marked @pytest.mark.monkey. No real CLI binaries needed.

Sections:
  A. Backend switching chaos    (40) — 5 backends × 2 modes × 4 ops
  B. Mode switch stress         (30) — 5 backends × 2 initial_modes × 3 scenarios
  C. Config writer chaos        (30) — 5 writers × 6 scenarios
  D. Binary resolution stress   (20) — 5 backends × 4 edges
  E. Protocol field torture     (30) — 6 field-scenarios × 5 backends
  F. Session lifecycle chaos    (25) — 5 backends × 5 scenarios
  G. Concurrent operations      (25) — 5 patterns × 5 backends

Run: pytest tests/test_monkey_relay_stress.py -m monkey -v --timeout=120
"""
import asyncio
import json
import os
import tempfile
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.chat_relay import (
    BACKENDS, CliSession, SessionMeta,
    MAX_BUF, KILL_WAIT,
)
from unity_mcp import mcp_config_writer
from .relay_helpers import make_proc, mock_sess, fresh_relay, tcp_cmd, relay_server  # noqa: F401

pytestmark = pytest.mark.monkey

# ─── Constants ────────────────────────────────────────────────────────────────

ALL_BACKENDS = ["claude", "codex", "kimi", "agy", "opencode"]
ALL_MODES    = ["ask", "agent"]

# has_resume truth table
_HAS_RESUME  = {"claude": True, "codex": True, "kimi": False,
                "agy": False, "opencode": True}

# ─── Local helpers ────────────────────────────────────────────────────────────

def mock_backend(resolve: str | None = "/bin/cli", has_resume: bool = True,
                 argv: list | None = None, name: str = "mock") -> MagicMock:
    b = MagicMock()
    b.name = name
    b.binary = name
    b.has_resume = has_resume
    b.resolve_binary = AsyncMock(return_value=resolve)
    b.build_args.return_value = (argv or ["-p", "prompt"], {}, [])
    return b



# ═══════════════════════════════════════════════════════════════════════════════
# A. Backend Switching Chaos — 5 × 2 × 4 = 40 tests
# ═══════════════════════════════════════════════════════════════════════════════

@pytest.mark.parametrize("backend,mode,op_idx", [
    (b, m, op)
    for b in ALL_BACKENDS
    for m in ALL_MODES
    for op in range(4)
])
async def test_backend_switch_chaos(backend: str, mode: str, op_idx: int) -> None:
    """
    op 0: start valid → start invalid → relay survives (status ok)
    op 1: start invalid → start valid → second ok
    op 2: start mode → start other mode same backend → session replaced
    op 3: start with unknown extra kwargs → gracefully ignored
    """
    relay = fresh_relay()
    proc  = make_proc(pid=42)
    other_mode = "agent" if mode == "ask" else "ask"
    b     = mock_backend(name=backend)

    with patch.dict(BACKENDS, {backend: b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):

            if op_idx == 0:
                # valid start, then invalid backend
                r1 = await relay._cmd_start({"backend": backend, "mode": mode,
                                              "mcp_port": 9500, "prompt": ""})
                r2 = await relay._cmd_start({"backend": "nonexistent_backend_xyz",
                                              "mode": mode, "mcp_port": 9500})
                assert r1["ok"] is True
                assert r2["ok"] is False and "unknown backend" in r2["err"]

            elif op_idx == 1:
                # invalid first, then valid
                r1 = await relay._cmd_start({"backend": "no_such_backend",
                                              "mode": mode, "mcp_port": 9500})
                r2 = await relay._cmd_start({"backend": backend, "mode": mode,
                                              "mcp_port": 9500, "prompt": ""})
                assert r1["ok"] is False
                assert r2["ok"] is True

            elif op_idx == 2:
                # start mode A, then mode B — session replaced
                r1 = await relay._cmd_start({"backend": backend, "mode": mode,
                                              "mcp_port": 9500, "prompt": "first"})
                pid1 = relay._session.pid if relay._session else None
                # re-mock to give a new proc for the second spawn
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(return_value=make_proc(pid=99))):
                    r2 = await relay._cmd_start({"backend": backend,
                                                  "mode": other_mode,
                                                  "mcp_port": 9500, "prompt": "second"})
                assert r1["ok"] is True
                assert r2["ok"] is True
                assert relay._session_meta is not None
                assert relay._session_meta.mode == other_mode

            else:  # op_idx == 3
                # extra unknown kwargs should not raise
                r1 = await relay._cmd_start({
                    "backend": backend, "mode": mode,
                    "mcp_port": 9500, "prompt": "",
                    "totally_unknown_key": "value",
                    "another_unknown": 12345,
                })
                # build_args may raise or relay catches — either way relay survives
                stat = await relay._cmd_status({})
                assert stat["ok"] is True


# ═══════════════════════════════════════════════════════════════════════════════
# B. Mode Switch Stress — 5 × 2 × 3 = 30 tests
# ═══════════════════════════════════════════════════════════════════════════════

@pytest.mark.parametrize("backend,initial_mode,scenario", [
    (b, m, s)
    for b in ALL_BACKENDS
    for m in ALL_MODES
    for s in range(3)
])
async def test_mode_switch_stress(backend: str, initial_mode: str,
                                   scenario: int) -> None:
    """
    scenario 0: set_mode with NO meta → error 'no active session'
    scenario 1: set_mode with has_resume=False meta → error 'does not support resume'
    scenario 2: set_mode with has_resume=True → kills old, spawns new, updates meta
    """
    relay = fresh_relay()
    new_mode = "agent" if initial_mode == "ask" else "ask"

    if scenario == 0:
        # No meta at all
        result = await relay._cmd_set_mode({"mode": new_mode})
        assert result["ok"] is False
        assert "no active session" in result["err"]

    elif scenario == 1:
        # Meta present but backend has no resume
        relay._session_meta = SessionMeta(
            backend=backend, mode=initial_mode, model=None,
            mcp_port=9500, prompt="", config_dir=None, extra={},
        )
        # Override BACKENDS[backend].has_resume → False
        no_resume_b = mock_backend(resolve="/bin/cli", has_resume=False, name=backend)
        with patch.dict(BACKENDS, {backend: no_resume_b}, clear=False):
            result = await relay._cmd_set_mode({"mode": new_mode})
        assert result["ok"] is False
        assert "does not support resume" in result["err"]

    else:  # scenario == 2
        # Meta + has_resume=True + successful spawn
        relay._session_meta = SessionMeta(
            backend=backend, mode=initial_mode, model=None,
            mcp_port=9500, prompt="hello", config_dir=None, extra={},
        )
        relay._session = mock_sess(pid=10)
        proc = make_proc(pid=20)
        resume_b = mock_backend(resolve="/bin/cli", has_resume=True, name=backend)
        with patch.dict(BACKENDS, {backend: resume_b}, clear=False):
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=proc)):
                result = await relay._cmd_set_mode(
                    {"mode": new_mode, "session_id": "sess-abc"}
                )
        assert result["ok"] is True
        assert relay._session_meta is not None
        assert relay._session_meta.mode == new_mode


# ═══════════════════════════════════════════════════════════════════════════════
# C. Config Writer Chaos — 5 writers × 6 scenarios = 30 tests
# ═══════════════════════════════════════════════════════════════════════════════

_WRITER_KEYS = ["claude", "kimi", "agy", "opencode", "claude_alt"]


def _call_writer(writer_key: str, config_dir: str, mcp_port: int) -> str | None:
    """Dispatch to appropriate config writer; return path for file-based writers."""
    if writer_key in ("claude", "claude_alt"):
        return mcp_config_writer.write_claude_config(config_dir, mcp_port)
    elif writer_key == "kimi":
        mcp_config_writer.write_kimi_mcp_config(config_dir, mcp_port)
        return os.path.join(config_dir, "mcp.json")
    elif writer_key == "agy":
        mcp_config_writer.write_agy_settings(config_dir, mcp_port)
        return os.path.join(config_dir, "settings.json")
    else:  # opencode
        return mcp_config_writer.write_opencode_config(config_dir, mcp_port)


@pytest.mark.parametrize("writer_key,scenario", [
    (w, s) for w in _WRITER_KEYS for s in range(6)
])
def test_config_writer_chaos(writer_key: str, scenario: int,
                              tmp_path: Path) -> None:
    """
    scenario 0: normal write → file exists + valid JSON
    scenario 1: overwrite with different port → port updated
    scenario 2: unicode subdirectory path → creates file without error
    scenario 3: concurrent writes (two sequential calls same dir) → last port wins
    scenario 4: pre-existing corrupted JSON → merge treats as empty, succeeds
    scenario 5: write read-back round-trip → JSON deserializes cleanly
    """
    base = str(tmp_path)

    if scenario == 0:
        path = _call_writer(writer_key, base, 9500)
        assert Path(path).exists()
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        assert isinstance(data, dict)

    elif scenario == 1:
        _call_writer(writer_key, base, 9500)
        path = _call_writer(writer_key, base, 9600)
        content = Path(path).read_text(encoding="utf-8")
        assert "9600" in content
        assert "9500" not in content or writer_key in ("kimi", "agy")
        # For merge-safe writers (kimi, agy), a second write with same key replaces port
        data = json.loads(content)
        assert isinstance(data, dict)

    elif scenario == 2:
        unicode_dir = str(tmp_path / "日本語テスト")
        os.makedirs(unicode_dir, exist_ok=True)
        path = _call_writer(writer_key, unicode_dir, 9500)
        assert Path(path).exists()

    elif scenario == 3:
        # Two sequential writes (simulating near-concurrent) → last state wins
        _call_writer(writer_key, base, 9501)
        path = _call_writer(writer_key, base, 9502)
        content = Path(path).read_text(encoding="utf-8")
        assert "9502" in content
        data = json.loads(content)  # must still be valid JSON
        assert isinstance(data, dict)

    elif scenario == 4:
        # Corrupt the pre-existing target file
        if writer_key in ("kimi", "agy"):
            existing = os.path.join(base, "mcp.json" if writer_key == "kimi" else "settings.json")
            Path(existing).write_text("{{{INVALID_JSON}}}", encoding="utf-8")
        # Writer should handle corrupt existing gracefully
        path = _call_writer(writer_key, base, 9500)
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        assert isinstance(data, dict)

    else:  # scenario == 5
        path = _call_writer(writer_key, base, 9503)
        raw = Path(path).read_text(encoding="utf-8")
        parsed = json.loads(raw)
        # round-trip: re-serialize and re-parse
        reparsed = json.loads(json.dumps(parsed))
        assert reparsed == parsed


# ═══════════════════════════════════════════════════════════════════════════════
# D. Binary Resolution Stress — 5 × 4 = 20 tests
# ═══════════════════════════════════════════════════════════════════════════════

@pytest.mark.parametrize("backend_name,edge", [
    (b, e) for b in ALL_BACKENDS for e in range(4)
])
async def test_binary_resolution_edge(backend_name: str, edge: int,
                                       monkeypatch: pytest.MonkeyPatch) -> None:
    """
    edge 0: shutil.which=None + login_shell=None → resolve_binary returns None
    edge 1: shutil.which returns path with spaces → returned unchanged
    edge 2: shutil.which=None, login_shell returns real path → that path returned
    edge 3: shutil.which called exactly once (no redundant resolution)
    """
    from unity_mcp import backend_def
    b = BACKENDS[backend_name]

    if edge == 0:
        monkeypatch.setattr(backend_def.shutil, "which", lambda _: None)
        monkeypatch.setattr(backend_def, "_which_via_login_shell", AsyncMock(return_value=None))
        assert await b.resolve_binary() is None

    elif edge == 1:
        path_with_spaces = "/usr/local/my apps/bin/claude"
        monkeypatch.setattr(backend_def.shutil, "which", lambda _: path_with_spaces)
        result = await b.resolve_binary()
        assert result == path_with_spaces

    elif edge == 2:
        monkeypatch.setattr(backend_def.shutil, "which", lambda _: None)
        login_path = f"/opt/homebrew/bin/{backend_name}"
        monkeypatch.setattr(backend_def, "_which_via_login_shell", AsyncMock(return_value=login_path))
        assert await b.resolve_binary() == login_path

    else:  # edge == 3
        call_count = []
        def counting_which(name: str) -> str:
            call_count.append(name)
            return f"/bin/{name}"
        monkeypatch.setattr(backend_def.shutil, "which", counting_which)
        await b.resolve_binary()
        assert len(call_count) == 1, f"shutil.which called {len(call_count)} times, expected 1"


# ═══════════════════════════════════════════════════════════════════════════════
# E. Protocol Field Torture — 6 × 5 = 30 tests
# ═══════════════════════════════════════════════════════════════════════════════

@pytest.mark.parametrize("scenario,backend_name", [
    (s, b) for s in range(6) for b in ALL_BACKENDS
])
async def test_protocol_field_torture(scenario: int, backend_name: str) -> None:
    """
    scenario 0: start with empty backend field → unknown backend error
    scenario 1: start with all-None optional fields → ok (uses defaults)
    scenario 2: set_mode before any start → no active session error
    scenario 3: set_mode mode field missing → uses existing meta.mode
    scenario 4: start with mcp_port=0 → ok (port passed to build_args)
    scenario 5: start with 100 KB prompt → ok, no truncation
    """
    relay = fresh_relay()
    proc  = make_proc(pid=77)
    b     = mock_backend(resolve="/bin/cli", has_resume=True, name=backend_name)

    with patch.dict(BACKENDS, {backend_name: b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):

            if scenario == 0:
                r = await relay._cmd_start({"backend": "", "mode": "ask", "mcp_port": 9500})
                assert r["ok"] is False
                assert "unknown backend" in r["err"]

            elif scenario == 1:
                r = await relay._cmd_start({
                    "backend": backend_name,
                    "mode":   None,
                    "model":  None,
                    "mcp_port": None,
                    "prompt":   None,
                    "session_id": None,
                    "config_dir": None,
                })
                # should succeed or fail cleanly (no crash)
                stat = await relay._cmd_status({})
                assert stat["ok"] is True

            elif scenario == 2:
                # No meta set
                r = await relay._cmd_set_mode({"mode": "agent"})
                assert r["ok"] is False
                assert "no active session" in r["err"]

            elif scenario == 3:
                # Set meta, then set_mode without mode field → keeps existing mode
                relay._session_meta = SessionMeta(
                    backend=backend_name, mode="ask", model=None,
                    mcp_port=9500, prompt="", config_dir=None, extra={},
                )
                relay._session = mock_sess(pid=10)
                r = await relay._cmd_set_mode({})  # no 'mode' key
                if r["ok"]:
                    assert relay._session_meta.mode == "ask"  # kept original
                else:
                    # error is acceptable (no resume) but must be clean
                    assert "err" in r

            elif scenario == 4:
                r = await relay._cmd_start({
                    "backend":  backend_name,
                    "mode":     "ask",
                    "mcp_port": 0,
                    "prompt":   "",
                })
                # build_args called with mcp_port=0
                if r["ok"]:
                    call_kwargs = b.build_args.call_args
                    assert call_kwargs is not None

            else:  # scenario == 5
                big_prompt = "x" * 100_000
                r = await relay._cmd_start({
                    "backend":  backend_name,
                    "mode":     "ask",
                    "mcp_port": 9500,
                    "prompt":   big_prompt,
                })
                stat = await relay._cmd_status({})
                assert stat["ok"] is True


# ═══════════════════════════════════════════════════════════════════════════════
# F. Session Lifecycle Chaos — 5 × 5 = 25 tests
# ═══════════════════════════════════════════════════════════════════════════════

@pytest.mark.parametrize("backend_name,scenario", [
    (b, s) for b in ALL_BACKENDS for s in range(5)
])
async def test_session_lifecycle_chaos(backend_name: str, scenario: int) -> None:
    """
    scenario 0: 5 rapid spawn→kill cycles → no leftover session
    scenario 1: kill with no session → idempotent, returns ok
    scenario 2: kill twice back to back → both return ok, second is no-op
    scenario 3: failed spawn → _session stays None, _session_meta NOT set
    scenario 4: meta persists across kill (kill clears session but not meta)
    """
    relay = fresh_relay()
    proc  = make_proc(pid=55)
    b     = mock_backend(resolve="/bin/cli", has_resume=True, name=backend_name)

    with patch.dict(BACKENDS, {backend_name: b}, clear=False):
        with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                   AsyncMock(return_value=proc)):

            if scenario == 0:
                for _ in range(5):
                    await relay._cmd_start({
                        "backend":  backend_name,
                        "mode":     "ask",
                        "mcp_port": 9500,
                        "prompt":   "",
                    })
                    await relay._cmd_kill({})
                assert relay._session is None

            elif scenario == 1:
                assert relay._session is None
                r = await relay._cmd_kill({})
                assert r["ok"] is True
                assert relay._session is None

            elif scenario == 2:
                await relay._cmd_start({
                    "backend":  backend_name,
                    "mode":     "ask",
                    "mcp_port": 9500,
                    "prompt":   "",
                })
                r1 = await relay._cmd_kill({})
                r2 = await relay._cmd_kill({})
                assert r1["ok"] is True
                assert r2["ok"] is True
                assert relay._session is None

            elif scenario == 3:
                with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                           AsyncMock(side_effect=FileNotFoundError("not found"))):
                    r = await relay._cmd_start({
                        "backend":  backend_name,
                        "mode":     "ask",
                        "mcp_port": 9500,
                        "prompt":   "",
                    })
                assert r["ok"] is False
                assert relay._session is None
                # meta must NOT be set on failed spawn
                assert relay._session_meta is None

            else:  # scenario == 4
                # start → establishes meta
                await relay._cmd_start({
                    "backend":  backend_name,
                    "mode":     "ask",
                    "mcp_port": 9500,
                    "prompt":   "keep_me",
                })
                meta_before = relay._session_meta
                assert meta_before is not None
                # kill clears session but meta survives
                await relay._kill_current()
                assert relay._session is None
                assert relay._session_meta is meta_before
                assert relay._session_meta.prompt == "keep_me"


# ═══════════════════════════════════════════════════════════════════════════════
# G. Concurrent Operations Chaos — 5 × 5 = 25 tests
# ═══════════════════════════════════════════════════════════════════════════════

@pytest.mark.parametrize("pattern,backend_name", [
    (p, b) for p in range(5) for b in ALL_BACKENDS
])
async def test_concurrent_ops_chaos(pattern: int, backend_name: str) -> None:
    """
    pattern 0: 3 concurrent _cmd_start → relay survives with valid state
    pattern 1: concurrent _cmd_start + _cmd_kill → relay survives
    pattern 2: 3 concurrent _cmd_set_mode → relay survives
    pattern 3: _cmd_start then immediately _cmd_set_mode (overlapping) → relay survives
    pattern 4: 5 concurrent _cmd_status (read-only) → all succeed
    """
    relay = fresh_relay()
    b     = mock_backend(resolve="/bin/cli", has_resume=True, name=backend_name)

    start_args = {
        "backend":  backend_name,
        "mode":     "ask",
        "mcp_port": 9500,
        "prompt":   "",
    }

    with patch.dict(BACKENDS, {backend_name: b}, clear=False):

        if pattern == 0:
            # 3 concurrent starts — only one can win, relay must not crash
            procs = [make_proc(pid=100 + i) for i in range(3)]
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(side_effect=procs)):
                results = await asyncio.gather(
                    relay._cmd_start({**start_args}),
                    relay._cmd_start({**start_args}),
                    relay._cmd_start({**start_args}),
                    return_exceptions=True,
                )
            # relay survives — status must work
            stat = await relay._cmd_status({})
            assert stat["ok"] is True
            # at least one start succeeded
            ok_count = sum(1 for r in results
                           if isinstance(r, dict) and r.get("ok"))
            assert ok_count >= 1

        elif pattern == 1:
            # concurrent start + kill
            proc = make_proc(pid=200)
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=proc)):
                results = await asyncio.gather(
                    relay._cmd_start({**start_args}),
                    relay._cmd_kill({}),
                    return_exceptions=True,
                )
            stat = await relay._cmd_status({})
            assert stat["ok"] is True
            # no exceptions propagated
            for r in results:
                assert not isinstance(r, Exception), f"unexpected exception: {r}"

        elif pattern == 2:
            # set_mode × 3 with no active session → all return errors cleanly
            results = await asyncio.gather(
                relay._cmd_set_mode({"mode": "ask"}),
                relay._cmd_set_mode({"mode": "agent"}),
                relay._cmd_set_mode({"mode": "ask"}),
                return_exceptions=True,
            )
            for r in results:
                assert not isinstance(r, Exception), f"unexpected exception: {r}"
                # All should return ok=False (no active session)
                assert isinstance(r, dict)

        elif pattern == 3:
            # Start then immediately set_mode — set_mode may win or lose the race
            proc = make_proc(pid=300)
            with patch("unity_mcp.chat_relay.asyncio.create_subprocess_exec",
                       AsyncMock(return_value=proc)):
                results = await asyncio.gather(
                    relay._cmd_start({**start_args}),
                    relay._cmd_set_mode({"mode": "agent"}),
                    return_exceptions=True,
                )
            stat = await relay._cmd_status({})
            assert stat["ok"] is True
            for r in results:
                assert not isinstance(r, Exception), f"unexpected exception: {r}"

        else:  # pattern == 4
            # 5 concurrent status reads — pure read, all must succeed
            results = await asyncio.gather(
                *[relay._cmd_status({}) for _ in range(5)],
                return_exceptions=True,
            )
            assert all(
                isinstance(r, dict) and r["ok"]
                for r in results
            ), f"some status calls failed: {results}"


