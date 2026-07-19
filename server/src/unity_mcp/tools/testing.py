"""Unity Test Runner orchestration (B2: split from scene.py)."""
import asyncio
from ._annotations import RO as _RO, RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None

# STALE-DOMAIN: defensive — unreachable with expected_compile=False, guards future callers
_BLOCK_STARTS = (
    "FAIL:", "BUILD-FAILED-WEDGE", "STALE-CACHE",
    "STALE-DOMAIN", "STALE-TRANSIENT", "WEDGE-ENGINE", "WEDGE-STATE",
    "REBUILDING", "TESTS-INVISIBLE",
)


async def run_tests(mode: str = "EditMode", filter: str | None = None) -> str:
    """Start Unity tests — returns immediately (for Play Mode scenario testing use `run_playtest`). mode: EditMode or PlayMode. filter: pipe-separated test names. Poll get_test_results every 5s for results."""
    from mcp.server.fastmcp.exceptions import ToolError as _ToolError
    _MAX_PREFLIGHT_RETRIES = 2
    # Verdicts where force_refresh can help; everything else blocks immediately
    _RECOVERABLE = ("FAIL:stale-dll", "FAIL:unknown", "STALE-CACHE", "STALE-TRANSIENT")
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


async def run_tests_wait(
    mode: str = "EditMode",
    filter: str = "",
    timeout: float = 180.0,
    poll_interval: float = 5.0,
) -> str:
    """Start Unity tests and block until completion. Returns final result, 'TIMEOUT: <last>', or 'BLOCKED: <reason>'.
    mode: 'EditMode' or 'PlayMode'. filter: pipe-separated test class names. timeout: max seconds. poll_interval: seconds between polls."""
    result = await run_tests(mode, filter or None)
    if not result.startswith("tests-started"):
        return result

    last = result
    max_polls = max(1, int(timeout / poll_interval))
    for _ in range(max_polls):
        await asyncio.sleep(poll_interval)
        try:
            last = await get_test_results()
        except Exception:
            last = "pending"
        if last not in ("pending", "none"):
            return last

    return f"TIMEOUT: {last}"


async def get_test_results() -> str:
    """Poll for test results after PlayMode run. Returns results, 'pending', or 'none'."""
    try:
        return await _send("get_test_results", {})
    except Exception:
        return "pending"


async def get_test_progress() -> str:
    """Poll real-time test progress. Returns running|ran|passed|failed|skipped|total|elapsed|eta or 'idle'."""
    try:
        return await _send("get_test_progress", {})
    except Exception:
        return "pending"


async def get_test_count() -> str:
    """Number of edit-mode and play-mode tests in the project."""
    return await _send("get_test_count", {})


async def run_playtest_file(path: str) -> str:
    """REMOVED in v0.85. Use: run_playtest(path='...')"""
    from mcp.server.fastmcp.exceptions import ToolError
    raise ToolError(f"run_playtest_file removed in v0.85. Use: run_playtest(path='{path}')")


async def get_perf() -> str:
    """REMOVED in v0.85. Use: get_frame_stats"""
    from mcp.server.fastmcp.exceptions import ToolError
    raise ToolError("get_perf removed in v0.85. Use: get_frame_stats")


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW_IDEM)(run_tests)
    mcp.tool(annotations=_RW_IDEM)(run_tests_wait)
    mcp.tool(annotations=_RO)(get_test_results)
    mcp.tool(annotations=_RO)(get_test_progress)
    mcp.tool(annotations=_RO)(get_test_count)
    # MCP091-011: register deprecated stubs so MCP returns ToolError+hint instead of "tool not found"
    # DEPRECATED category excluded from filter_by_tier → invisible in ListTools, but callable by name
    mcp.tool(annotations=_RO)(get_perf)
    mcp.tool(annotations=_RO)(run_playtest_file)
