"""Production seam factories (wiring only) shared by the T3/T4 live harness
in scripts/run_ab_reload_identity.py. None of this is exercised by the
offline tests -- every offline test injects a fake in its place. See
Plans/N3-T3-T4-live-reload-identity.md.
"""

import contextlib

from gauntlet.ab_reload_identity import COUNTER_INCREMENT_CODE, BridgeFactory, BridgeLike


def live_sync_unity(host: str):
    """Production sync_unity bound to a bridge pinned at (port, project_path)
    -- mirrors server/tests/mutation/_canary.py::make_raw_send, which
    server/tests/mutation/conftest.py::mutation_sdk wires the same way.
    sync_tool internals (sync_algorithm._parse_stamp et al.) parse _send's
    return value as a raw wire-format string; UnityBridge.send() returns the
    unwrapped {"ok":..., "data":...} dict, so _send must unwrap it first --
    binding bridge.send directly crashes str-only parsing on the first real
    call (AttributeError: 'dict' object has no attribute 'split').

    Also passes is_retry_safe like server/tests/mutation/_canary.py::make_bridge:
    without it, UnityBridge treats EVERY command (including sync_status,
    sync_unity's own first probe) as unsafe to resend after a SENT-but-
    uncertain delivery, raising UncertainDeliveryError on a live connection
    hiccup that a harmless read should just retry."""
    async def run(port: int, project_path: str, *, timeout: float = 120.0) -> str:
        from mcp.server.fastmcp.exceptions import ToolError

        from unity_mcp.bridge import UnityBridge
        from unity_mcp.bridge_result import unwrap_bridge_result
        from unity_mcp.timeout_categories import get_timeout
        from unity_mcp.tools import sync as sync_tool
        from unity_mcp.tools._annotations import _INTERNAL_RETRY_SAFE_CMDS

        bridge = UnityBridge(
            host, port, expected_project_path=project_path,
            is_retry_safe=lambda cmd: cmd in _INTERNAL_RETRY_SAFE_CMDS,
        )
        await bridge.connect()

        async def _send_raw_like(cmd: str, args: dict, timeout: float = 0) -> str:
            result = await bridge.send(cmd, args, timeout=timeout or get_timeout(cmd))
            text, ok = unwrap_bridge_result(result)
            if not ok:
                raise ToolError(text)
            return text

        previous_send = sync_tool._send
        sync_tool._send = _send_raw_like
        try:
            return await sync_tool.sync_unity(timeout=timeout)
        finally:
            sync_tool._send = previous_send
            with contextlib.suppress(Exception):
                await bridge.close()

    return run


def live_bridge_factory(host: str) -> BridgeFactory:
    """Production bridge factory: a real UnityBridge per probe_identity() call.
    is_retry_safe matches live_sync_unity's -- probe_identity only connects/
    closes today, but a bare UnityBridge defaults every command to unsafe,
    so this keeps the two production bridges consistent."""
    def factory(port: int, expected_project_path: str) -> BridgeLike:
        from unity_mcp.bridge import UnityBridge
        from unity_mcp.tools._annotations import _INTERNAL_RETRY_SAFE_CMDS

        return UnityBridge(
            host, port, expected_project_path=expected_project_path,
            is_retry_safe=lambda cmd: cmd in _INTERNAL_RETRY_SAFE_CMDS,
        )

    return factory


def live_send_increment_via_proxy(host: str, proxy_port: int, project_path: str):
    """T3 slice 3: execute_code counter increment sent through a UnityBridge
    pinned at the CounterProxy's listen port. UnityBridge classifies
    execute_code as unsafe-to-retry by default (is_retry_safe rejects every
    command unless told otherwise), so a dropped ACK after the frame was
    written and drained raises UncertainDeliveryError instead of silently
    resending -- this is the same default the mutation lost-ACK regression
    (server/tests/mutation/test_lost_ack_regression.py) relies on for
    'sync'."""
    async def run() -> None:
        from unity_mcp.bridge import UnityBridge

        bridge = UnityBridge(host, proxy_port, expected_project_path=project_path)
        try:
            await bridge.connect()
            await bridge.send("execute_code", {"code": COUNTER_INCREMENT_CODE})
        finally:
            with contextlib.suppress(Exception):
                await bridge.close()

    return run
