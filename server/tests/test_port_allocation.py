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
