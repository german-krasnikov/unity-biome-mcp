"""Runtime Play Mode tools — blocked outside Play Mode by Unity guard."""
import asyncio
import re

from ..sampling import sampling_service as _sampling
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._annotations import RW_IDEM as _RW_IDEM
from ._common import bind
from .editor_state import parse_editor_field as _editor_field

_send = None
_args = None

_TCP_POLL_BUFFER = 5.0
_TCP_STEP_BUFFER = 10.0
_TCP_PLAYTEST_BUFFER = 20.0
_PLAY_STATE_POLLS = 15
_PLAY_STATE_POLL_INTERVAL = 1.0


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
                       fresh: bool = False,
                       before_hook: "str | None" = None,
                       after_hook: "str | None" = None) -> str:
    """[Play Mode] Execute a playtest DSL script. Returns structured report (for NUnit tests, use `run_tests`).
    Commands: MOVE TO x,y,z | WAIT n | WAIT_UNTIL query op value | ASSERT query op value |
    ASSERT_CONSOLE_CLEAN [IGNORE "pat"] | SNAPSHOT queries | INVOKE path comp method args |
    SET path comp field value | LOG msg | TIMESCALE n | ASSERT_CONSERVED SUM a+b OVER t |
    ASSERT_CTA VISIBLE|CLICKABLE | VAL name query | TELEPORT path x,y,z |
    ASSERT_BATCH...END | ASSERT_NEAR pathA pathB dist | INVARIANT query op value |
    SIMULATE name [DURATION n] [TIMESCALE n] | MONITOR name | TRACE_FLOW FROM a TO b FIELD f |
    CAPTURE label query | ASSERT_CAPTURED label INCREASED|DECREASED.
    defs: inline VAL definitions prepended to script.
    abort_on_fail=True: stop after the first failed step or automatic console failure; skip all remaining steps including teardown."""
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
            before_hook=before_hook, after_hook=after_hook,
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
            fresh=_fresh,
            before_hook=before_hook, after_hook=after_hook),
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


def _editor_command_error(action: str, response: str | None) -> str | None:
    """Return why an editor lifecycle command is not proven successful."""
    text = (response or "").strip()
    if not text:
        return f"editor {action} returned an empty response"
    lowered = text.lower()
    if lowered == "compile_pending" or lowered == "timeout" or lowered.startswith((
        "err:", "error:", "fail:", "failed:", "blocked:", "timeout:",
    )):
        return f"editor {action} failed: {text}"
    return None


async def _read_play_state() -> bool:
    """Read a canonical editor state; malformed state is not evidence."""
    state = await _send("editor", _args(action="state"), timeout=5.0)
    playing = _editor_field(state, "playing")
    if playing is None or playing.lower() not in ("true", "false"):
        raise RuntimeError(f"editor state missing playing:true|false: {state!r}")
    return playing.lower() == "true"


async def _wait_for_play_state(expected: bool, action: str) -> None:
    """Poll boundedly until Unity proves the requested Play Mode state."""
    last_state = None
    for attempt in range(_PLAY_STATE_POLLS):
        state = await _send("editor", _args(action="state"), timeout=5.0)
        last_state = state
        playing = _editor_field(state, "playing")
        if playing is None or playing.lower() not in ("true", "false"):
            raise RuntimeError(f"editor state missing playing:true|false: {state!r}")
        if (playing.lower() == "true") == expected:
            return
        if attempt + 1 < _PLAY_STATE_POLLS:
            await asyncio.sleep(_PLAY_STATE_POLL_INTERVAL)
    target = "Play Mode" if expected else "Edit Mode"
    raise RuntimeError(
        f"editor {action} did not reach {target} after {_PLAY_STATE_POLLS} polls; "
        f"last state: {last_state!r}"
    )


async def _transition_play_state(expected: bool) -> None:
    """Request and then prove a Play/Edit Mode transition."""
    action = "play" if expected else "stop"
    timeout = 5.0 if expected else 10.0
    response = await _send("editor", _args(action=action), timeout=timeout)
    error = _editor_command_error(action, response)
    if error:
        raise RuntimeError(error)
    await _wait_for_play_state(expected, action)


def _is_playtest_pass(result: str) -> bool:
    """Require a non-empty, complete PLAYTEST ratio and no failure signals."""
    first_line = (result or "").splitlines()[0] if result else ""
    match = re.match(r"PLAYTEST:\s*(\d+)\s*/\s*(\d+)\b", first_line)
    if not match:
        return False
    passed, total = (int(match.group(1)), int(match.group(2)))
    if total <= 0 or passed != total:
        return False
    return not re.search(r"\b(?:FAIL|ERROR|CONSOLE_ERR|BLOCKED|TIMEOUT)\b", result)


async def run_playtest_suite(
    pattern: "str | None" = None,
    suite_path: "str | None" = None,
    timeout_per_test: float = 120.0,
    stop_on_fail: bool = False,
    stop_after: bool = True,
    auto_play: bool = False,
    restart_between: bool = False,
) -> str:
    """Run multiple .playtest files sequentially and return a compact matrix.
    Side effects: auto_play/restart_between may enter or restart Play Mode;
    stop_after exits Play Mode. No confirmation is requested.
    pattern: glob pattern (e.g. 'Playtests/*.playtest'), comma-separated list,
             or newline-separated list of project-relative paths.
    suite_path: absolute path to a .suite file (lines = project-relative .playtest paths, # = comment).
    Exactly one of pattern or suite_path must be provided.
    stop_on_fail=True: abort suite after first failure.
    stop_after=True: exit Play Mode when suite completes.
    auto_play=True: enter Play Mode automatically if not already playing.
    restart_between=True: stop+play between each file to reset runtime state;
    with auto_play=True, also resets an already-running editor before file one.
    Lifecycle commands must return successfully and reach their observed state.
    A failed transition stops the suite and is reported as a failed row.
    Empty matches return a failing SUITE: 0/0 report.
    Output: SUITE: X/Y passed (Zs) + per-file line + full failure details."""
    if pattern and suite_path:
        raise ValueError("pattern and suite_path are mutually exclusive")
    if not pattern and not suite_path:
        raise ValueError("pattern or suite_path required")

    import time as _time
    results = []
    suite_start = _time.monotonic()
    empty_reason = None
    primary_error = None

    try:
        if auto_play:
            try:
                playing = await _read_play_state()
                # restart_between promises an isolated first file too when the
                # suite owns Play Mode and Unity was already running.
                if restart_between and playing:
                    await _transition_play_state(False)
                    playing = False
                if not playing:
                    await _transition_play_state(True)
            except Exception as exc:
                raw = f"PLAYTEST: 0/1 ERROR: startup failed: {type(exc).__name__}: {exc}"
                results.append(("<suite startup>", raw, 0.0, False))

        if not results:
            if suite_path:
                import pathlib as _pathlib
                file_list = [
                    line.strip()
                    for line in _pathlib.Path(suite_path).read_text(
                        encoding="utf-8"
                    ).splitlines()
                    if line.strip() and not line.strip().startswith("#")
                ]
                if not file_list:
                    empty_reason = "no files in suite"
            elif "*" in pattern or "?" in pattern:
                file_list_raw = await _send(
                    "list_playtest_files", _args(pattern=pattern), timeout=10.0
                )
                if file_list_raw.lower().startswith(("err:", "error:")):
                    empty_reason = file_list_raw.strip()
                    file_list = []
                elif file_list_raw.strip().lower() == "no files":
                    empty_reason = "no files matched"
                    file_list = []
                else:
                    file_list = [
                        file_path.strip()
                        for file_path in file_list_raw.strip().split("\n")
                        if file_path.strip()
                    ]
            else:
                sep = "," if "," in pattern else "\n"
                file_list = [
                    file_path.strip()
                    for file_path in pattern.split(sep)
                    if file_path.strip()
                ]

            if not file_list and empty_reason is None:
                empty_reason = "no files matched"

            if empty_reason is None:
                for filepath in file_list:
                    if restart_between and results:  # not before first file
                        try:
                            await _transition_play_state(False)
                            await _transition_play_state(True)
                        except Exception as exc:
                            raw = (
                                "PLAYTEST: 0/1 ERROR: restart failed: "
                                f"{type(exc).__name__}: {exc}"
                            )
                            results.append((filepath, raw, 0.0, False))
                            break
                    t0 = _time.monotonic()
                    try:
                        raw = await _send(
                            "run_playtest",
                            _args(
                                path=filepath,
                                timeout=str(timeout_per_test),
                                abort_on_fail=None,
                            ),
                            timeout=timeout_per_test + _TCP_PLAYTEST_BUFFER,
                        )
                    except Exception as exc:
                        # P-336: capture network/timeout exceptions as failed results
                        raw = f"PLAYTEST: 0/0 ERROR: {type(exc).__name__}: {exc}"
                    elapsed = _time.monotonic() - t0
                    passed = _is_playtest_pass(raw)
                    results.append((filepath, raw, elapsed, passed))
                    if stop_on_fail and not passed:
                        break
    except BaseException as exc:
        primary_error = exc

    cleanup_error = None
    if stop_after:
        try:
            await _transition_play_state(False)
        except Exception as exc:
            cleanup_error = exc

    if primary_error is not None:
        if cleanup_error is not None and hasattr(primary_error, "add_note"):
            primary_error.add_note(
                "Play Mode cleanup also failed: "
                f"{type(cleanup_error).__name__}: {cleanup_error}"
            )
        raise primary_error

    if cleanup_error is not None:
        raw = (
            "PLAYTEST: 0/1 ERROR: cleanup failed: "
            f"{type(cleanup_error).__name__}: {cleanup_error}"
        )
        results.append(("<suite cleanup>", raw, 0.0, False))

    elapsed = _time.monotonic() - suite_start
    if empty_reason is not None:
        cleanup_detail = ""
        if cleanup_error is not None:
            cleanup_detail = (
                "\nFAIL suite cleanup: "
                f"{type(cleanup_error).__name__}: {cleanup_error}"
            )
        return (
            f"SUITE: 0/0 passed ({elapsed:.1f}s)\n"
            f"FAIL suite input: {empty_reason}{cleanup_detail}"
        )

    return _format_suite_report(results, elapsed)


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
    """Static validation for playtest DSL. Read-only — no scene changes. Returns warnings list.
    Checks: $alias resolution, deprecated ALIAS, unimplemented steps, missing ASSERT_CONSOLE_CLEAN.
    path: project-relative path to .playtest file.
    script: inline DSL (mutually exclusive with path)."""
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
    mcp.tool(annotations=_RW_IDEM)(lint_playtest)
    mcp.tool(annotations=_RO)(lint_playtest_suite)
    mcp.tool(annotations=_RO)(validate_playtest_aliases)
    mcp.tool(annotations=_RW)(sync_playtest_aliases_from_defs)
    mcp.tool(annotations=_RW)(export_playtest_aliases_to_defs)
    mcp.tool(annotations=_RO)(runtime_snapshot)
