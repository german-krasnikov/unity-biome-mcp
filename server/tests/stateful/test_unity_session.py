"""Hypothesis RuleBasedStateMachine: models a Unity session end-to-end.

No Unity Editor required — all transport is in-process TCP against StatefulFakeServer.
Hypothesis shrinks failing sequences to minimal reproductions automatically.
"""

import asyncio

import pytest
from hypothesis import HealthCheck, settings
from hypothesis import strategies as st
from hypothesis.stateful import (
    Bundle,
    RuleBasedStateMachine,
    initialize,
    invariant,
    multiple,
    rule,
)

from tests.wire.helpers.stateful_server import StatefulFakeServer
from unity_mcp.bridge import UnityBridge

pytestmark = [pytest.mark.wire, pytest.mark.stateful]

# Strategies
names_st = st.from_regex(r"[A-Za-z][A-Za-z0-9_]{0,11}", fullmatch=True)
float_st = st.floats(min_value=-1000.0, max_value=1000.0, allow_nan=False, allow_infinity=False)


class UnitySessionMachine(RuleBasedStateMachine):
    """Models create/delete/modify/query operations against a stateful fake server.

    Python shadow dict mirrors server state. @invariant checks shadow vs server
    after every rule step.
    """

    existing = Bundle("objects")  # stores object names (no leading slash)

    def __init__(self) -> None:
        super().__init__()
        self._loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self._loop)
        self._server = StatefulFakeServer()
        self._loop.run_until_complete(self._server.start())
        self._bridge = UnityBridge("127.0.0.1", self._server.port)
        self._loop.run_until_complete(self._bridge.connect())
        self._shadow: dict[str, dict] = {}  # name -> {"position": {"x", "y"}, "active": bool}

    def teardown(self) -> None:
        self._loop.run_until_complete(self._bridge.close())
        self._loop.run_until_complete(self._server.close())
        self._loop.close()
        asyncio.set_event_loop(None)

    def _call(self, cmd: str, args: dict) -> dict:
        """Synchronous bridge.send() wrapper for use inside synchronous rules."""
        return self._loop.run_until_complete(self._bridge.send(cmd, args))

    @initialize()
    def init_scene(self) -> None:
        """Warm up the bridge — sets _state = CONNECTED after first successful send."""
        result = self._call("get_hierarchy", {})
        assert result["ok"], f"get_hierarchy failed on init: {result}"

    @rule(target=existing, name=names_st)
    def create_object(self, name: str):
        result = self._call("create_object", {"name": name})
        if result["ok"]:
            self._shadow[name] = {"position": {"x": 0.0, "y": 0.0}, "active": True}
            return name
        return multiple()

    @rule(name=existing)
    def delete_object(self, name: str) -> None:
        from hypothesis import assume
        assume(name in self._shadow)
        result = self._call("delete_object", {"path": f"/{name}"})
        if result["ok"]:
            self._shadow.pop(name, None)

    @rule(name=existing, x=float_st)
    def set_position_x(self, name: str, x: float) -> None:
        from hypothesis import assume
        assume(name in self._shadow)
        result = self._call("set_property", {
            "path": f"/{name}",
            "component": "Transform",
            "prop": "position.x",
            "value": str(x),
        })
        if result["ok"]:
            self._shadow[name]["position"]["x"] = x

    @rule(name=existing, y=float_st)
    def set_position_y(self, name: str, y: float) -> None:
        from hypothesis import assume
        assume(name in self._shadow)
        result = self._call("set_property", {
            "path": f"/{name}",
            "component": "Transform",
            "prop": "position.y",
            "value": str(y),
        })
        if result["ok"]:
            self._shadow[name]["position"]["y"] = y

    @rule()
    def query_hierarchy(self) -> None:
        result = self._call("get_hierarchy", {})
        assert result["ok"], f"get_hierarchy failed: {result}"

    @rule(name=existing)
    def query_component(self, name: str) -> None:
        from hypothesis import assume
        assume(name in self._shadow)
        self._call("get_component", {"path": f"/{name}", "component": "Transform"})

    @rule(target=existing, name=names_st)
    def recreate_after_delete(self, name: str):
        """Delete if exists, then recreate — probes duplicate-rejection + cleanup."""
        if name in self._shadow:
            del_result = self._call("delete_object", {"path": f"/{name}"})
            if del_result["ok"]:
                self._shadow.pop(name, None)
        create_result = self._call("create_object", {"name": name})
        if create_result["ok"]:
            self._shadow[name] = {"position": {"x": 0.0, "y": 0.0}, "active": True}
            return name
        return multiple()

    @invariant()
    def shadow_matches_hierarchy(self) -> None:
        """Every shadow object must appear in the hierarchy response."""
        result = self._call("get_hierarchy", {})
        assert result["ok"], f"get_hierarchy failed during invariant check: {result}"
        hierarchy = result["data"]
        for name in self._shadow:
            assert f"/{name}" in hierarchy, (
                f"Shadow object /{name} missing from hierarchy.\n"
                f"Shadow: {list(self._shadow)}\n"
                f"Hierarchy:\n{hierarchy}"
            )

    @invariant()
    def bridge_stays_connected(self) -> None:
        """Bridge writer must stay open — detects silent connection failure."""
        assert self._bridge.connected, (
            f"Bridge disconnected. State: {self._bridge._state}"
        )


TestUnitySession = UnitySessionMachine.TestCase
TestUnitySession.settings = settings(
    max_examples=50,
    stateful_step_count=15,
    suppress_health_check=[HealthCheck.too_slow],
)
