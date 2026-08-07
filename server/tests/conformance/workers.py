from __future__ import annotations

import uuid
from dataclasses import dataclass, field


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
        """Verify port, project, dirty=False, playing=False, compiling=False.

        Raises AssertionError on any mismatch.
        """
        resp = await bridge.send("mcp_status", {})
        data = resp.get("data", {})

        actual_port = data.get("port")
        if actual_port != self.port:
            raise AssertionError(f"port mismatch: expected {self.port}, got {actual_port}")

        actual_path = data.get("project_path")
        if actual_path != self.project_path:
            raise AssertionError(f"project_path mismatch: expected {self.project_path!r}, got {actual_path!r}")

        if data.get("dirty"):
            raise AssertionError(f"scene is dirty (expected clean): {data}")

        if data.get("playing"):
            raise AssertionError(f"editor is in Play Mode (expected EditMode): {data}")

        if data.get("compiling"):
            raise AssertionError(f"editor is compiling (expected idle): {data}")

    async def prove_absent(self, bridge) -> None:
        """Confirm scene_ns does NOT appear in get_hierarchy response.

        Raises AssertionError if cleanup failed.
        """
        resp = await bridge.send("get_hierarchy", {"depth": 1})
        hierarchy_text = resp.get("data", "")
        if self.scene_ns in hierarchy_text:
            raise AssertionError(f"scene_ns {self.scene_ns!r} still present in hierarchy — cleanup failed")
