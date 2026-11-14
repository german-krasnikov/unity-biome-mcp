"""Tests for doctor.py health diagnostics tool."""
import asyncio
import os
import sys
from pathlib import Path
from unittest.mock import AsyncMock, patch

import pytest

from unity_mcp.doctor import (
    CheckResult,
    USER_MESSAGES,
    check_python_version,
    check_port_file,
    check_lockfile,
    check_tcp_connection,
    check_unity_state,
    run_doctor,
    format_report,
)


@pytest.mark.asyncio
async def test_check_python_version_ok():
    result = await check_python_version()
    assert result.ok
    assert "3." in result.detail


@pytest.mark.asyncio
async def test_check_python_version_name():
    result = await check_python_version()
    assert result.name == "python_version"


@pytest.mark.asyncio
async def test_check_port_file_no_files(tmp_path):
    """No port files → warning (not hard error, Unity may not be running)."""
    with patch("unity_mcp.doctor._ports_dir", lambda: tmp_path):
        result = await check_port_file()
    assert result.ok is False
    assert result.auto_fixable is False  # nothing to fix


@pytest.mark.asyncio
async def test_check_port_file_stale_pid(tmp_path):
    """Port file with non-existent PID → auto_fixable=True."""
    port_file = tmp_path / "99999999.port"
    port_file.write_text("9500\n/some/path", encoding="utf-8")
    with patch("unity_mcp.doctor._ports_dir", lambda: tmp_path):
        result = await check_port_file()
    assert result.auto_fixable is True
    assert result.ok is False


@pytest.mark.asyncio
async def test_check_port_file_live_pid(tmp_path):
    """Port file with own PID → healthy."""
    port_file = tmp_path / f"{os.getpid()}.port"
    port_file.write_text("9500\n/some/path", encoding="utf-8")
    with patch("unity_mcp.doctor._ports_dir", lambda: tmp_path):
        result = await check_port_file()
    assert result.ok is True


@pytest.mark.asyncio
async def test_check_port_file_autofix_deletes_stale(tmp_path):
    """run_doctor(fix=True) removes stale port files but reports no Unity running."""
    port_file = tmp_path / "99999999.port"
    port_file.write_text("9500\n/some/path", encoding="utf-8")
    with patch("unity_mcp.doctor._ports_dir", lambda: tmp_path):
        result = await check_port_file(fix=True)
    assert not port_file.exists()
    # File cleaned but no live Unity → still not ok
    assert result.ok is False
    assert "cleaned" in result.detail.lower()


@pytest.mark.asyncio
async def test_check_port_file_autofix_with_live_pid_ok(tmp_path):
    """run_doctor(fix=True) with stale + live → ok=True after cleanup."""
    stale_file = tmp_path / "99999999.port"
    stale_file.write_text("9500\n/some/path", encoding="utf-8")
    live_file = tmp_path / f"{os.getpid()}.port"
    live_file.write_text("9501\n/some/path", encoding="utf-8")
    with patch("unity_mcp.doctor._ports_dir", lambda: tmp_path):
        result = await check_port_file(fix=True)
    assert not stale_file.exists()
    assert result.ok is True


@pytest.mark.asyncio
async def test_check_lockfile_no_locks(tmp_path):
    """No lock files → ok (nothing to clean)."""
    with patch("unity_mcp.doctor._lock_dir", lambda: tmp_path):
        result = await check_lockfile()
    assert result.ok is True


@pytest.mark.asyncio
async def test_check_lockfile_stale_entry(tmp_path):
    """Stale lockfile (dead PID) → auto_fixable=True."""
    lock_file = tmp_path / "server-9500-99999999.lock"
    lock_file.write_text("99999999\n", encoding="utf-8")
    with patch("unity_mcp.doctor._lock_dir", lambda: tmp_path):
        result = await check_lockfile()
    assert result.auto_fixable is True


@pytest.mark.asyncio
async def test_check_tcp_connection_refused():
    """No server on port → ok=False."""
    result = await check_tcp_connection(port=19999)
    assert result.ok is False
    assert "not running" in result.detail.lower() or "refused" in result.detail.lower() or "connect" in result.detail.lower()


@pytest.mark.asyncio
async def test_check_tcp_connection_name():
    result = await check_tcp_connection(port=19999)
    assert result.name == "tcp_connection"


@pytest.mark.asyncio
async def test_check_unity_state_no_connection():
    """No TCP → ok=False with descriptive message."""
    result = await check_unity_state(port=19999)
    assert result.ok is False


@pytest.mark.asyncio
async def test_run_doctor_returns_5_checks():
    results = await run_doctor(fix=False)
    assert len(results) == 5


@pytest.mark.asyncio
async def test_run_doctor_all_have_name():
    results = await run_doctor(fix=False)
    names = [r.name for r in results]
    assert len(names) == len(set(names)), "check names must be unique"


def test_format_report_green():
    results = [CheckResult("test", True, "all good")]
    report = format_report(results)
    assert "test" in report
    assert "✓" in report


def test_format_report_red():
    results = [CheckResult("port", False, "stale pid")]
    report = format_report(results)
    assert "✗" in report
    assert "stale pid" in report


def test_format_report_with_fix_cmd():
    results = [CheckResult("port", False, "stale", fix_cmd="rm file", auto_fixable=True)]
    report = format_report(results)
    assert "rm file" in report


def test_format_report_summary_line():
    results = [
        CheckResult("a", True, "ok"),
        CheckResult("b", False, "bad"),
    ]
    report = format_report(results)
    assert "1/2" in report


def test_user_messages_all_keys():
    expected = {"disconnected", "compiling", "dlls_stale", "frozen"}
    assert expected <= set(USER_MESSAGES.keys())


@pytest.mark.asyncio
async def test_doctor_autofix_stale_port(tmp_path):
    """run_doctor(fix=True) with only stale port file → file deleted, no Unity running."""
    port_file = tmp_path / "99999999.port"
    port_file.write_text("9500\n/some/path", encoding="utf-8")
    with patch("unity_mcp.doctor._ports_dir", lambda: tmp_path):
        results = await run_doctor(fix=True)
    port_check = next(r for r in results if r.name == "port_file")
    assert not port_file.exists()
    # Stale cleaned but no live Unity → still not ok
    assert port_check.ok is False


# ── P-417: diagnose timeout parameter ────────────────────────────────────────

async def test_check_unity_state_custom_timeout():
    """P-417: _diagnose_timeout parameter controls frame_read timeout."""
    from contextlib import asynccontextmanager
    from unittest.mock import MagicMock
    read_timeout_used = None

    async def fake_frame_read(reader, timeout):
        nonlocal read_timeout_used
        read_timeout_used = timeout
        raise asyncio.TimeoutError("simulated slow response")

    @asynccontextmanager
    async def fake_connect(port, timeout=3.0):
        mock_reader = MagicMock()
        mock_writer = MagicMock()
        mock_writer.drain = AsyncMock()
        yield mock_reader, mock_writer

    with patch("unity_mcp.doctor.frame_read_with_timeout", side_effect=fake_frame_read), \
         patch("unity_mcp.doctor._tcp_connect", side_effect=fake_connect), \
         patch("unity_mcp.doctor.frame_write"):
        result = await check_unity_state(port=9999, _diagnose_timeout=15.0)

    assert read_timeout_used == 15.0
    assert not result.ok


async def test_check_unity_state_default_timeout_is_10():
    """P-417: default _diagnose_timeout must be 10.0 (was 3.0)."""
    from contextlib import asynccontextmanager
    from unittest.mock import MagicMock
    read_timeout_used = None

    async def fake_frame_read(reader, timeout):
        nonlocal read_timeout_used
        read_timeout_used = timeout
        raise asyncio.TimeoutError("simulated")

    @asynccontextmanager
    async def fake_connect(port, timeout=3.0):
        mock_writer = MagicMock()
        mock_writer.drain = AsyncMock()
        yield MagicMock(), mock_writer

    with patch("unity_mcp.doctor.frame_read_with_timeout", side_effect=fake_frame_read), \
         patch("unity_mcp.doctor._tcp_connect", side_effect=fake_connect), \
         patch("unity_mcp.doctor.frame_write"):
        result = await check_unity_state(port=9999)

    assert read_timeout_used == 10.0
