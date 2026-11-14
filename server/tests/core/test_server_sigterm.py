"""Tests for unity_mcp.server._handle_sigterm.

Split out of test_server_control.py: PID-lockfile monitoring itself moved to
biome_mcp.server_control (tested there); only Unity's own SIGTERM handler
wiring stays here.
"""


def test_sigterm_handler_no_lock_fd(monkeypatch):
    """Handler with lock_fd=None must not crash, must call os._exit(0)."""
    import unity_mcp.server as srv

    exit_calls = []
    monkeypatch.setattr(srv.os, "_exit", lambda code: exit_calls.append(code))

    # Reset state
    srv._sigterm_state.update({"requested": False, "lock_fd": None,
                               "loop": None, "task": None})
    srv._handle_sigterm(None, None)

    assert exit_calls == [0]
    assert srv._sigterm_state["requested"] is True


def test_sigterm_handler_releases_lock(monkeypatch):
    """Handler with lock_fd=42 must call release_lock(42) and clear lock_fd."""
    import unity_mcp.server as srv

    released = []
    exit_calls = []
    monkeypatch.setattr(srv, "release_lock", lambda fd: released.append(fd))
    monkeypatch.setattr(srv.os, "_exit", lambda code: exit_calls.append(code))

    srv._sigterm_state.update({"requested": False, "lock_fd": 42,
                               "loop": None, "task": None})
    srv._handle_sigterm(None, None)

    assert released == [42]
    assert srv._sigterm_state["lock_fd"] is None
    assert exit_calls == [0]


def test_sigterm_handler_idempotent(monkeypatch):
    """Second call when requested=True must early-return, no release_lock called."""
    import unity_mcp.server as srv

    released = []
    exit_calls = []
    monkeypatch.setattr(srv, "release_lock", lambda fd: released.append(fd))
    monkeypatch.setattr(srv.os, "_exit", lambda code: exit_calls.append(code))

    srv._sigterm_state.update({"requested": True, "lock_fd": 42,
                               "loop": None, "task": None})
    srv._handle_sigterm(None, None)

    assert released == []
    assert exit_calls == []
