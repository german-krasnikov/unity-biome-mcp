"""Scene Brief — P2: proactive scene summary injected on first tool call.

Enable: UNITY_MCP_SCENE_BRIEF=1
"""
import os
from collections.abc import Awaitable, Callable  # noqa: TC003

from .console_levels import PROBLEM_LEVELS

META_CMDS = {"list_connections", "reconnect_unity",
             "discover_tools", "get_enabled_tools", "ping"}


class SceneBrief:
    def __init__(self):
        self.brief: str | None = None
        self._injected: bool = False

    @property
    def enabled(self) -> bool:
        return os.environ.get("UNITY_MCP_SCENE_BRIEF") == "1"

    def should_inject(self, cmd: str) -> bool:
        return not self._injected and self.brief is not None and cmd not in META_CMDS

    def mark_injected(self) -> None:
        self._injected = True

    def reset(self) -> None:
        self.brief = None
        self._injected = False

    async def ensure(self, send_raw: Callable[..., Awaitable[str]]) -> str | None:
        """Return cached brief from raw bridge data (capped 2000 chars). None when disabled."""
        if not self.enabled:
            return None
        if self.brief:
            return self.brief

        try:
            hierarchy = await send_raw("get_hierarchy", {"summary": "true"})
            console = await send_raw("get_console", {"count": "5", "level": PROBLEM_LEVELS})
            state = await send_raw("editor", {"action": "state"})
        except Exception:
            return None

        data = f"HIERARCHY:\n{hierarchy}\n\nCONSOLE:\n{console}\n\nSTATE:\n{state}"
        self.brief = data[:2000]  # raw, capped — no LLM call in T21
        return self.brief
