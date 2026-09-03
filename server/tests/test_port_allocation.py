"""Regression tests for the ephemeral-port TOCTOU in relay test fixtures.

Before the fix, `relay_helpers.relay_server` probed a free port with a
throwaway socket (`_find_free_port()`), closed it, then bound a *second*
socket to that port number. Under concurrency, another bind can claim the
port in the gap between probe and real bind (TOCTOU). The fix binds port 0
directly and reads the OS-assigned port back off the live socket — no gap,
no collision possible.
"""
import asyncio
import inspect

import pytest

from unity_mcp.chat_relay import ChatRelay

from . import relay_helpers as _relay_helpers_mod
from .relay_helpers import relay_server, tcp_cmd  # noqa: F401  (relay_server is a fixture)

_CONCURRENT_BINDS = 20


async def _bind_relay() -> tuple[ChatRelay, asyncio.AbstractServer]:
    """Same bind pattern as relay_helpers.relay_server: bind port 0 directly, no separate probe."""
    relay = ChatRelay()
    server = await asyncio.start_server(relay._handle_client, "127.0.0.1", 0)
    return relay, server


async def test_concurrent_free_port_probes_do_not_collide():
    """20 concurrent ChatRelay binds must land on 20 distinct, live ports (no TOCTOU)."""
    pairs = await asyncio.gather(*(_bind_relay() for _ in range(_CONCURRENT_BINDS)))
    try:
        ports = [srv.sockets[0].getsockname()[1] for _relay, srv in pairs]
        assert len(ports) == _CONCURRENT_BINDS
        # distinct-port check is load-bearing: a probe-then-bind-elsewhere race
        # would silently let two binds land on the same port under contention
        assert len(set(ports)) == _CONCURRENT_BINDS
        assert all(srv.sockets for _relay, srv in pairs)  # every socket still open
    finally:
        for _relay, srv in pairs:
            srv.close()
            await srv.wait_closed()


def test_relay_server_fixture_does_not_use_probe_then_bind_helper():
    """Structural guard: relay_server must bind port 0 directly, not via
    _find_free_port. `_find_free_port` legitimately lives in relay_helpers.py
    now (moved there from cli_session.py — it has no production caller, kept
    only as a documented, quarantined test helper), so a bare
    `hasattr(module, "_find_free_port")` can no longer distinguish "safe to
    exist" from "the fixture wired it back in". Checking the fixture's own
    source is the precise guard: if relay_server starts calling
    _find_free_port again, the probe-then-bind TOCTOU gap is back — this goes
    red immediately and deterministically, independent of OS port-reuse
    timing (a real collision under contention is real but not reliably
    forceable in-process)."""
    fixture_src = inspect.getsource(_relay_helpers_mod.relay_server)
    assert "_find_free_port" not in fixture_src


async def test_relay_server_fixture_port_is_the_actually_bound_port(relay_server):  # noqa: F811  (fixture param name; aliasing the import breaks pytest fixture discovery — verified empirically)
    """relay_helpers.relay_server's returned port must be live/reachable, not a stale probe value."""
    relay, port = relay_server
    resp = await tcp_cmd(port, "status")
    assert resp["ok"] is True  # a real response proves the fixture's port is the live bound socket


@pytest.fixture
def _start_server_port_spy(monkeypatch):
    """Wrap asyncio.start_server to record the `port` argument a caller passes
    it, then call through to the real implementation. Declared before
    `relay_server` in a test's parameter list: same-scope fixtures with no
    dependency between them are set up in declared order, so this patch is
    installed before relay_server's setup runs and calls the wrapped
    start_server."""
    recorded: dict[str, int] = {}
    real_start_server = asyncio.start_server

    async def _spy(client_cb, host, port):
        recorded["port"] = port
        return await real_start_server(client_cb, host, port)

    monkeypatch.setattr(asyncio, "start_server", _spy)
    return recorded


async def test_relay_server_fixture_binds_port_zero_via_start_server(_start_server_port_spy, relay_server):  # noqa: F811
    """Behavioral spy alongside the getsource guard above: proves relay_server
    actually calls asyncio.start_server(..., 0) at runtime, not just that its
    source text lacks `_find_free_port`. Double-red: red if relay_server
    reverts to probing a port via _find_free_port() and passing that nonzero
    probed port to start_server instead of 0."""
    relay, port = relay_server
    assert _start_server_port_spy["port"] == 0
    resp = await tcp_cmd(port, "status")
    assert resp["ok"] is True  # live round trip proves the recorded port is the real bound socket
