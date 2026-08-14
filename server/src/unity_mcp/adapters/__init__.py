"""Public API for the adapters package."""
from __future__ import annotations

from .fixture import FixtureAdapter
from .protocol import AgentAdapter, EventContext

__all__ = [
    "AgentAdapter", "EventContext", "FixtureAdapter",
    "AcpAgentAdapter", "make_opencode_adapter",
    "CodexAcpAdapter", "make_codex_adapter",
    "ClaudeAcpAdapter", "make_claude_adapter",
]


def make_opencode_adapter(backend: object, broker: object) -> AgentAdapter:
    """Return AcpAgentAdapter (ACP-only: no legacy fallback)."""
    from .acp import AcpAgentAdapter
    return AcpAgentAdapter(backend, broker)  # type: ignore[arg-type]


def make_codex_adapter(backend: object, broker: object) -> AgentAdapter:
    """Return CodexAcpAdapter (ACP-only: no legacy fallback)."""
    from .codex_acp import CodexAcpAdapter
    return CodexAcpAdapter(backend, broker)  # type: ignore[arg-type]


def make_claude_adapter(backend: object, broker: object) -> AgentAdapter:
    """Return ClaudeAcpAdapter (ACP-only: no legacy fallback)."""
    from ..backend_def import BACKENDS
    from .claude_acp import ClaudeAcpAdapter
    return ClaudeAcpAdapter(BACKENDS["claude_acp"], broker)  # type: ignore[arg-type]


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
