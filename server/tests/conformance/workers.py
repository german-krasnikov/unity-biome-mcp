from __future__ import annotations

import asyncio
import uuid
from dataclasses import dataclass, field


async def connect_bridge(host: str, port: int, project: str = ""):
    """Connect a UnityBridge with retry. Returns None if unreachable."""
    from unity_mcp.bridge import UnityBridge

    bridge = UnityBridge(host, port=port, expected_project_path=project)
    for attempt in range(10):
        try:
            await bridge.connect()
            return bridge
        except (ConnectionRefusedError, OSError, asyncio.TimeoutError):
            if attempt < 9:
                await asyncio.sleep(1.0)
    return None


def _parse_status(text: str) -> dict[str, str]:
    return dict(line.split("=", 1) for line in text.splitlines() if "=" in line)


@dataclass
class ConformanceWorker:
    port: int
    project_path: str
    run_id: str = field(default_factory=lambda: uuid.uuid4().hex[:8])

    @property
    def asset_ns(self) -> str:
        """Namespaced asset folder for this conformance run."""
        return f"Assets/__MCP_CONF_{self.run_id}"

    @property
    def scene_ns(self) -> str:
        """Scene object name prefix for this conformance run."""
        return f"__MCP_CONF_{self.run_id}"

    async def gate(self, bridge) -> None:
        """Verify port, dirty=False, playing=False, compiling=False.

        project_path is validated by bridge.connect() via expected_project_path.
        Raises AssertionError on any mismatch.
        """
        resp = await bridge.send("get_status", {})
        info = _parse_status(resp.get("data", ""))

        actual_port = info.get("port", "")
        if str(self.port) != actual_port:
            raise AssertionError(f"port mismatch: expected {self.port}, got {actual_port}")

        if info.get("dirty", "false") == "true":
            raise AssertionError(f"scene is dirty (expected clean): {info}")

        if info.get("playing", "false") == "true":
            raise AssertionError(f"editor is in Play Mode (expected EditMode): {info}")

        if info.get("compiling", "false") == "true":
            raise AssertionError(f"editor is compiling (expected idle): {info}")

    async def prove_absent(self, bridge) -> None:
        """Confirm scene_ns does NOT appear in get_hierarchy response.

        Raises AssertionError if cleanup failed.
        """
        resp = await bridge.send("get_hierarchy", {"depth": 1})
        hierarchy_text = resp.get("data", "")
        if self.scene_ns in hierarchy_text:
            raise AssertionError(f"scene_ns {self.scene_ns!r} still present in hierarchy — cleanup failed")
