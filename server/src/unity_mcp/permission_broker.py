"""Permission broker — stateless mode-aware policy for MCP permission gates.

Used by permission_prompt_tool to enforce chat mode restrictions.
External MCP clients (no UNITY_MCP_CHAT_MODE env var) bypass all enforcement.
"""

import logging
import os
import uuid
from dataclasses import dataclass

from .decisions.generated.permission_logic import (
    ChatPermissionInput,
    ChatPermissionValues,
    chat_permission,
)
from .middleware_types import BLAST_DANGEROUS, BLAST_RADIUS, WRITE_CMDS

_log = logging.getLogger(__name__)

# Commands with blast_radius >= BLAST_DANGEROUS are denied even in agent mode.
_DANGEROUS_CMDS: frozenset[str] = frozenset(
    cmd for cmd, r in BLAST_RADIUS.items() if r >= BLAST_DANGEROUS
)

# chat.mode of dsl/decisions/permission.logic uses enum literals, not our wire
# strings ("full-access" has a hyphen; None/"" means no chat token at all).
_CHAT_MODE: dict[str, str] = {"ask": "ask", "agent": "agent", "full-access": "fullAccess"}


def _chat_mode(mode: str | None) -> str:
    if not mode:
        return "external"
    return _CHAT_MODE.get(mode, "unknown")


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
        mode = self._mode
        chat_mode = _chat_mode(mode)

        # SV2/Q15: one rule (dsl/decisions/permission.logic), shared with C#
        # SessionAuthorization.Check. The facts below are each side's own —
        # that divergence (static WRITE_CMDS here vs args-aware IsMutating in
        # C#) is INTENDED per Q15, not something this decision table hides.
        outcome = chat_permission(ChatPermissionInput(
            call_dangerous=tool_cmd in _DANGEROUS_CMDS,
            call_mutating=tool_cmd in WRITE_CMDS,
            chat_mode=chat_mode,
        ))

        if outcome == ChatPermissionValues.ALLOW:
            if chat_mode == "external":
                return _allow(did, "external client: no mode restriction")
            if chat_mode == "fullAccess":
                return _allow(did, "full-access mode")
            if chat_mode == "agent":
                return _allow(did, "agent mode")
            return _allow(did, "ask mode: read-only tool")
        if outcome == ChatPermissionValues.DENY_UNKNOWN_MODE:
            return _deny(did, f"unknown mode '{mode}': denied by default")
        if outcome == ChatPermissionValues.DENY_DANGEROUS:
            return _deny(did, f"agent mode: '{tool_cmd}' is dangerous — use full-access mode")
        return _deny(did, f"ask mode: '{tool_cmd}' requires agent mode for mutations")


# Module-level singleton — tests override via monkeypatch.setattr(mod, "_broker", ...)
_broker: PermissionBroker = PermissionBroker(mode=os.environ.get("UNITY_MCP_CHAT_MODE"))
