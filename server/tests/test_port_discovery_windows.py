"""Tests for Windows path fix (A5) and startup race fix (A2).

A5: _is_path_prefix uses normcase+normpath for cross-platform separator handling.
A2: discover_port_with_retry retries with backoff when no port file exists yet.
"""
from unittest.mock import AsyncMock, patch

import pytest

from unity_mcp.server_filtering import _is_path_prefix, read_unity_port as _read_unity_port


# ---------------------------------------------------------------------------
# Group A — _is_path_prefix pure unit tests
# ---------------------------------------------------------------------------

def test_is_path_prefix_exact_match():
    assert _is_path_prefix("/foo/bar", "/foo/bar")


def test_is_path_prefix_child():
    assert _is_path_prefix("/foo/bar/baz", "/foo/bar")


def test_is_path_prefix_no_partial_name():
    """/foo/bar2 must NOT match prefix /foo/bar."""
    assert not _is_path_prefix("/foo/bar2", "/foo/bar")


def test_is_path_prefix_prefix_longer_than_cwd():
    assert not _is_path_prefix("/foo/bar", "/foo/bar/baz")


def test_is_path_prefix_trailing_slash_normalized():
    """Trailing slash in prefix gets normalized away by normpath."""
    assert _is_path_prefix("/foo/bar/baz", "/foo/bar/")


# ---------------------------------------------------------------------------
# Group B — Windows slash normalization via normcase mock
# ---------------------------------------------------------------------------

def test_is_path_prefix_windows_forward_slash_in_pp():
    """Simulate Windows: pp uses '/', cwd uses os.sep='\\'.
    normcase on Windows converts both to '\\' and lowercases.
    """
    def fake_normcase(p: str) -> str:
        return p.replace("/", "\\").lower()

    with patch("os.path.normcase", side_effect=fake_normcase), \
         patch("os.sep", "\\"):
        from unity_mcp import server_filtering
        assert server_filtering._is_path_prefix(
            r"C:\Users\User\Project\Assets",
            "C:/Users/User/Project",
        )


def test_is_path_prefix_windows_no_false_match_partial_name():
    """Windows: 'C:/Users/ProjectAB' must not match prefix 'C:/Users/Project'."""
    def fake_normcase(p: str) -> str:
        return p.replace("/", "\\").lower()

    with patch("os.path.normcase", side_effect=fake_normcase), \
         patch("os.sep", "\\"):
        from unity_mcp import server_filtering
        assert not server_filtering._is_path_prefix(
            r"C:\Users\ProjectAB\Assets",
            "C:/Users/Project",
        )


# ---------------------------------------------------------------------------
# Group C — end-to-end read_unity_port with forward-slash project path
# ---------------------------------------------------------------------------

def test_read_unity_port_windows_forward_slash_project_path(monkeypatch, tmp_path):
    """Port file has forward-slash path; getcwd returns the same path.
    normpath normalizes consistently within the same OS.
    """
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.delenv("UNITY_MCP_PROJECT_DIR", raising=False)
    monkeypatch.delenv("CLAUDE_PROJECT_DIR", raising=False)

    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    # Unity writes forward-slash paths via Application.dataPath
    project_path = (tmp_path / "MyProject").as_posix()  # always forward slashes
    f = ports_dir / "1234.port"
    f.write_text(f"9502\n{project_path}\nMyProject\n", encoding="utf-8")

    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    # mock at abstraction level — works on all platforms (Windows uses ctypes, not os.kill)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    # getcwd returns native-sep path (normpath handles either form on same OS)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "MyProject" / "Assets"))

    result = _read_unity_port()
    assert result == 9502


# ---------------------------------------------------------------------------
# Group D — discover_port_with_retry async tests
# ---------------------------------------------------------------------------

async def test_discover_port_immediate_hit(monkeypatch):
    """First call finds a port file — no sleep."""
    from unity_mcp.server_filtering import discover_port_with_retry

    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    sleep_calls = []

    with patch("unity_mcp.server_filtering.read_unity_port", return_value=9502), \
         patch("asyncio.sleep", side_effect=lambda d: sleep_calls.append(d)):
        result = await discover_port_with_retry()

    assert result == 9502
    assert sleep_calls == []


async def test_discover_port_eventual_hit(monkeypatch):
    """First call returns None (no files), second call succeeds."""
    from unity_mcp.server_filtering import discover_port_with_retry

    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    call_count = 0

    def fake_read(skip_probe=False):
        nonlocal call_count
        call_count += 1
        return 9502 if call_count >= 2 else None

    with patch("unity_mcp.server_filtering.read_unity_port", side_effect=fake_read), \
         patch("asyncio.sleep", new_callable=AsyncMock):
        result = await discover_port_with_retry(attempts=3, initial_delay=0.0)

    assert result == 9502


async def test_discover_port_all_fail_returns_default(monkeypatch):
    """All attempts return None — falls back to DEFAULT_PORT."""
    from unity_mcp.server_filtering import discover_port_with_retry
    from unity_mcp.constants import DEFAULT_PORT

    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)

    with patch("unity_mcp.server_filtering.read_unity_port", return_value=None), \
         patch("asyncio.sleep", new_callable=AsyncMock):
        result = await discover_port_with_retry(attempts=3, initial_delay=0.0)

    assert result == DEFAULT_PORT


async def test_discover_port_env_var_skips_retry(monkeypatch):
    """UNITY_MCP_PORT set → returns immediately, no sleep."""
    from unity_mcp.server_filtering import discover_port_with_retry

    monkeypatch.setenv("UNITY_MCP_PORT", "9999")
    sleep_calls = []

    with patch("unity_mcp.server_filtering.read_unity_port", return_value=9999), \
         patch("asyncio.sleep", side_effect=lambda d: sleep_calls.append(d)):
        result = await discover_port_with_retry()

    assert result == 9999
    assert sleep_calls == []


async def test_discover_port_real_9500_no_retry(monkeypatch):
    """Port file says 9500 (real registration) — not a fallback, no retry."""
    from unity_mcp.server_filtering import discover_port_with_retry

    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    sleep_calls = []

    with patch("unity_mcp.server_filtering.read_unity_port", return_value=9500), \
         patch("asyncio.sleep", side_effect=lambda d: sleep_calls.append(d)):
        result = await discover_port_with_retry()

    assert result == 9500
    assert sleep_calls == []
