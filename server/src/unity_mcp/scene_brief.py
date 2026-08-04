"""Scene Brief — P2: proactive scene summary via Haiku on first tool call.

Enable: UNITY_MCP_SCENE_BRIEF=1
"""
import os
from collections.abc import Awaitable, Callable

from .console_levels import PROBLEM_LEVELS
from .sampling import SamplingService

META_CMDS = {"list_connections", "reconnect_unity",
             "discover_tools", "get_enabled_tools", "ping"}

_SUMMARY_PROMPT = (
    "Describe this Unity scene in 100 tokens for an AI assistant. "
    "Include: object count, key patterns, errors, play state."
)


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
        """Return cached brief, or generate one via Haiku. Returns None when disabled."""
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

        scene_count = hierarchy.count("\n[")
        if hierarchy.startswith("["):
            scene_count += 1
        multi_hint = f" Multi-scene ({scene_count} loaded)." if scene_count > 1 else ""
        prompt = (
            f"Describe this Unity scene in 100 tokens for an AI assistant.{multi_hint} "
            "Include: object count, key patterns, errors, play state."
        )

        svc = SamplingService()
        if not svc.enabled:
            return None

        summary = await svc.summarize(data, prompt)
        if summary:
            self.brief = summary
        return self.brief
