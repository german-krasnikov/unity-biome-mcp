"""Crash/disconnect logger tests — TDD Red phase."""
import json
import time
import asyncio
import pytest
from pathlib import Path
from unittest.mock import MagicMock, patch, AsyncMock

from unity_mcp.crash_log import CrashLogger
from unity_mcp.bridge import UnityBridge


# ── Group 1: CrashLogger unit tests ──────────────────────────────────────────

def test_creates_log_dir_and_file(tmp_path):
    log_dir = tmp_path / "crash_logs"
    assert not log_dir.exists()
    CrashLogger(log_dir=log_dir)
    assert log_dir.exists()
    assert (log_dir / "crash.jsonl").exists()


def test_log_disconnect_writes_jsonl(tmp_path):
    logger = CrashLogger(log_dir=tmp_path)
    logger.log_disconnect(cmd="set_property", retry=0, error_type="ConnectionError",
                          unity_busy=False, port=9500)
    lines = (tmp_path / "crash.jsonl").read_text(encoding="utf-8").strip().splitlines()
    assert len(lines) == 1
    entry = json.loads(lines[0])
    assert entry["ev"] == "disconnect"
    assert entry["cmd"] == "set_property"
    assert entry["retry"] == 0
    assert entry["err"] == "ConnectionError"
    assert entry["busy"] is False
    assert entry["port"] == 9500


def test_log_reconnect_writes_jsonl(tmp_path):
    logger = CrashLogger(log_dir=tmp_path)
    logger.log_reconnect(outage_s=6.66, retries=2, port=9500)
    entry = json.loads((tmp_path / "crash.jsonl").read_text(encoding="utf-8").strip())
    assert entry["ev"] == "reconnect"
    assert abs(entry["outage_s"] - 6.66) < 0.01
    assert entry["retries"] == 2
    assert entry["port"] == 9500


def test_entries_have_timestamp(tmp_path):
    before = time.time()
    logger = CrashLogger(log_dir=tmp_path)
    logger.log_disconnect(cmd="x", retry=0, error_type="E", unity_busy=False, port=9500)
    after = time.time()
    entry = json.loads((tmp_path / "crash.jsonl").read_text(encoding="utf-8").strip())
    assert before - 1 <= entry["t"] <= after + 1


def test_rotation_truncates_on_init(tmp_path):
    log_file = tmp_path / "crash.jsonl"
    # Write 600 lines
    lines = [json.dumps({"ev": "disconnect", "i": i}) for i in range(600)]
    log_file.write_text("\n".join(lines) + "\n", encoding="utf-8")

    logger = CrashLogger(log_dir=tmp_path, max_entries=500)
    count = len(log_file.read_text(encoding="utf-8").strip().splitlines())
    assert count == 250  # max_entries // 2


def test_write_rotation_uses_instance_max_entries(tmp_path):
    """_write()'s over-MAX_BYTES rotation must call _rotate(self._max_entries),
    not a hardcoded 500 (regression: literal 500 in _write() ignored a caller's
    custom max_entries — a small-cap logger would silently keep up to 500 entries)."""
    logger = CrashLogger(log_dir=tmp_path, max_entries=7)
    logger.MAX_BYTES = 0  # any existing file content counts as "over budget"
    logger.log_disconnect(cmd="prime", retry=0, error_type="E", unity_busy=False, port=9500)

    calls = []
    logger._rotate = lambda n: calls.append(n)
    logger.log_disconnect(cmd="trigger", retry=0, error_type="E", unity_busy=False, port=9500)

    assert calls == [7]


def test_no_rotation_under_limit(tmp_path):
    log_file = tmp_path / "crash.jsonl"
    lines = [json.dumps({"ev": "disconnect", "i": i}) for i in range(100)]
    log_file.write_text("\n".join(lines) + "\n", encoding="utf-8")

    CrashLogger(log_dir=tmp_path, max_entries=500)
    count = len(log_file.read_text(encoding="utf-8").strip().splitlines())
    assert count == 100


# no-assert: crash guard
def test_close_idempotent(tmp_path):
    """Verifies CrashLogger.close() can be called multiple times without raising."""
    logger = CrashLogger(log_dir=tmp_path)
    logger.close()
    logger.close()  # must not raise


# no-assert: crash guard
def test_write_after_close_no_crash(tmp_path):
    """Verifies CrashLogger methods silently drop writes after close()."""
    logger = CrashLogger(log_dir=tmp_path)
    logger.close()
    # All methods must silently drop — no exception
    logger.log_disconnect(cmd="x", retry=0, error_type="E", unity_busy=False, port=9500)
    logger.log_reconnect(outage_s=1.0, retries=1, port=9500)


# ── Group 2: Bridge integration ───────────────────────────────────────────────

def test_bridge_creates_crash_logger():
    bridge = UnityBridge("127.0.0.1", 9999)
    from unity_mcp.crash_log import CrashLogger
    assert isinstance(bridge._crash_log, CrashLogger)


async def test_disconnect_logs_to_crash_log():
    bridge = UnityBridge("127.0.0.1", 9999)
    mock_log = MagicMock()
    bridge._crash_log = mock_log

    # simulate send failing with ConnectionRefusedError immediately
    with patch("asyncio.open_connection", side_effect=ConnectionRefusedError("refused")):
        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("ping", {})

    assert mock_log.log_disconnect.called
    call_kwargs = mock_log.log_disconnect.call_args.kwargs
    assert call_kwargs["cmd"] == "ping"
    assert call_kwargs["error_type"] == "ConnectionRefusedError"
    assert call_kwargs["port"] == 9999


# ── Group 3: log_crash() module-level function ────────────────────────────────

from unity_mcp.crash_log import log_crash


def test_log_crash_writes_ev_crash(tmp_path):
    """T1: log_crash writes ev=crash entry to crash.jsonl."""
    exc = RuntimeError("boom")
    log_crash(exc, log_dir=tmp_path)
    lines = (tmp_path / "crash.jsonl").read_text(encoding="utf-8").strip().splitlines()
    assert len(lines) == 1
    entry = json.loads(lines[0])
    assert entry["ev"] == "crash"
    assert entry["exc"] == "RuntimeError"
    assert entry["msg"] == "boom"


def test_log_crash_includes_traceback(tmp_path):
    """T2: log_crash writes tb field with traceback string."""
    try:
        raise ValueError("trace me")
    except ValueError as exc:
        log_crash(exc, log_dir=tmp_path)
    entry = json.loads((tmp_path / "crash.jsonl").read_text(encoding="utf-8").strip())
    assert "tb" in entry
    assert "ValueError" in entry["tb"]


def test_log_crash_includes_timestamp(tmp_path):
    """T3: log_crash writes numeric t field close to now."""
    before = time.time()
    log_crash(RuntimeError("ts"), log_dir=tmp_path)
    after = time.time()
    entry = json.loads((tmp_path / "crash.jsonl").read_text(encoding="utf-8").strip())
    assert before - 1 <= entry["t"] <= after + 1


def test_log_crash_creates_dir_if_missing(tmp_path):
    """T4: log_crash creates missing parent dir."""
    log_dir = tmp_path / "new" / "nested"
    assert not log_dir.exists()
    log_crash(RuntimeError("dir"), log_dir=log_dir)
    assert (log_dir / "crash.jsonl").exists()


# no-assert: crash guard
def test_log_crash_silent_on_permission_error(tmp_path):
    """T5: log_crash silently no-ops when write fails."""
    with patch("pathlib.Path.open", side_effect=PermissionError("denied")):
        log_crash(RuntimeError("nope"), log_dir=tmp_path)  # must not raise


def test_log_crash_appends_preserves_existing(tmp_path):
    """T6: log_crash appends — existing entries preserved."""
    log_crash(RuntimeError("first"), log_dir=tmp_path)
    log_crash(RuntimeError("second"), log_dir=tmp_path)
    lines = (tmp_path / "crash.jsonl").read_text(encoding="utf-8").strip().splitlines()
    assert len(lines) == 2
    assert json.loads(lines[0])["msg"] == "first"
    assert json.loads(lines[1])["msg"] == "second"
