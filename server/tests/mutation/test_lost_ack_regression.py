"""S8: lost-ACK regression -- production sync_unity() through a real TCP proxy
that drops the first sync's reply. See Plans/MUTATION-REGRESSION-MODULE.md
matrix row S8 and Plans/Reviews/build-readiness-implementation-2026-09-06/
LIVE-QUALIFICATION.md ("Lost ACK": one real sync_ack delivered to Unity and
dropped by the proxy; caller got typed uncertainty in 1.687s; no second
effect).
"""
import asyncio
import time
from pathlib import Path

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from tests.mutation._canary import build_mutation_sdk, make_bridge, make_raw_send, sdk_args
from tests.mutation._proxy import LostAckProxy
from tests.mutation.conftest import MUTATION_HOST, MUTATION_PORT, MUTATION_PROJECT
from unity_mcp import editor_log
from unity_mcp.errors import UncertainDeliveryError, recovery_barrier
from unity_mcp.middleware import Middleware, wrap_send
from unity_mcp.tools import codegen, diagnose, objects, runtime, sync
from unity_mcp.tools.sync import _parse_status

pytestmark = [pytest.mark.live, pytest.mark.mutation_live, pytest.mark.timeout(300)]


async def _wait_stable(direct_send, timeout: float = 120.0) -> None:
    """The one real sync this test allows through starts an actual Unity
    refresh/reload cycle that can outlive the caller's UncertainDeliveryError
    (received before Unity finished). Drain it with read-only sync_status polls
    (retry-safe, never through the proxy) so the next test's fresh connection
    doesn't race Unity's TCP listener while it is still mid-reload."""
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            _, state, _ = _parse_status(await direct_send("sync_status", {}))
        except (ConnectionError, OSError):
            state = ""
        if state in ("ready", "idle"):
            return
        await asyncio.sleep(1.0)
    raise AssertionError(f"Unity did not settle to ready/idle within {timeout}s after the dropped sync")


async def test_lost_ack_live(monkeypatch, mutation_bridge):
    """One real sync forwarded and its ACK dropped -> sync_unity() raises the
    typed uncertainty exactly once, no resend reaches the proxy (checked both
    by rejected commands and by connection/command counts), and Unity still
    advanced its epoch by exactly one (the single sync it did execute)."""
    direct_send = make_raw_send(mutation_bridge)
    before_epoch, _, _ = _parse_status(await direct_send("sync_status", {}))

    proxy = LostAckProxy(MUTATION_HOST, MUTATION_PORT)
    listener = await asyncio.start_server(proxy.handle_client, "127.0.0.1", 0)
    project = Path(MUTATION_PROJECT).resolve()
    proxied_bridge = make_bridge(MUTATION_HOST, listener.sockets[0].getsockname()[1], project)
    try:
        await proxied_bridge.connect()
        editor_log.init_corroboration()
        mw = Middleware()
        wrapped_send = wrap_send(make_raw_send(proxied_bridge), mw)
        for module in (sync, diagnose, runtime, objects, codegen):
            monkeypatch.setattr(module, "_send", wrapped_send)
            monkeypatch.setattr(module, "_args", sdk_args, raising=False)
        sdk = build_mutation_sdk(proxied_bridge, mw, sync=sync, diagnose=diagnose,
                                  runtime=runtime, objects=objects, codegen=codegen)

        started = time.monotonic()
        with pytest.raises(ToolError) as error:
            await sdk.sync_unity(timeout=60)
        elapsed = time.monotonic() - started

        barrier = recovery_barrier(error.value)
        assert isinstance(barrier, UncertainDeliveryError), (type(barrier), str(error.value))
        assert elapsed < 30, elapsed
        assert proxy.forwarded_sync == 1
        assert proxy.rejected == []
        # No second sync/force_refresh reached the proxy: neither as a rejection
        # (checked above) nor as a fresh connection retrying the call, nor as an
        # extra command multiplexed on the one connection that stayed open.
        assert proxy.connections == 1
        assert proxy.commands.count("sync") == 1
        assert proxy.commands.count("force_refresh") == 0
    finally:
        await proxied_bridge.close()
        listener.close()
        await listener.wait_closed()

    after_epoch, _, _ = _parse_status(await direct_send("sync_status", {}))
    assert after_epoch == before_epoch + 1, (before_epoch, after_epoch)
    await _wait_stable(direct_send)
