"""Unity Test Runner orchestration (B2: split from scene.py)."""
import asyncio
from ._annotations import RO as _RO, RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None

# STALE-DOMAIN: defensive — unreachable with expected_compile=False, guards future callers
_BLOCK_STARTS = (
    "FAILED:", "BUILD-FAILED-WEDGE", "STALE-CACHE",
    "STALE-DOMAIN", "STALE-TRANSIENT", "WEDGE-ENGINE", "WEDGE-STATE",
    "REBUILDING", "TESTS-INVISIBLE",
)


async def run_tests(mode: str = "EditMode", filter: str | None = None) -> str:
    """Start Unity tests (returns immediately). mode: EditMode or PlayMode. filter: pipe-separated test names. Poll get_test_results every 5s for results."""
    from mcp.server.fastmcp.exceptions import ToolError as _ToolError
    _MAX_PREFLIGHT_RETRIES = 2
    # Verdicts where force_refresh can help; everything else blocks immediately
    _RECOVERABLE = ("FAILED:stale-dll", "FAILED:unknown", "STALE-CACHE", "STALE-TRANSIENT")
    try:
        from . import diagnose as _diag
        for _attempt in range(_MAX_PREFLIGHT_RETRIES + 1):
            verdict = await _diag.diagnose(prev_mvid="", expected_compile=False)
            if not verdict.startswith(_BLOCK_STARTS):
                break  # clean, proceed
            if not verdict.startswith(_RECOVERABLE):
                return f"BLOCKED: {verdict} — fix domain state before running tests"
            if _attempt >= _MAX_PREFLIGHT_RETRIES:
                return f"BLOCKED: {verdict} — auto-recovery exhausted after {_MAX_PREFLIGHT_RETRIES} attempts"
            # Auto-recovery attempt
            try:
                await _send("force_refresh", {})
            except Exception:
                pass
            await asyncio.sleep(10)
    except _ToolError:
        raise  # compile guard / connection-dead ToolErrors must propagate
    except Exception:
        pass  # diagnose unavailable — degrade gracefully

    args = {"mode": mode}
    if filter:
        args["filter"] = filter
    try:
        result = await _send("run_tests", args, timeout=8.0)
        if result and result not in ("pending", "none"):
            return result
    except Exception:
        pass  # TCP died / domain reload expected — return fire-and-forget ack
    return f"tests-started|{mode}|poll get_test_results every 5s for up to 2min"


async def get_test_results() -> str:
    """Poll for test results after PlayMode run. Returns results, 'pending', or 'none'."""
    return await _send("get_test_results", {})


async def get_test_count() -> str:
    """Number of edit-mode and play-mode tests in the project."""
    return await _send("get_test_count", {})


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW_IDEM)(run_tests)
    mcp.tool(annotations=_RO)(get_test_results)
    mcp.tool(annotations=_RO)(get_test_count)
