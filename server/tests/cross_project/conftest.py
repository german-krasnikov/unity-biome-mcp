from __future__ import annotations

import os

import pytest
import pytest_asyncio

from conformance.workers import ConformanceWorker, connect_bridge

HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
PORT_A = int(os.environ.get("UNITY_MCP_PORT", "9500"))
PORT_B = int(os.environ.get("UNITY_MCP_SECOND_PORT", "0"))
PROJECT = os.environ.get("UNITY_MCP_PROJECT_PATH", "")
PROJECT_B = os.environ.get("UNITY_MCP_SECOND_PROJECT_PATH", "")

# Applied to all tests in this directory — forces session loop so session-scoped
# bridge fixtures (whose asyncio.Queue is bound to the session loop) don't hang.
pytestmark = [pytest.mark.asyncio(loop_scope="session")]


@pytest_asyncio.fixture(scope="session", loop_scope="session")
async def dual_worker_session():
    """Two independent Unity workers for cross-project isolation tests.

    Worker B is expected to have MCPSettings.ReadOnly = true.
    test_read_only.py tests will fail (not skip) if Worker B is not read-only.

    Skips if UNITY_MCP_SECOND_PORT is not set or either Unity is unreachable.
    """
    if not PORT_B:
        pytest.skip("UNITY_MCP_SECOND_PORT not set — cross_project live tests skipped")
    if not PROJECT:
        pytest.skip("UNITY_MCP_PROJECT_PATH not set — cross_project live tests skipped")
    if not PROJECT_B:
        pytest.skip("UNITY_MCP_SECOND_PROJECT_PATH not set — cross_project live tests skipped")

    bridge_a = await connect_bridge(HOST, PORT_A, PROJECT)
    if bridge_a is None:
        pytest.skip(f"Worker A unreachable at {HOST}:{PORT_A}")

    bridge_b = await connect_bridge(HOST, PORT_B, PROJECT_B)
    if bridge_b is None:
        await bridge_a.close()
        pytest.skip(f"Worker B unreachable at {HOST}:{PORT_B}")

    worker_a = ConformanceWorker(port=PORT_A, project_path=PROJECT)
    worker_b = ConformanceWorker(port=PORT_B, project_path=PROJECT_B)

    try:
        await worker_a.gate(bridge_a)
    except AssertionError as e:
        await bridge_a.close()
        await bridge_b.close()
        pytest.fail(f"Worker A gate failed: {e}")

    try:
        await worker_b.gate(bridge_b)
    except AssertionError as e:
        await bridge_a.close()
        await bridge_b.close()
        pytest.fail(f"Worker B gate failed: {e}")

    yield worker_a, bridge_a, worker_b, bridge_b

    errors = []
    for worker, bridge, label in [
        (worker_a, bridge_a, "A"),
        (worker_b, bridge_b, "B"),
    ]:
        try:
            await worker.prove_absent(bridge)
        except AssertionError as e:
            errors.append(f"Worker {label}: {e}")
        finally:
            await bridge.close()

    if errors:
        pytest.fail("Cross-project teardown failed:\n" + "\n".join(errors))


@pytest_asyncio.fixture(scope="session", loop_scope="session")
async def conformance_worker():
    """Single Worker A fixture for fault_injection tests."""
    if not PROJECT:
        pytest.skip("UNITY_MCP_PROJECT_PATH not set")

    bridge = await connect_bridge(HOST, PORT_A, PROJECT)
    if bridge is None:
        pytest.skip(f"Worker A unreachable at {HOST}:{PORT_A}")

    worker = ConformanceWorker(port=PORT_A, project_path=PROJECT)
    try:
        await worker.gate(bridge)
    except AssertionError as e:
        await bridge.close()
        pytest.fail(f"Worker A gate failed: {e}")

    yield worker, bridge

    try:
        await worker.prove_absent(bridge)
    except AssertionError as e:
        pytest.fail(f"Teardown: {e}")
    finally:
        await bridge.close()
