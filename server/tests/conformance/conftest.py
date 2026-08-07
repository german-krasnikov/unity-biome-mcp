import os

import pytest
import pytest_asyncio

from conformance.workers import ConformanceWorker, connect_bridge

CONF_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
CONF_PORT = int(os.environ.get("UNITY_MCP_PORT", "9500"))
CONF_PROJECT = os.environ.get("UNITY_MCP_PROJECT_PATH", "")

# Applied to all tests in this directory — forces session loop so the session-scoped
# bridge fixture (whose asyncio.Queue is bound to the session loop) doesn't hang.
pytestmark = [pytest.mark.asyncio(loop_scope="session")]


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
