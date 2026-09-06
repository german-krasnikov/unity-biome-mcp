"""Mutation regression lane: opt-in gate + fail-closed worker requirement.

No `unity_state_owner` here -- mutation tests manage their own canary
directory (outside `Assets/TestsTemp/PythonLive/`), use `[BiomeWorkerOnly]`
disposable-worker boundaries, and perform explicit cleanup with 516-baseline
verification. See Plans/MUTATION-REGRESSION-MODULE.md section 9.
"""
import os
import socket
import time
from pathlib import Path

import pytest

from unity_mcp.bridge import UnityBridge

REAL_PORTS_DIR = Path.home() / ".unity-biome-mcp" / "ports"  # at import, before conftest patches Path.home(); reserved for port-file discovery (A5+)
MUTATION_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
MUTATION_PORT = int(os.environ.get("UNITY_MCP_PORT", "9600"))
MUTATION_PROJECT = os.environ.get("UNITY_MCP_PROJECT_PATH", "")
MUTATION_LIVE_GATE = "UNITY_MCP_RUN_MUTATION_LIVE"


def pytest_collection_modifyitems(items):
    if os.environ.get(MUTATION_LIVE_GATE) != "1":
        skip = pytest.mark.skip(reason=f"mutation lane; set {MUTATION_LIVE_GATE}=1")
        for item in items:
            if item.get_closest_marker("mutation_live"):
                item.add_marker(skip)


def _bridge_up(timeout: float = 0.2) -> bool:
    try:
        with socket.create_connection((MUTATION_HOST, MUTATION_PORT), timeout=timeout):
            return True
    except OSError:
        return False


@pytest.fixture(scope="session", autouse=True)
def _require_mutation_worker():
    """Require the configured worker; an unavailable mutation gate is a failure."""
    if os.environ.get(MUTATION_LIVE_GATE) != "1":
        return  # gate closed -- items are already skipped, never dial out
    for _ in range(10):
        if _bridge_up():
            return
        time.sleep(1)
    pytest.fail(
        f"verified mutation worker unavailable: host={MUTATION_HOST} "
        f"port={MUTATION_PORT} project={MUTATION_PROJECT!r}"
    )


@pytest.fixture
async def mutation_bridge():
    """Bare bridge factory for mutation regression tests -- no ownership wrapper."""
    bridge = UnityBridge(
        MUTATION_HOST,
        port=MUTATION_PORT,
        expected_project_path=MUTATION_PROJECT or None,
    )
    await bridge.connect()
    try:
        yield bridge
    finally:
        await bridge.close()
