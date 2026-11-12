"""Hand-written Run host for the animation wire-seam plans
(dsl/test-plans/animation.editmode, dsl/test-plans/animation-omit.editmode).

Bridges the plain-sync `Run` contract the py printer expects
(biome-dsl implementations/typescript/packages/languages/py/src/plan-pytest.ts:
fixture/act/q/teardown, no async) onto this repo's async `animation()` tool and its
mock_bridge-shaped slot patch -- biome-dsl ships no reference Python host to copy from.
"""
import asyncio
from unittest.mock import AsyncMock, Mock, patch

from unity_mcp.server import animation

ABSENT_SENTINEL = "ABSENT"  # the plans' comments name this constant; see q() below


class Run:
    """fixture/act/q/teardown, exactly as the generated test calls them."""

    def __init__(self, test_name: str, case_id: str) -> None:
        self._wire: dict = {}
        bridge = Mock()
        bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
        bridge.connected = True
        bridge.transport_status = "tcp:connected"
        slot = Mock(bridge=bridge, connected=True, status="connected", port=9500)
        self._bridge = bridge
        self._patches = [patch("unity_mcp.server.slot", slot), patch("unity_mcp.server.manager", slot)]
        for started in self._patches:
            started.start()

    def fixture(self, name: str, **kwargs) -> None:
        pass  # the "tool" fixture holds no state of its own

    def act(self, name: str, **kwargs) -> None:
        asyncio.run(animation(**kwargs))
        self._wire = self._bridge.send.call_args[0][1]

    def q(self, path: str) -> object:
        _, key = path.split(".", 1)
        if key.endswith("_sent"):
            return "SENT" if key.removesuffix("_sent") in self._wire else ABSENT_SENTINEL
        return self._wire.get(key, ABSENT_SENTINEL)

    def teardown(self) -> None:
        for started in reversed(self._patches):
            started.stop()
