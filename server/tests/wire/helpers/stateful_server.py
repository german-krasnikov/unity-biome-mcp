"""StatefulUnityPeer + StatefulFakeServer: in-memory object registry for Hypothesis tests."""

import dataclasses
from pathlib import Path

from gauntlet.fake_unity_peer import PeerReply, ScriptedUnityPeer

from tests.wire.helpers.fake_server import FakeUnityServer


@dataclasses.dataclass
class ObjectRecord:
    name: str
    path: str  # "/Name"
    properties: dict[str, dict[str, str]] = dataclasses.field(default_factory=dict)
    active: bool = True


class StatefulUnityPeer:
    """Intercepts CRUD commands and replies from in-memory registry.

    All other commands delegate to ScriptedUnityPeer for ping/get_version/etc.
    """

    def __init__(self) -> None:
        self._scripted = ScriptedUnityPeer(project_path=Path("fake-project"))
        self._objects: dict[str, ObjectRecord] = {}  # path -> record
        self.transcript: list[dict] = []

    def _reply(self, request: dict) -> PeerReply:
        cmd = request.get("cmd")
        args: dict = request.get("args") or {}

        handlers = {
            "create_object": lambda: self._handle_create(args),
            "delete_object": lambda: self._handle_delete(args),
            "set_property": lambda: self._handle_set_property(args),
            "get_hierarchy": lambda: self._handle_get_hierarchy(),
            "get_component": lambda: self._handle_get_component(args),
            "set_active": lambda: self._handle_set_active(args),
        }
        handler = handlers.get(str(cmd) if cmd else "")
        if handler:
            return handler()
        return self._scripted._reply(request)

    def _handle_create(self, args: dict) -> PeerReply:
        name = str(args.get("name", ""))
        path = f"/{name}"
        if path in self._objects:
            return PeerReply(ok=False, error=f"Object already exists: {path}")
        self._objects[path] = ObjectRecord(name=name, path=path)
        return PeerReply(ok=True, data=path)

    def _handle_delete(self, args: dict) -> PeerReply:
        path = str(args.get("path", ""))
        if path not in self._objects:
            return PeerReply(ok=False, error=f"Object not found: {path}")
        del self._objects[path]
        return PeerReply(ok=True, data="")

    def _handle_set_property(self, args: dict) -> PeerReply:
        path = str(args.get("path", ""))
        component = str(args.get("component", "Transform"))
        prop = str(args.get("prop", ""))
        value = str(args.get("value", ""))
        if path not in self._objects:
            return PeerReply(ok=False, error=f"Object not found: {path}")
        obj = self._objects[path]
        obj.properties.setdefault(component, {})[prop] = value
        return PeerReply(ok=True, data="")

    def _handle_get_hierarchy(self) -> PeerReply:
        lines = ["Scene: Fake"]
        for path in self._objects:
            obj = self._objects[path]
            prefix = "" if obj.active else "!"
            lines.append(f"{prefix}{path}")
        return PeerReply(ok=True, data="\n".join(lines))

    def _handle_get_component(self, args: dict) -> PeerReply:
        path = str(args.get("path", ""))
        component = str(args.get("component", "Transform"))
        if path not in self._objects:
            return PeerReply(ok=False, error=f"Object not found: {path}")
        obj = self._objects[path]
        props = obj.properties.get(component, {})
        data = "\n".join(f"{k}={v}" for k, v in props.items())
        return PeerReply(ok=True, data=data)

    def _handle_set_active(self, args: dict) -> PeerReply:
        path = str(args.get("path", ""))
        active = str(args.get("active", "true")).lower() != "false"
        if path not in self._objects:
            return PeerReply(ok=False, error=f"Object not found: {path}")
        self._objects[path].active = active
        return PeerReply(ok=True, data="")


class StatefulFakeServer(FakeUnityServer):
    """FakeUnityServer backed by StatefulUnityPeer (real in-memory object state)."""

    def __init__(self) -> None:
        super().__init__()
        self._peer = StatefulUnityPeer()  # replace scripted peer

    @property
    def objects(self) -> dict[str, ObjectRecord]:
        """Direct access to the object registry for test assertions."""
        return self._peer._objects  # type: ignore[attr-defined]
