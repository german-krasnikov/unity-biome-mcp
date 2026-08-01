"""Python gap tests for reload mini-server dispatch helpers (B1-B6)."""
import asyncio
import json
import os
import socket
import struct
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.tools import reload_ladder as _ladder
from unity_mcp.lockfile import read_reload_port


# B1: both main and reload dead → raises ConnectionError
async def test_send_with_fallback_raises_when_both_dead():
    send_main = AsyncMock(side_effect=ConnectionError("main dead"))
    send_reload = AsyncMock(side_effect=ConnectionError("reload dead"))

    with pytest.raises(ConnectionError):
        await _ladder._send_with_fallback(send_main, send_reload, "ping", {})

    send_main.assert_called_once()
    send_reload.assert_called_once()


# B2: reload=None → re-raises main exception immediately
async def test_send_with_fallback_raises_immediately_when_reload_none():
    send_main = AsyncMock(side_effect=ConnectionError("main dead"))

    with pytest.raises(ConnectionError, match="main dead"):
        await _ladder._send_with_fallback(send_main, None, "diagnose", {})


# B3: make_reload_send raises ConnectionError quickly (no hang) when port has no listener
async def test_make_reload_send_raises_connection_error_not_hang():
    # Bind a port then close it — guaranteed no listener
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()

    send_fn = _ladder.make_reload_send(port=port)

    with pytest.raises((ConnectionError, OSError)):
        await asyncio.wait_for(send_fn("ping", {}), timeout=1.0)


# B4: make_reload_send returns err field when ok=false
async def test_make_reload_send_returns_err_field_on_ok_false():
    response_data = json.dumps({"id": "r", "ok": False, "err": "bad request"}).encode()

    mock_writer = MagicMock()
    mock_writer.drain = AsyncMock()

    with patch("unity_mcp.tools.reload_ladder.frame_read",
               AsyncMock(return_value=response_data)), \
         patch("unity_mcp.tools.reload_ladder.frame_write"), \
         patch("asyncio.open_connection",
               new=AsyncMock(return_value=(AsyncMock(), mock_writer))):
        send_fn = _ladder.make_reload_send(port=9600)
        result = await send_fn("bad_cmd", {})

    assert result == "bad request"
    mock_writer.close.assert_called_once()


# B5: newest mtime wins when no cwd match (fallback sort)
def test_read_reload_port_newest_mtime_wins_when_no_cwd_match(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)

    pid = os.getpid()
    ppid = os.getppid()

    # older file (force ancient mtime)
    old_file = ports_dir / f"{pid}.reload-port"
    old_file.write_text("9701\n/other/proj\nOther", encoding="utf-8")
    os.utime(old_file, (1_000_000, 1_000_000))

    # newer file (default mtime = now)
    new_file = ports_dir / f"{ppid}.reload-port"
    new_file.write_text("9702\n/other/proj2\nOther2", encoding="utf-8")

    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True), \
         patch("os.getcwd", return_value="/unrelated/path"):
        result = read_reload_port()

    assert result == 9702  # newest mtime wins


# B6: ports dir exists but no .reload-port files → None (reload window)
def test_read_reload_port_none_during_reload_window(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    # No *.reload-port files — dir exists but file was deleted during reload

    with patch.object(Path, "home", return_value=tmp_path):
        result = read_reload_port()

    assert result is None
