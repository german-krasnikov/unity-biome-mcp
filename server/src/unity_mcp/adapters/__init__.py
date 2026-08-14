"""Public API for the adapters package."""
from __future__ import annotations

import os

from .fixture import FixtureAdapter
from .legacy import LegacyCliAdapter
from .protocol import AgentAdapter, EventContext

__all__ = [
    "AgentAdapter", "EventContext", "LegacyCliAdapter", "FixtureAdapter",
    "AcpAgentAdapter", "make_opencode_adapter",
    "CodexAcpAdapter", "make_codex_adapter",
    "ClaudeAcpAdapter", "make_claude_adapter",
]

_ACP_OPENCODE_FLAG = "UNITY_MCP_ACP_OPENCODE"
_ACP_CODEX_FLAG    = "UNITY_MCP_ACP_CODEX"
_ACP_CLAUDE_FLAG   = "UNITY_MCP_ACP_CLAUDE"


def make_opencode_adapter(backend: object, broker: object) -> AgentAdapter:
    """Return AcpAgentAdapter if UNITY_MCP_ACP_OPENCODE is set, else LegacyCliAdapter."""
    if os.environ.get(_ACP_OPENCODE_FLAG):
        from .acp import AcpAgentAdapter  # lazy: only when flag is set
        return AcpAgentAdapter(backend, broker)  # type: ignore[arg-type]
    return LegacyCliAdapter(backend)  # type: ignore[arg-type]


def make_codex_adapter(backend: object, broker: object) -> AgentAdapter:
    """Return CodexAcpAdapter if UNITY_MCP_ACP_CODEX is set, else LegacyCliAdapter."""
    if os.environ.get(_ACP_CODEX_FLAG):
        from .codex_acp import CodexAcpAdapter  # lazy: only when flag is set
        return CodexAcpAdapter(backend, broker)  # type: ignore[arg-type]
    return LegacyCliAdapter(backend)  # type: ignore[arg-type]


def make_claude_adapter(backend: object, broker: object) -> AgentAdapter:
    """Return ClaudeAcpAdapter if UNITY_MCP_ACP_CLAUDE is set, else LegacyCliAdapter.

    backend: used only when flag is unset (legacy path).
    """
    if os.environ.get(_ACP_CLAUDE_FLAG):
        from ..backend_def import BACKENDS
        from .claude_acp import ClaudeAcpAdapter  # lazy: only when flag is set
        return ClaudeAcpAdapter(BACKENDS["claude_acp"], broker)  # type: ignore[arg-type]
    return LegacyCliAdapter(backend)  # type: ignore[arg-type]


def __getattr__(name: str) -> object:
    if name == "AcpAgentAdapter":
        from .acp import AcpAgentAdapter
        return AcpAgentAdapter
    if name == "CodexAcpAdapter":
        from .codex_acp import CodexAcpAdapter
        return CodexAcpAdapter
    if name == "ClaudeAcpAdapter":
        from .claude_acp import ClaudeAcpAdapter
        return ClaudeAcpAdapter
    raise AttributeError(f"module {__name__!r} has no attribute {name!r}")
