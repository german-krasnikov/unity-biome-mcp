"""Executor for do() tool — runs batch + 1 retry on partial failure."""
import re
from typing import Optional, Callable, Set
from ..sampling_postproc import normalize
from .validator import validate_plan

# Matches '[N] err: ...' (actual Unity bridge batch format)
_ERR_LINE_RE = re.compile(r"^\[\d+\]\s+err:", re.IGNORECASE)


def _count_failures(result: str) -> list[str]:
    """Extract failed lines from batch result — matches '[N] err:' format."""
    failed = []
    for line in result.splitlines():
        if _ERR_LINE_RE.match(line):
            failed.append(line)
    return failed


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
        scene_paths: Optional[Set[str]] = None,
    ) -> str:
        result = await self._send("batch", {"commands": plan, "on_error": "continue"})
        result_str = str(result)

        # Retry once on partial failure (≤5 failed lines)
        if _is_partial(result_str) and self._svc is not None:
            failed_lines = _count_failures(result_str)
            if 0 < len(failed_lines) <= 5:
                fix_prompt = (
                    f"Fix these failed Unity Biome MCP commands (output only fixed commands, one per line):\n"
                    f"Original intent: {original_intent}\n"
                    f"Failed:\n" + "\n".join(failed_lines)
                )
                fixed = await self._svc.generate(fix_prompt)
                fixed, _ = normalize(fixed, "dsl")
                if fixed:
                    # Validate retry plan with enriched paths (include objects declared in original plan)
                    validate_plan(plan, scene_paths or set())  # side-effect: builds declared_paths
                    from ..utils import parse_kv_line as _pkv
                    enriched: set[str] = set(scene_paths or set())
                    for ln in plan.strip().splitlines():
                        cmd, kv = _pkv(ln.strip())
                        if cmd == "create_object":
                            nm = kv.get("name", "")
                            par = kv.get("parent", "")
                            p_norm = (par if par.startswith("/") else f"/{par}") if par else ""
                            enriched.add(f"{p_norm}/{nm}" if p_norm else f"/{nm}")
                    err = validate_plan(fixed, enriched)
                    if err:
                        return result_str  # validation failed — return original result
                    retry = await self._send("batch", {"commands": fixed, "on_error": "continue"})
                    return str(retry)

        return result_str
