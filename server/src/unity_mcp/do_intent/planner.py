"""Haiku planner for do() tool — converts intent to batch DSL."""

from .prompt import build_prompt


class Planner:
    def __init__(self, sampling_service):
        self._svc = sampling_service

    async def plan(self, intent: str, scene_brief: str) -> str | None:
        """Generate batch DSL plan from intent. Returns plan text or None."""
        prompt = build_prompt(intent, scene_brief)
        return await self._svc.generate(prompt)
