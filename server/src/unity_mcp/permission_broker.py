"""Permission broker — stateless mode-aware policy for MCP permission gates.

Used by permission_prompt_tool to enforce chat mode restrictions.
External MCP clients (no UNITY_MCP_CHAT_MODE env var) bypass all enforcement.
"""
from __future__ import annotations

import logging
import os
import uuid
from dataclasses import dataclass

from .middleware_types import BLAST_RADIUS, WRITE_CMDS

_log = logging.getLogger(__name__)

# Commands with blast_radius >= 4 are "dangerous" — denied even in agent mode.
_DANGEROUS_CMDS: frozenset[str] = frozenset(
    cmd for cmd, r in BLAST_RADIUS.items() if r >= 4
)


@dataclass
class PermissionDecision:
    outcome: str      # "deny" | "allow_by_saved_policy"
    decision_id: str  # uuid4 for audit trail
    reason: str


def _allow(did: str, reason: str) -> PermissionDecision:
    return PermissionDecision("allow_by_saved_policy", did, reason)


def _deny(did: str, reason: str) -> PermissionDecision:
    _log.warning("permission denied | decision_id=%s | reason=%s", did, reason)
    return PermissionDecision("deny", did, reason)


class PermissionBroker:
    """Stateless policy: mode + tool_cmd → allow/deny.

    mode=None means external MCP client (not spawned by relay) → allow all.
    """

    def __init__(self, mode: str | None) -> None:
        self._mode = mode

    def decide(self, tool_cmd: str) -> PermissionDecision:
        did = str(uuid.uuid4())

        if not self._mode:
            return _allow(did, "external client: no mode restriction")

        mode = self._mode

        if mode not in ("ask", "agent", "full-access"):
            return _deny(did, f"unknown mode '{mode}': denied by default")

        if mode == "full-access":
            return _allow(did, "full-access mode")

        # agent: allow writes, deny dangerous
        if mode == "agent":
            if tool_cmd in _DANGEROUS_CMDS:
                return _deny(did, f"agent mode: '{tool_cmd}' is dangerous — use full-access mode")
            return _allow(did, "agent mode")

        # ask: deny writes + dangerous, allow reads
        if tool_cmd in WRITE_CMDS or tool_cmd in _DANGEROUS_CMDS:
            return _deny(did, f"ask mode: '{tool_cmd}' requires agent mode for mutations")
        return _allow(did, "ask mode: read-only tool")


# Module-level singleton — tests override via monkeypatch.setattr(mod, "_broker", ...)
_broker: PermissionBroker = PermissionBroker(mode=os.environ.get("UNITY_MCP_CHAT_MODE"))
