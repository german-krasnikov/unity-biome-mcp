"""AskExecutor — runs a ToolPlan's steps via _send."""

from collections.abc import Callable  # noqa: TC003

from .. import editor_log
from .plans import ToolPlan  # noqa: TC001


class AskExecutor:
    def __init__(self, send: Callable):
        self._send = send

    async def run(self, plan: ToolPlan) -> list[str]:
        """Execute all steps in plan, return list of raw results."""
        results = []
        for tool, args in plan.steps:
            try:
                result = await self._send(tool, args)
                if tool == "get_compile_errors":
                    result = editor_log.corroborate(str(result))
                results.append(str(result))
            except Exception as e:  # noqa: PERF203
                results.append(f"ERROR: {e}")
        return results
