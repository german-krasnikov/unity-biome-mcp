import asyncio
import os

import pytest
import pytest_asyncio
from conformance.workers import ConformanceWorker, connect_bridge

CONF_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
CONF_PORT = int(os.environ.get("UNITY_MCP_PORT", "9500"))
CONF_PROJECT = os.environ.get("UNITY_MCP_PROJECT_PATH", "")


class _SessionBridgeHolder:
    """Caches one bridge connection across every per-test teardown call.

    Reconnecting fresh in every test's teardown paid a full TCP handshake
    per test for the whole conformance run. Reuse the same bridge for the
    session, falling back to a fresh connect only when the cached one is
    no longer usable (closed or unreachable).
    """

    def __init__(self):
        self._bridge = None

    def is_usable(self) -> bool:
        return self._bridge is not None and self._bridge.connected

    async def get(self, host, port, project):
        if not self.is_usable():
            self._bridge = await connect_bridge(host, port, project)
        return self._bridge

    async def close(self):
        if self._bridge is not None:
            await self._bridge.close()
            self._bridge = None


_session_bridge = _SessionBridgeHolder()


@pytest_asyncio.fixture(scope="session", loop_scope="session")
async def conformance_worker():
    """Session-scoped conformance worker with identity gate.

    Skips all conformance+live tests if Unity is unreachable or env vars not set.
    """
    if not CONF_PROJECT:
        pytest.skip("UNITY_MCP_PROJECT_PATH not set — conformance live tests skipped")

    bridge = await connect_bridge(CONF_HOST, CONF_PORT, CONF_PROJECT)
    if bridge is None:
        pytest.skip(f"Unity unreachable at {CONF_HOST}:{CONF_PORT} — conformance live tests skipped")

    worker = ConformanceWorker(port=CONF_PORT, project_path=CONF_PROJECT)

    try:
        await worker.gate(bridge)
    except AssertionError as e:
        await bridge.close()
        pytest.fail(f"Conformance identity gate failed: {e}")

    yield worker, bridge

    try:
        await worker.prove_absent(bridge)
    except AssertionError as e:
        pytest.fail(f"Conformance teardown: {e}")
    finally:
        await bridge.close()


def pytest_runtest_teardown(item, nextitem):  # noqa: ARG001
    if "live" not in item.keywords or "conformance" not in item.keywords:
        return
    if not CONF_PROJECT:
        return
    asyncio.run(_cleanup_live_worker())


def pytest_sessionfinish(session, exitstatus):  # noqa: ARG001
    if _session_bridge.is_usable():
        asyncio.run(_session_bridge.close())


async def _cleanup_live_worker():
    bridge = await _session_bridge.get(CONF_HOST, CONF_PORT, CONF_PROJECT)
    if bridge is None:
        return
    worker = ConformanceWorker(port=CONF_PORT, project_path=CONF_PROJECT)
    await worker.prove_absent(bridge)
    await worker.discard_if_dirty(bridge)
