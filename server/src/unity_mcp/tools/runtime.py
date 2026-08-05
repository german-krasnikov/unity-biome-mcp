"""Runtime Play Mode tools — blocked outside Play Mode by Unity guard."""
import contextlib

from ..sampling import sampling_service as _sampling
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._annotations import RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None

_TCP_POLL_BUFFER = 5.0
_TCP_STEP_BUFFER = 10.0
_TCP_PLAYTEST_BUFFER = 20.0


async def invoke_method(path: str, component: str, method: str, args: str = "") -> str:
    """[Play Mode] Call public method on a component via reflection.
    args: comma-separated values matching method parameters.
    Example: invoke_method('/Player', 'PlayerController', 'MoveTo', '10,0,5')"""
    return await _send("invoke_method", _args(
        path=path, component=component, method=method, args=args or None))


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
        timeout=timeout + _TCP_POLL_BUFFER)


async def move_to(path: str, position: str, timeout: float = 15.0) -> str:
    """[Play Mode] Move character to position and wait for arrival.
    path: scene path to GO with movement component.
    position: x,y,z (e.g. '5,0,-3'). Returns 'arrived' or 'blocked'."""
    return await _send("move_to", _args(
        path=path, position=position, timeout=str(timeout)),
        timeout=timeout + _TCP_POLL_BUFFER)


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
        timeout=str(timeout)), timeout=timeout + _TCP_STEP_BUFFER)


test_step.__test__ = False  # prevent pytest from collecting as test


def _normalize_defs(defs: "str | None") -> "str | None":
    """Normalize defs: strip comments, ensure VAL prefix, return None if empty."""
    if not defs:
        return defs
    lines = []
    for line in defs.strip().split("\n"):
        s = line.strip()
        if not s or s.startswith("#"):
            continue
        s = "VAL " + (s[4:] if s.upper().startswith("VAL ") else s)
        lines.append(s)
    return "\n".join(lines) if lines else None


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


async def run_playtest(script: "str | None" = None, timeout: float = 120.0,
                       abort_on_fail: bool = False,
                       defs: "str | None" = None,
                       path: "str | None" = None,
                       snapshot_on_failure: bool = False,
                       fresh: bool = False) -> str:
    """[Play Mode] Execute a playtest DSL script. Returns structured report (for NUnit tests, use `run_tests`).
    Commands: MOVE TO x,y,z | WAIT n | WAIT_UNTIL query op value | ASSERT query op value |
    ASSERT_CONSOLE_CLEAN [IGNORE "pat"] | SNAPSHOT queries | INVOKE path comp method args |
    SET path comp field value | LOG msg | TIMESCALE n | ASSERT_CONSERVED SUM a+b OVER t |
    ASSERT_CTA VISIBLE|CLICKABLE | VAL name query | TELEPORT path x,y,z |
    ASSERT_BATCH...END | ASSERT_NEAR pathA pathB dist | INVARIANT query op value |
    SIMULATE name [DURATION n] [TIMESCALE n] | MONITOR name | TRACE_FLOW FROM a TO b FIELD f |
    CAPTURE label query | ASSERT_CAPTURED label INCREASED|DECREASED.
    defs: inline VAL definitions prepended to script."""
    if script and path:
        raise ValueError("script and path are mutually exclusive")
    if not script and not path:
        raise ValueError("script or path required")
    _fresh = "true" if fresh else None
    if path:
        raw = await _send("run_playtest", _args(
            path=path, timeout=str(timeout),
            abort_on_fail="true" if abort_on_fail else None,
            snapshot_on_failure="true" if snapshot_on_failure else None,
            fresh=_fresh,
            defs=_normalize_defs(defs), _explicit_path="true"),
                          timeout=timeout + _TCP_PLAYTEST_BUFFER)
    else:
        if defs:
            normalized = _normalize_defs(defs)
            if normalized:
                script = normalized + "\n" + script
        raw = await _send("run_playtest", _args(
            script=script, timeout=str(timeout),
            abort_on_fail="true" if abort_on_fail else None,
            snapshot_on_failure="true" if snapshot_on_failure else None,
            fresh=_fresh),
                          timeout=timeout + _TCP_PLAYTEST_BUFFER)
    compressed = _compress_report(raw)
    if len(compressed) > 300:
        svc = _sampling
        if svc.enabled:
            summary = await svc.summarize(
                compressed,
                "Summarize playtest report. Line 1: X/Y result. Line 2+: only failures with cause.",
            )
            if summary:
                return summary
    return compressed


run_playtest.__test__ = False  # prevent pytest from collecting as test


async def run_playtest_suite(
    pattern: "str | None" = None,
    suite_path: "str | None" = None,
    timeout_per_test: float = 120.0,
    stop_on_fail: bool = False,
    stop_after: bool = True,
    auto_play: bool = False,
    restart_between: bool = False,
) -> str:
    """[Play Mode] Run multiple .playtest files sequentially and return a compact matrix.
    pattern: glob pattern (e.g. 'Playtests/*.playtest'), comma-separated list,
             or newline-separated list of project-relative paths.
    suite_path: absolute path to a .suite file (lines = project-relative .playtest paths, # = comment).
    Exactly one of pattern or suite_path must be provided.
    stop_on_fail=True: abort suite after first failure.
    stop_after=True: exit Play Mode when suite completes.
    auto_play=True: enter Play Mode automatically if not already playing.
    restart_between=True: stop+play between each file to reset runtime state.
    Output: SUITE: X/Y passed (Zs) + per-file line + full failure details."""
    if pattern and suite_path:
        raise ValueError("pattern and suite_path are mutually exclusive")
    if auto_play:
        import asyncio as _asyncio
        state = await _send("editor", _args(action="state"), timeout=5.0)
        _lower = state.lower()
        if "state: playing" not in _lower and "state: paused" not in _lower:
            await _send("editor", _args(action="play"), timeout=5.0)
            for _ in range(15):
                await _asyncio.sleep(1.0)
                state = await _send("editor", _args(action="state"), timeout=5.0)
                _lower = state.lower()
                if "state: playing" in _lower or "state: paused" in _lower:
                    break

    if suite_path:
        import pathlib as _pathlib
        file_list = [l.strip() for l in
                     _pathlib.Path(suite_path).read_text(encoding="utf-8").splitlines()
                     if l.strip() and not l.strip().startswith("#")]
        if not file_list:
            return "SUITE: no files in suite"
    elif not pattern:
        raise ValueError("pattern or suite_path required")
    elif "*" in pattern or "?" in pattern:
        file_list_raw = await _send("list_playtest_files", _args(pattern=pattern), timeout=10.0)
        if file_list_raw.startswith("err:") or file_list_raw == "no files":
            return file_list_raw
        file_list = [f.strip() for f in file_list_raw.strip().split("\n") if f.strip()]
    else:
        sep = "," if "," in pattern else "\n"
        file_list = [f.strip() for f in pattern.split(sep) if f.strip()]

    if not file_list:
        return "SUITE: no files matched"

    import time as _time
    results = []
    suite_start = _time.monotonic()

    for filepath in file_list:
        if restart_between and results:  # not before first file
            import asyncio as _asyncio
            try:
                await _send("editor", _args(action="stop"), timeout=10.0)
                await _asyncio.sleep(1.0)
                await _send("editor", _args(action="play"), timeout=5.0)
                for _ in range(15):
                    await _asyncio.sleep(1.0)
                    s = await _send("editor", _args(action="state"), timeout=5.0)
                    if "state: playing" in s.lower():
                        break
            except Exception:
                pass  # best-effort
        t0 = _time.monotonic()
        raw = await _send("run_playtest", _args(
            path=filepath,
            timeout=str(timeout_per_test),
            abort_on_fail=None),
            timeout=timeout_per_test + _TCP_PLAYTEST_BUFFER)
        elapsed = _time.monotonic() - t0
        passed = raw.startswith("PLAYTEST:") and "FAIL" not in raw and "CONSOLE_ERR" not in raw
        results.append((filepath, raw, elapsed, passed))
        if stop_on_fail and not passed:
            break

    if stop_after:
        with contextlib.suppress(Exception):  # best-effort
            await _send("editor", _args(action="stop"), timeout=10.0)

    return _format_suite_report(results, _time.monotonic() - suite_start)


run_playtest_suite.__test__ = False  # prevent pytest from collecting as test


def _format_suite_report(results, total_elapsed):
    """Compact matrix: OK → one line. FAIL → one line with step info + full block."""
    import re as _re
    passed_count = sum(1 for _, _, _, ok in results if ok)
    total = len(results)
    lines = [f"SUITE: {passed_count}/{total} passed ({total_elapsed:.1f}s)"]
    failures = []
    for filepath, raw, elapsed, ok in results:
        name = filepath.rsplit("/", 1)[-1]
        if ok:
            lines.append(f"OK   {elapsed:5.1f}s  {name}")
        else:
            first_line = raw.split("\n")[0] if raw else ""
            step_part = ""
            if "/" in first_line:
                m = _re.search(r"(\d+/\d+)", first_line)
                if m:
                    step_part = f"  {m.group(1)}"
            lines.append(f"FAIL {elapsed:5.1f}s  {name}{step_part}")
            failures.append(f"\n--- {name} ---\n{raw}")
    lines.extend(failures)
    return "\n".join(lines)


async def lint_playtest(
    path: "str | None" = None,
    script: "str | None" = None,
) -> str:
    """Read-only preflight check on a .playtest file or inline script.
    Checks: unresolved $alias, deprecated ALIAS, TRACE_FLOW (unimplemented), CALL unknown macro,
    mixed AND/OR, no evidence commands, missing ASSERT_CONSOLE_CLEAN at end.
    path: project-relative path to .playtest file.
    script: inline DSL to lint (mutually exclusive with path).
    Returns: OK or severity-tagged issues (ERROR/WARN/INFO) with file:line."""
    if path and script:
        raise ValueError("path and script are mutually exclusive")
    if not path and not script:
        raise ValueError("path or script required")
    return await _send("lint_playtest", _args(path=path, script=script), timeout=60.0)


async def validate_playtest_aliases(
    defs: str = "Assets/PlaytestDefs/farm_core.defs",
    asset: str = "Assets/Configs/PlaytestConfig.asset",
) -> str:
    """Compare alias .defs text file vs PlaytestConfig.asset. Reports missing/extra/changed.
    defs: project-relative path to .defs file (default: Assets/PlaytestDefs/farm_core.defs).
    asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset).
    Returns 'ok: N aliases in sync' when identical, or a diff report."""
    return await _send("validate_playtest_aliases", _args(defs=defs, asset=asset))


async def sync_playtest_aliases_from_defs(
    defs: str = "Assets/PlaytestDefs/farm_core.defs",
    asset: str = "Assets/Configs/PlaytestConfig.asset",
) -> str:
    """Overwrite PlaytestConfig.asset aliases from a .defs text file.
    defs: project-relative path to .defs file (default: Assets/PlaytestDefs/farm_core.defs).
    asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset).
    Invalidates AliasExpander cache after sync. Not allowed in Play Mode."""
    return await _send("sync_playtest_aliases_from_defs", _args(defs=defs, asset=asset))


async def export_playtest_aliases_to_defs(
    asset: str = "Assets/Configs/PlaytestConfig.asset",
    defs: str = "Assets/PlaytestDefs/farm_core.defs",
) -> str:
    """Export PlaytestConfig.asset aliases to a readable .defs text file.
    asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset).
    defs: project-relative output path (default: Assets/PlaytestDefs/farm_core.defs)."""
    return await _send("export_playtest_aliases_to_defs", _args(asset=asset, defs=defs))


async def lint_playtest_suite(pattern: "str | None" = None,
                              suite_path: "str | None" = None) -> str:
    """Read-only preflight check across multiple .playtest files.
    pattern: glob pattern (e.g. 'Playtests/*.playtest') or comma-separated list.
    suite_path: absolute path to a .suite file (lines = project-relative .playtest paths, # = comment).
    Returns: aggregated lint report, one block per file."""
    if suite_path:
        import pathlib as _pathlib
        file_list = [l.strip() for l in
                     _pathlib.Path(suite_path).read_text(encoding="utf-8").splitlines()
                     if l.strip() and not l.strip().startswith("#")]
    elif not pattern:
        raise ValueError("pattern or suite_path required")
    elif "*" in pattern or "?" in pattern:
        file_list_raw = await _send("list_playtest_files", _args(pattern=pattern), timeout=10.0)
        if file_list_raw.startswith("err:") or file_list_raw == "no files":
            return file_list_raw
        file_list = [f.strip() for f in file_list_raw.strip().split("\n") if f.strip()]
    else:
        file_list = [f.strip() for f in pattern.split(",") if f.strip()]

    if not file_list:
        return "LINT: no files matched"

    results = []
    for filepath in file_list:
        result = await _send("lint_playtest", _args(path=filepath), timeout=60.0)
        results.append(result)

    ok_count = sum(1 for r in results if r.startswith("OK"))
    return f"LINT: {ok_count}/{len(results)} clean\n" + "\n".join(results)


async def resolve_scene_refs(refs: str, fields: str | None = None) -> str:
    """Read-only scene reference resolver.
    refs: comma-separated list of $alias, /path, or t:Type tokens.
    fields: optional comma-separated field names to check existence on matched component.
    Returns one tab-aligned line per ref: OK|MISS|AMB + path + details."""
    return await _send("resolve_scene_refs", _args(refs=refs, fields=fields), timeout=15.0)


async def lint_scene_refs(path: "str | None" = None, snippet: "str | None" = None) -> str:
    """Read-only linter for scene references in DSL scripts or batch commands.
    path: project-relative path to .playtest file.
    snippet: inline DSL or batch commands to lint (mutually exclusive with path).
    Checks: unresolved aliases, embedded aliases, missing objects, ambiguous names.
    Returns: 'OK: no issues' or severity-tagged issues (ERROR/WARN) with file:line:token."""
    if path and snippet:
        raise ValueError("path and snippet are mutually exclusive")
    if not path and not snippet:
        raise ValueError("path or snippet required")
    return await _send("lint_scene_refs", _args(path=path, snippet=snippet), timeout=30.0)


async def runtime_snapshot(type: str, name: "str | None" = None,
                           component: "str | None" = None, compress: bool = False) -> str:
    """Snapshot all runtime objects of a given component type. Returns per-object field dump.
    type: component type name (e.g. 'Rigidbody', 'EnemyController').
    name: optional name substring filter.
    component: component type to serialize (defaults to type).
    compress: strip default-value fields to reduce response size."""
    return await _send("runtime_snapshot", _args(
        type=type, name=name, component=component,
        compress="true" if compress else None))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(resolve_scene_refs)
    mcp.tool(annotations=_RO)(lint_scene_refs)
    mcp.tool(annotations=_RW)(invoke_method)
    mcp.tool(annotations=_RW_IDEM)(wait_until)
    mcp.tool(annotations=_RW)(move_to)
    mcp.tool(annotations=_RO)(query_state)
    mcp.tool(annotations=_RW)(test_step)
    mcp.tool(annotations=_RW)(run_playtest)
    mcp.tool(annotations=_RW)(run_playtest_suite)
    mcp.tool(annotations=_RO)(lint_playtest)
    mcp.tool(annotations=_RO)(lint_playtest_suite)
    mcp.tool(annotations=_RO)(validate_playtest_aliases)
    mcp.tool(annotations=_RW)(sync_playtest_aliases_from_defs)
    mcp.tool(annotations=_RW)(export_playtest_aliases_to_defs)
    mcp.tool(annotations=_RO)(runtime_snapshot)
