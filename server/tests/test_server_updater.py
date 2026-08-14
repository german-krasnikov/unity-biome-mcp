"""Tests for ServerUpdater — auto-restart MCP server on UPM plugin update."""
import asyncio
import json
import struct
import sys
from unittest.mock import AsyncMock, Mock, patch

import pytest
from unity_mcp.server_updater import ServerUpdater, _default_is_uvx_install, _updater as module_updater


def make_updater(current="1.0.0", uvx_found=True, subprocess_exit=0, exit_calls=None):
    if exit_calls is None:
        exit_calls = []

    async def fake_subprocess(*args, **kwargs):
        class FakeProc:
            async def wait(self):
                return subprocess_exit

        return FakeProc()

    return ServerUpdater(
        install_url="git+https://example.com",
        version_fn=lambda: current,
        which_fn=lambda name: "/usr/bin/uvx" if uvx_found else None,
        subprocess_fn=fake_subprocess,
        exit_fn=lambda code: exit_calls.append(code),
        is_uvx_install_fn=lambda: True,
    )


@pytest.mark.asyncio
async def test_no_update_when_same_version():
    u = make_updater(current="1.5.0")
    r = await u.maybe_update("1.5.0")
    assert r.reason == "not_needed"
    assert r.triggered is False


@pytest.mark.asyncio
async def test_no_update_when_server_newer():
    u = make_updater(current="1.6.0")
    r = await u.maybe_update("1.5.0")
    assert r.reason == "not_needed"
    assert r.triggered is False


@pytest.mark.asyncio
async def test_update_triggered_when_plugin_newer():
    calls = []
    u = make_updater(current="1.5.0", exit_calls=calls)
    r = await u.maybe_update("1.6.0")
    assert r.triggered is True
    assert calls == [0]


@pytest.mark.asyncio
async def test_skip_when_no_uvx():
    u = make_updater(uvx_found=False)
    r = await u.maybe_update("1.6.0")
    assert r.reason == "no_uvx"
    assert r.triggered is False


@pytest.mark.asyncio
async def test_no_exit_when_reinstall_fails():
    calls = []
    u = make_updater(current="1.5.0", subprocess_exit=1, exit_calls=calls)
    r = await u.maybe_update("1.6.0")
    assert calls == []
    assert r.reason == "reinstall_failed"


@pytest.mark.asyncio
async def test_debounce_prevents_double_update():
    calls = []
    u = make_updater(current="1.5.0", exit_calls=calls)
    u._updating = True
    r = await u.maybe_update("1.6.0")
    assert r.reason == "already_running"
    assert calls == []


@pytest.mark.asyncio
async def test_correct_subprocess_args():
    captured = []

    async def cap_subprocess(*args, **kwargs):
        captured.extend(args)

        class P:
            async def wait(self):
                return 0

        return P()

    u = ServerUpdater(
        install_url="git+https://example.com#sub",
        version_fn=lambda: "1.5.0",
        which_fn=lambda _: "/usr/bin/uvx",
        subprocess_fn=cap_subprocess,
        exit_fn=lambda _: None,
        is_uvx_install_fn=lambda: True,
    )
    await u.maybe_update("1.6.0")
    assert captured == [
        "uvx",
        "--reinstall",
        "--from",
        "git+https://example.com#sub",
        "unity-biome-mcp",
    ]


@pytest.mark.asyncio
async def test_skip_when_not_uvx_install():
    u = ServerUpdater(
        install_url="git+https://example.com",
        version_fn=lambda: "1.5.0",
        which_fn=lambda _: "/usr/bin/uvx",
        subprocess_fn=None,
        exit_fn=lambda _: None,
        is_uvx_install_fn=lambda: False,
    )
    r = await u.maybe_update("1.6.0")
    assert r.reason == "not_uvx_install"


def test_is_newer_basic():
    u = make_updater(current="1.5.0")
    assert u._is_update_needed("1.6.0") is True
    assert u._is_update_needed("1.5.0") is False
    assert u._is_update_needed("1.4.9") is False


def test_is_newer_handles_prerelease():
    u = make_updater(current="0.0.0-dev")
    assert u._is_update_needed("1.0.0") is True


def test_is_newer_handles_malformed():
    u = make_updater(current="1.5.0")
    assert u._is_update_needed("") is False
    assert u._is_update_needed("not-a-version") is False


@pytest.mark.asyncio
async def test_updating_flag_reset_on_failure():
    """After a failed reinstall, _updating is reset so retry is possible."""
    calls = []
    u = make_updater(current="1.5.0", subprocess_exit=1, exit_calls=calls)
    await u.maybe_update("1.6.0")
    assert u._updating is False


def test_module_singleton_is_server_updater():
    """Module-level _updater singleton has the right type."""
    assert isinstance(module_updater, ServerUpdater)


@pytest.mark.asyncio
async def test_bridge_schedules_update_task_when_plugin_newer():
    """bridge._open_reconnect_candidate schedules maybe_update when plugin is newer."""
    from unity_mcp.bridge import UnityBridge, frame_write

    scheduled = []

    class FakeUpdater:
        async def maybe_update(self, plugin_version):
            scheduled.append(plugin_version)

    # Build fake TCP frames: ping pong + project check + version with newer plugin
    def make_frame(data: dict) -> bytes:
        p = json.dumps(data).encode()
        return struct.pack("!I", len(p)) + p

    ping_frame = make_frame({"id": "rc0001", "ok": True, "data": "pong"})
    ver_frame = make_frame(
        {"id": "ver", "ok": True, "data": "proto:3|plugin:99.0.0|stamp:abc"}
    )

    # Sequence of reads: each frame = header (4 bytes) then payload.
    # _verify_candidate_project is patched away — no proj_frame needed.
    def split_frames(*frames):
        chunks = []
        for f in frames:
            chunks.append(f[:4])   # header
            chunks.append(f[4:])   # payload
        return chunks

    reader = AsyncMock()
    reader.readexactly.side_effect = split_frames(ping_frame, ver_frame)

    writer = Mock()
    writer.drain = AsyncMock()
    writer.is_closing = Mock(return_value=False)
    writer.close = Mock()
    writer.wait_closed = AsyncMock()
    writer.get_extra_info = Mock(return_value=None)

    with patch("unity_mcp.server_updater._updater", FakeUpdater()):
        with patch("asyncio.open_connection", return_value=(reader, writer)):
            with patch("unity_mcp.bridge.frame_write"):
                with patch("unity_mcp.bridge._apply_socket_options"):
                    bridge = UnityBridge.__new__(UnityBridge)
                    bridge._host = "127.0.0.1"
                    bridge._port = 9500
                    bridge._counter = 0
                    bridge._session_id = "test-session-id"
                    bridge._lock_token = "test-lock-token"
                    bridge._bridge_id = "br-test"
                    bridge._started_at_utc = "2026-01-01T00:00:00Z"
                    bridge._expected_project_path = None
                    bridge._probe = Mock()
                    bridge._probe.project_uuid = "project-uuid"

                    # Patch project check to no-op (focus on version step)
                    with patch.object(bridge, "_verify_candidate_project",
                                      new=AsyncMock()):
                        await bridge._open_reconnect_candidate(9500)

    # Allow the create_task coroutine to run
    await asyncio.sleep(0)

    assert "99.0.0" in scheduled


# M4: Tests for _default_is_uvx_install — positive identification only

def test_is_uvx_install_true_via_env(monkeypatch):
    monkeypatch.setenv("UV_TOOL_DIR", "/home/user/.local/share/uv/tools")
    monkeypatch.setattr(sys, "argv", ["/usr/bin/python3"])
    monkeypatch.setattr(sys, "executable", "/usr/bin/python3")
    assert _default_is_uvx_install() is True


def test_is_uvx_install_true_via_argv(monkeypatch):
    monkeypatch.delenv("UV_TOOL_DIR", raising=False)
    monkeypatch.setattr(sys, "argv", ["/home/user/.local/bin/uvx"])
    monkeypatch.setattr(sys, "executable", "/usr/bin/python3")
    assert _default_is_uvx_install() is True


def test_is_uvx_install_true_via_executable_uv_tools(monkeypatch):
    monkeypatch.delenv("UV_TOOL_DIR", raising=False)
    monkeypatch.setattr(sys, "argv", ["/some/script.py"])
    monkeypatch.setattr(
        sys, "executable",
        "/home/user/.local/share/uv/tools/unity-biome-mcp/bin/python3",
    )
    assert _default_is_uvx_install() is True


def test_is_uvx_install_false_for_venv(monkeypatch):
    monkeypatch.delenv("UV_TOOL_DIR", raising=False)
    monkeypatch.setattr(sys, "argv", ["/project/.venv/bin/python3"])
    monkeypatch.setattr(sys, "executable", "/project/.venv/bin/python3.12")
    assert _default_is_uvx_install() is False


def test_is_uvx_install_false_for_homebrew(monkeypatch):
    """Homebrew/system Python must not be mistaken for a uvx install."""
    monkeypatch.delenv("UV_TOOL_DIR", raising=False)
    monkeypatch.setattr(sys, "argv", ["/opt/homebrew/bin/python3.12"])
    monkeypatch.setattr(sys, "executable", "/opt/homebrew/bin/python3.12")
    assert _default_is_uvx_install() is False
