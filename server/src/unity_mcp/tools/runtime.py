"""Runtime Play Mode tools — blocked outside Play Mode by Unity guard."""
from ._annotations import RO as _RO, RW as _RW, RW_IDEM as _RW_IDEM
from ..sampling import SamplingService
from ._common import bind

_send = None
_args = None


async def invoke_method(path: str, component: str, method: str, args: str = "") -> str:
    """[Play Mode] Call public method on a component via reflection.
    args: comma-separated values matching method parameters.
    Example: invoke_method('/Player', 'PlayerController', 'MoveTo', '10,0,5')"""
    return await _send("invoke_method", _args(
        path=path, component=component, method=method, args=args or None))


async def set_runtime_property(path: str, component: str, field: str, value: str) -> str:
    """[Play Mode] Set field/property via reflection. Safe — doesn't use SerializedObject. For Edit Mode serialized mutations, use `set_property`."""
    return await _send("set_runtime_property", _args(
        path=path, component=component, field=field, value=value))


async def wait_until(path: str, component: str, field: str, value: str,
                     timeout: float = 5.0, negate: bool = False,
                     abort_on_fail: bool = False) -> str:
    """[Play Mode] Poll field until it matches value (or timeout).
    Python timeout = Unity timeout + 5s buffer.
    abort_on_fail=True: stops Play Mode on timeout."""
    return await _send("wait_until", _args(
        path=path, component=component, field=field, value=value,
        timeout=str(timeout),
        negate="true" if negate else None,
        abort_on_fail="true" if abort_on_fail else None),
        timeout=timeout + 5.0)


async def move_to(path: str, position: str, timeout: float = 15.0) -> str:
    """[Play Mode] Move character to position and wait for arrival.
    path: scene path to GO with movement component.
    position: x,y,z (e.g. '5,0,-3'). Returns 'arrived' or 'blocked'."""
    return await _send("move_to", _args(
        path=path, position=position, timeout=str(timeout)),
        timeout=timeout + 5.0)


async def query_state(queries: str) -> str:
    """[Play Mode] Snapshot multiple game values in one call.
    queries: comma-separated 'path|component|field_or_method' triplets.
    Example: query_state('/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX')"""
    return await _send("query_state", _args(queries=queries), timeout=10.0)


async def test_step(path: str, position: str,
                    checks_before: str = "", checks_after: str = "",
                    wait_after: float = 0.5, timeout: float = 15.0) -> str:
    """[Play Mode] Move character, snapshot state before/after, check console.
    checks_before/after: comma-separated 'path|component|field' triplets.
    Returns structured BEFORE/MOVE/AFTER/CONSOLE report."""
    return await _send("test_step", _args(
        path=path, position=position,
        checks_before=checks_before or None,
        checks_after=checks_after or None,
        wait_after=str(wait_after),
        timeout=str(timeout)), timeout=timeout + 10.0)


test_step.__test__ = False  # prevent pytest from collecting as test


def _compress_report(report: str) -> str:
    """Strip passing step lines, keep failures/snapshots/logs."""
    lines = report.split('\n')
    if len(lines) <= 2:
        return report
    keep = [lines[0]]
    for line in lines[1:]:
        s = line.strip()
        if not s:
            continue
        if s.startswith('[') and ('— PASS' in s or '— done' in s or '— ok' in s):
            continue
        keep.append(line)
    return '\n'.join(keep) if len(keep) > 1 else keep[0]


async def run_playtest(script: str, timeout: float = 120.0,
                       abort_on_fail: bool = False,
                       defs: "str | None" = None) -> str:
    """[Play Mode] Execute a playtest DSL script. Returns structured report (for NUnit test suite, use `run_tests`).
    Commands: MOVE TO x,y,z | WAIT n | WAIT_UNTIL query op value | ASSERT query op value |
    ASSERT_CONSOLE_CLEAN [IGNORE "pat1","pat2"] | SNAPSHOT queries | INVOKE path comp method args |
    SET path comp field value | LOG msg | TIMESCALE n | ASSERT_CONSERVED SUM a+b OVER t |
    ASSERT_CTA VISIBLE|CLICKABLE | VAL name query | TELEPORT path x,y,z |
    ASSERT_BATCH...END | ASSERT_NEAR pathA pathB dist | CAPTURE label query |
    ASSERT_CAPTURED label INCREASED|DECREASED | INVARIANT query op value |
    SIMULATE name [DURATION n] [TIMESCALE n] | MONITOR name | TRACE_FLOW FROM a TO b FIELD f.
    Queries use aliases from PlaytestConfig.asset or pipe format: path|component|field
    defs: inline VAL definitions ('name path|comp|field' per line), prepended to script.
    abort_on_fail=True: stops Play Mode on step timeout."""
    if defs:
        alias_lines = []
        for line in defs.strip().split("\n"):
            stripped = line.strip()
            if not stripped or stripped.startswith("#"):
                continue
            if not stripped.upper().startswith("VAL "):
                stripped = "VAL " + stripped
            elif not stripped.startswith("VAL "):
                stripped = "VAL " + stripped[4:]  # normalize to uppercase
            alias_lines.append(stripped)
        script = "\n".join(alias_lines) + "\n" + script
    raw = await _send("run_playtest", _args(
        script=script, timeout=str(timeout),
        abort_on_fail="true" if abort_on_fail else None),
                      timeout=timeout + 20.0)
    compressed = _compress_report(raw)
    if len(compressed) > 300:
        svc = SamplingService()
        if svc.enabled:
            summary = await svc.summarize(
                compressed,
                "Summarize playtest report. Line 1: X/Y result. Line 2+: only failures with cause.",
            )
            if summary:
                return summary
    return compressed


run_playtest.__test__ = False  # prevent pytest from collecting as test


async def fuzz_playtest(steps: int = 10, seed: int | None = None) -> str:
    """Generate and run a random playtest DSL script. Finds hidden bugs via property-based testing.
    steps: number of random actions to generate. seed: for reproducibility."""
    from ..fuzzer import generate_script
    script = generate_script(steps, seed)
    return await _send("run_playtest", {"script": script, "timeout": "30"}, timeout=40.0)


fuzz_playtest.__test__ = False  # prevent pytest from collecting as test


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(invoke_method)
    mcp.tool(annotations=_RW_IDEM)(set_runtime_property)
    mcp.tool(annotations=_RW_IDEM)(wait_until)
    mcp.tool(annotations=_RW)(move_to)
    mcp.tool(annotations=_RO)(query_state)
    mcp.tool(annotations=_RW)(test_step)
    mcp.tool(annotations=_RW)(run_playtest)
    mcp.tool(annotations=_RW)(fuzz_playtest)
