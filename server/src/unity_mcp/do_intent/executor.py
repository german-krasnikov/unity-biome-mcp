"""Executor for do() tool — runs batch + 1 retry on partial failure."""
import re
from collections.abc import Callable  # noqa: TC003

from ..decisions.planverify_facts import fix_apply, fix_gate
from ..sampling_postproc import normalize
from .validator import declared_paths, plan_verdict

# Matches '[N] err: ...' (actual Unity bridge batch format)
_ERR_LINE_RE = re.compile(r"^\[\d+\]\s+err:", re.IGNORECASE)


def _count_failures(result: str) -> list[str]:
    """Extract failed lines from batch result — matches '[N] err:' format."""
    return [line for line in result.splitlines() if _ERR_LINE_RE.match(line)]


def _is_partial(result: str) -> bool:
    """True if result contains any err lines or legacy PARTIAL: marker."""
    if "PARTIAL:" in result:
        return True
    return any(_ERR_LINE_RE.match(l) for l in result.splitlines())


class Executor:
    def __init__(self, send: Callable, sampling=None):
        self._send = send
        self._svc = sampling

    async def execute(
        self,
        plan: str,
        original_intent: str = "",
        scene_paths: set[str] | None = None,
    ) -> str:
        result = await self._send("batch", {"commands": plan, "on_error": "continue"})
        result_str = str(result)

        failed_lines = _count_failures(result_str)
        gate = fix_gate(partial=_is_partial(result_str), has_sampling=self._svc is not None, failed_lines=len(failed_lines))
        if gate != "ask_fix":
            return result_str

        fix_prompt = (
            f"Fix these failed Unity Biome MCP commands (output only fixed commands, one per line):\n"
            f"Original intent: {original_intent}\n"
            f"Failed:\n" + "\n".join(failed_lines)
        )
        fixed = await self._svc.generate(fix_prompt)
        fixed, _ = normalize(fixed, "dsl")

        verdict = "ok"
        if fixed:
            enriched = declared_paths(plan, scene_paths or set())
            verdict, _ = plan_verdict(fixed, enriched)

        if fix_apply(empty=not fixed, verdict=verdict) != "send_fix":
            return result_str

        retry = await self._send("batch", {"commands": fixed, "on_error": "continue"})
        return str(retry)
