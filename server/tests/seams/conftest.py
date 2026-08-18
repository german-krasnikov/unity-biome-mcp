"""Fixtures for seam tests: SeamWorker + seam_bridge + seam_worker."""

import os
import re
import uuid
from dataclasses import dataclass, field

import pytest
import pytest_asyncio
from conformance.workers import connect_bridge

SEAM_HOST = os.environ.get("UNITY_MCP_HOST", "127.0.0.1")
SEAM_PORT = int(os.environ.get("UNITY_MCP_PORT", "9500"))
SEAM_PROJECT = os.environ.get("UNITY_MCP_PROJECT_PATH", "")


@dataclass
class SeamWorker:
    """Isolated namespace + cleanup for a single test function.

    All objects created by a test should be named with self.name(suffix)
    so that clean() can find and remove them on teardown.
    """

    port: int
    project_path: str
    run_id: str = field(default_factory=lambda: uuid.uuid4().hex[:8])

    @property
    def ns(self) -> str:
        """Object name prefix unique to this test invocation."""
        return f"__SEAM_{self.run_id}"

    def name(self, suffix: str) -> str:
        """Return a unique namespaced test object name."""
        return f"{self.ns}_{suffix}"

    async def clean(self, bridge) -> list[str]:
        """Delete all root-level objects whose name starts with self.ns.

        Reads depth=1 hierarchy, finds matching names via regex, deletes each.
        Returns a list of error messages for any objects that could not be deleted.
        """
        resp = await bridge.send("get_hierarchy", {"depth": 1})
        hier = resp.get("data", "")
        pattern = re.compile(rf'\b({re.escape(self.ns)}\w*)')
        seen: set[str] = set()
        errors: list[str] = []
        for line in hier.splitlines():
            m = pattern.search(line)
            if m:
                name = m.group(1)
                if name not in seen:
                    seen.add(name)
                    try:
                        await bridge.send("delete_object", {"path": f"/{name}"})
                    except Exception as exc:  # noqa: BLE001
                        errors.append(f"failed to delete /{name}: {exc}")
        return errors


@pytest_asyncio.fixture(scope="session", loop_scope="session")
async def seam_bridge():
    """Session-scoped TCP bridge. Skips all seam tests if Unity unreachable."""
    if not SEAM_PROJECT:
        pytest.skip("UNITY_MCP_PROJECT_PATH not set — seam tests skipped")
    bridge = await connect_bridge(SEAM_HOST, SEAM_PORT, SEAM_PROJECT)
    if bridge is None:
        pytest.skip(f"Unity unreachable at {SEAM_HOST}:{SEAM_PORT} — seam tests skipped")
    yield bridge
    await bridge.close()


@pytest_asyncio.fixture(scope="function", loop_scope="session")
async def seam_worker(seam_bridge):
    """Function-scoped worker: fresh namespace per test, auto-cleanup on teardown."""
    worker = SeamWorker(port=SEAM_PORT, project_path=SEAM_PROJECT)
    yield worker
    errors = await worker.clean(seam_bridge)
    assert not errors, "Seam cleanup errors:\n" + "\n".join(errors)
