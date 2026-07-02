"""do() meta-tool: NL intent → Haiku plan → validate → batch execute."""
from typing import Optional
from mcp.server.fastmcp.exceptions import ToolError
from ..sampling import sampling_service as _sampling
from ..do_intent.planner import Planner
from ..do_intent.validator import validate_plan
from ..do_intent.executor import Executor
from ._annotations import RW as _RW
from .intent_common import sanitize_intent
from ._common import bind

# Module-level references — patched in tests
_send = None


async def _get_scene_brief() -> str:
    """Fetch minimal scene context for planner prompt."""
    if _send is None:
        return ""
    try:
        return await _send("get_hierarchy", {"summary": "true"})
    except Exception:
        return ""


async def do(intent: str, dry_run: bool = False) -> str:
    """Convert natural language intent into Unity scene operations.

    Haiku generates a batch DSL plan, which is validated then executed.
    dry_run=True returns the plan without executing it.
    """
    intent = sanitize_intent(intent)
    scene_brief = await _get_scene_brief()

    planner = Planner(_sampling)
    plan = await planner.plan(intent, scene_brief)

    if not plan:
        raise ToolError("Haiku planning unavailable (set UNITY_MCP_VISUAL_VERIFY=1)")

    # Extract scene paths from hierarchy for validator
    scene_paths = _extract_paths(scene_brief)
    err = validate_plan(plan, scene_paths)
    if err:
        raise ToolError(f"INVALID PLAN: {err}")

    if dry_run:
        return f"DRY RUN plan:\n{plan}"

    executor = Executor(_send, sampling=_sampling)
    result = await executor.execute(plan, original_intent=intent, scene_paths=scene_paths)

    # Build summary
    lines = [l for l in plan.strip().splitlines() if l.strip()]
    return f"do: {len(lines)} ops\n{result}"


def _extract_paths(hierarchy: str) -> set[str]:
    """Extract /paths from hierarchy text (best-effort)."""
    paths: set[str] = set()
    for line in hierarchy.splitlines():
        stripped = line.strip().lstrip("!").strip()
        if stripped.startswith("/"):
            paths.add(stripped.split()[0])
    return paths


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(do)
