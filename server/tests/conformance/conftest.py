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


async def _teardown_live_conformance_item(item) -> None:
    """Per-test cleanup for one live+conformance item.

    Split out from the fixture below so tests/conformance/test_conftest_bridge_reuse.py
    can drive this exact dispatch logic (marker gate + bridge-reuse cleanup)
    against a faked connect_bridge/ConformanceWorker without a real event loop.
    """
    if "live" not in item.keywords or "conformance" not in item.keywords:
        return
    if not CONF_PROJECT:
        return
    await _cleanup_live_worker()


@pytest_asyncio.fixture(autouse=True, loop_scope="session")
async def _live_conformance_teardown(request):
    """Per-test cleanup for live+conformance tests, run on the session's own
    event loop rather than a fresh asyncio.run() loop per test.

    _session_bridge caches one UnityBridge for the whole session (see
    _SessionBridgeHolder above) — its internal asyncio primitives (locks,
    events, the StreamReader/Writer) are bound to whichever loop first
    touched them. The former pytest_runtest_teardown sync hook called
    asyncio.run(_cleanup_live_worker()) directly: asyncio.run() opens a new
    loop and closes it on return, so every test after the first reused the
    cached bridge from a loop different than (and, by the next test, already
    closed relative to) the one its primitives were created on — "Future
    attached to a different loop", then "Event loop is closed" under
    pytest-asyncio's session loop (Python 3.14). loop_scope="session" binds
    this fixture to the same persistent loop conformance_worker runs on, so
    the bridge is always touched from the loop it was created on. Same
    cleanup semantics and fail-closed behavior as before: skipped for tests
    without both the live and conformance markers, or with no project pinned.
    """
    yield
    await _teardown_live_conformance_item(request.node)


@pytest_asyncio.fixture(scope="session", loop_scope="session", autouse=True)
async def _session_bridge_final_close():
    """Closes _session_bridge while the session loop is still alive.

    A former pytest_sessionfinish hook did this via asyncio.run() after all
    tests ran — but pytest-asyncio's session loop is already closed by the
    time pytest_sessionfinish fires (session-scoped fixtures finalize first),
    so closing _bridge's loop-bound transport there raised 'Event loop is
    closed' and crashed the whole run. A session-scoped, loop_scope="session"
    fixture finalizes as part of that same loop-scope group's teardown,
    before the loop itself is torn down.
    """
    yield
    if _session_bridge.is_usable():
        await _session_bridge.close()


async def _cleanup_live_worker():
    bridge = await _session_bridge.get(CONF_HOST, CONF_PORT, CONF_PROJECT)
    if bridge is None:
        return
    worker = ConformanceWorker(port=CONF_PORT, project_path=CONF_PROJECT)
    await worker.prove_absent(bridge)
    await worker.discard_if_dirty(bridge)
