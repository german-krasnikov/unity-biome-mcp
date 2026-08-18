"""Single-slot Unity connection holder. One bridge, optional port switch."""

from .bridge import UnityBridge
from .constants import DEFAULT_PORT


class ConnectionSlot:
    def __init__(self, port_discoverer=None, on_port_change=None, is_retry_safe=None):
        self._bridge: UnityBridge | None = None
        self._port: int = DEFAULT_PORT
        self._host: str = "127.0.0.1"
        self._reconnect_callbacks: list = []
        self._port_discoverer = port_discoverer
        self._on_port_change = on_port_change
        self._is_retry_safe = is_retry_safe

    @property
    def bridge(self) -> UnityBridge | None:
        return self._bridge

    @property
    def connected(self) -> bool:
        return self._bridge is not None and self._bridge.connected

    @property
    def status(self) -> str:
        if self._bridge is None:
            return "disconnected"
        return self._bridge.status

    @property
    def port(self) -> int:
        return self._port

    def add_reconnect_callback(self, cb) -> None:
        """Register a callback to be wired on every new bridge (survives reconnect)."""
        self._reconnect_callbacks.append(cb)
        if self._bridge is not None:
            self._bridge.add_reconnect_callback(cb)

    async def connect(self, port: int, host: str = "127.0.0.1") -> str:
        if self._bridge is not None:
            self._bridge.stop_heartbeat()
            await self._bridge.close()
        self._bridge = UnityBridge(host, port, port_discoverer=self._port_discoverer,
                                    is_retry_safe=self._is_retry_safe)
        for cb in self._reconnect_callbacks:
            self._bridge.add_reconnect_callback(cb)

        bridge_ref = self._bridge
        def _sync_port():
            if bridge_ref._port != self._port:
                old = self._port
                self._port = bridge_ref._port
                if self._on_port_change:
                    self._on_port_change(old, self._port)
        self._bridge.add_reconnect_callback(_sync_port)

        try:
            await self._bridge.connect()
            self._port = port
            self._host = host
            self._bridge.start_heartbeat()
            return f"Connected to Unity on port {port}"
        except OSError:
            return f"Registered Unity on port {port} (not yet available)"

    async def close(self):
        if self._bridge:
            self._bridge.stop_heartbeat()
            await self._bridge.close()
            self._bridge = None
