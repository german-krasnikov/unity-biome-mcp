"""Bounded sync orchestration. Compiler diagnostics are not source-provenance proof."""
import asyncio
import math
import time
from collections.abc import Awaitable, Callable  # noqa: TC003

from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp import editor_log
from unity_mcp.constants import SESSION_TIMEOUT as _DEFAULT_TIMEOUT
from unity_mcp.constants import SYNC_COMPILE_GUARD_TEXT
from unity_mcp.errors import recovery_barrier
from unity_mcp.lockfile import read_reload_port
from unity_mcp.tools.diagnose import _parse_diagnose, _parse_dlls, _verdict
from unity_mcp.tools.reload_ladder import _send_with_fallback, make_reload_send, reject_blocked
from unity_mcp.tools.reload_ladder import run_ladder as _run_ladder
from unity_mcp.tools.sync_algorithm import (  # noqa: F401 -- re-exported for existing importers
    BumpFlag,
    SyncContext,
    _package_json_path,
    _parse_ack,
    _parse_stamp,
    _parse_status,
    _status_issue,
    _timed_send,
)
from unity_mcp.utils import parse_pipe_fields

_send = None
_POLL_INTERVAL = 1.0
_FOCUS_HINT_AFTER = 15.0
_RECOVERY_TIMEOUT = 30.0
_RECOVERY_POLL = 1.0
_bump_used = False


def _reset_bump_used() -> None:
    global _bump_used
    _bump_used = False


async def _get_errors(send=None) -> str:
    """Observe compiler state at the same error check; never default it to unknown/idle."""
    send = send or _send
    try:
        state = (await send('compile_status', {})).partition('|')[0].strip()
        errors = await editor_log.get_corroborated_errors(send, compile_status=state)
    except (ConnectionError, OSError) as exc:
        if recovery_barrier(exc) is not None:
            raise
        return editor_log.UNITY_UNREACHABLE
    if state == 'idle-failed':
        return errors if 'error CS' in errors else 'compile failed (details unavailable)'
    if errors:
        return 'UNKNOWN: ' + errors if '[warn:' in errors and 'error CS' not in errors else errors
    if state != 'idle':
        return f'UNKNOWN: compiler state is {state or "unavailable"}'
    try:
        fields = _parse_diagnose(await send('diagnose', {}))
    except (ConnectionError, OSError) as exc:
        if recovery_barrier(exc) is not None:
            raise
        return editor_log.UNITY_UNREACHABLE
    verdict = _verdict(fields)
    if (verdict.startswith(('FAIL:', 'BUILD-FAILED-WEDGE')) or fields.reload_failed
            or fields.compile.startswith(('compiling', 'reloading'))
            or fields.sync_state in ('failed', 'compiling', 'reloading')
            or fields.iscompiling or fields.cn_active or fields.is_really_compiling):
        return verdict  # later failure/busy evidence cannot be discarded by the source check
    # The C# adapter's exact stale token means a known checksum/output mismatch.
    # Unknown PDB coverage is advisory; this final check must not block repair.
    if any(status == 'stale' for _, status in _parse_dlls(fields.dlls)):
        return 'FAIL:stale-dll'
    return ''


async def _attempt_recovery(send, mvid_pre: str, send_reload=None, deadline: float = 0,
                            expected_epoch: int | None = None) -> str | None:
    """One force-refresh attempt within the caller budget; no status alone proves source freshness.

    Takes send explicitly rather than a SyncContext: it never touches bump
    state, and every caller (the algorithm below via ctx.send, or a direct
    test call) already holds the right binding.
    """
    deadline = deadline or time.monotonic() + _RECOVERY_TIMEOUT + 5
    if time.monotonic() >= deadline:
        return 'STOP: reload deadline exceeded before recovery'
    try:
        async with asyncio.timeout_at(deadline):
            reject_blocked(await _send_with_fallback(send, send_reload, 'force_refresh', {}))
    except (ConnectionError, OSError) as exc:
        if recovery_barrier(exc) is not None:
            raise
        return 'REIMPORT-NEEDED: TCP unreachable during recovery'
    recovery_deadline = min(deadline, time.monotonic() + _RECOVERY_TIMEOUT)
    while time.monotonic() < recovery_deadline:
        await asyncio.sleep(_RECOVERY_POLL)
        try:
            status = await _timed_send(send, 'sync_status', {}, recovery_deadline)
        except TimeoutError:
            break
        except (ConnectionError, OSError) as exc:
            if recovery_barrier(exc) is not None:
                raise
            continue
        issue = _status_issue(status, expected_epoch)
        if issue:
            return issue
        _, state, _ = _parse_status(status)
        stamp = _parse_stamp(status)
        if state in ('ready', 'idle') and stamp and stamp.partition(':')[0] != mvid_pre:
            async with asyncio.timeout_at(deadline):
                return await _get_errors(send) or None
    try:
        status = await _timed_send(send, 'sync_status', {}, deadline)
        issue = _status_issue(status, expected_epoch)
        if issue:
            return issue
        if _parse_status(status)[1] not in ('ready', 'idle'):
            return f'REIMPORT-NEEDED: focus Unity (stale MVID {mvid_pre})'
        async with asyncio.timeout_at(deadline):
            return await _get_errors(send) or None
    except (ConnectionError, OSError) as exc:
        if recovery_barrier(exc) is not None:
            raise
        return f'REIMPORT-NEEDED: focus Unity (stale MVID {mvid_pre})'


async def _run_with_context(ctx: SyncContext, resolve: bool, bump: bool, timeout: float) -> str:
    """Timeout-bounded entry point shared by the legacy module-level facade
    (sync_unity below) and SyncModule.sync_unity (sync_module.py). The two
    callers differ only in how ctx is built and, for the legacy facade, in
    writing the bump flag back to a module global afterward -- the algorithm
    itself never reads tools.sync module state once ctx exists.
    """
    if not math.isfinite(timeout) or timeout <= 0:
        return 'STOP: timeout must be a finite positive number'
    if ctx.send is None:
        raise ToolError('sync_unity requires a Unity connection (no bridge)')
    deadline = time.monotonic() + timeout
    try:
        async with asyncio.timeout_at(deadline):
            return await _sync_unity(ctx, resolve, bump, deadline)
    except TimeoutError:
        return f'STOP: reload observation exceeded {timeout:g}s; Unity operation may still be running'


async def sync_unity(resolve: bool = False, bump: bool = False, timeout: float = _DEFAULT_TIMEOUT) -> str:
    """Refresh Unity and await its matching compile cycle within one timeout.

    Legacy adapter: builds a SyncContext from this module's own _send/
    _bump_used globals and writes the (possibly updated) bump flag back to
    _bump_used after the call -- kept for callers/tests that still bind
    Unity via unity_mcp.tools.sync._send directly. The production route is
    SyncModule (tools/sync_module.py), which builds its own SyncContext from
    per-instance state and never touches these globals.

    resolve=True resolves packages first; bump=True increments the plugin patch
    version once per connection and implies resolve. 'sync clean' means the
    observed cycle completed without captured errors; it is not a hash proof
    that every source file was compiled. Timeout does not cancel Unity effects.
    """
    global _bump_used
    ctx = SyncContext(send=_send, bump=BumpFlag(_bump_used))
    try:
        return await _run_with_context(ctx, resolve, bump, timeout)
    finally:
        _bump_used = ctx.bump.used


async def _sync_unity(ctx: SyncContext, resolve: bool, bump: bool, deadline: float) -> str:
    if bump and ctx.bump.used:
        return 'STOP: bump already used this session; investigate compile errors instead of re-bumping'
    if bump:
        pkg = _package_json_path()
        if pkg is None:
            raise ToolError('bump=True requires unity-plugin/package.json (not found — standalone install?)')
        from unity_mcp.scripts.bump_version import bump_patch
        bump_patch(pkg)
        resolve = True
        ctx.bump.used = True
    port = read_reload_port()
    send_reload = make_reload_send(port) if port else None
    try:
        stamp_pre = _parse_stamp(await ctx.send('sync_status', {}))
    except (ConnectionError, OSError) as exc:
        if recovery_barrier(exc) is not None:
            raise
        stamp_pre = ''
    try:
        ack = await ctx.send('sync', {'resolve': 'true'} if resolve else {})
    except ConnectionError as exc:
        raise ToolError(f'Unity unreachable: {exc}') from exc
    except ToolError as exc:
        if str(exc) != SYNC_COMPILE_GUARD_TEXT:
            raise
        # Unity was already compiling when 'sync' arrived (headed auto-refresh or a
        # package resolve raced us). 'sync' is RW, not retry-safe, so do not retry
        # it -- just observe the already-running cycle through the same wait+verdict
        # path a normal will_compile=true ack takes. epoch=None: we have no ack, so
        # adopt whatever epoch sync_status first reports as ours.
        return await _await_sync_completion(ctx, None, deadline, stamp_pre, send_reload)
    if ack.startswith('blocked|'):
        return 'BLOCKED: ' + parse_pipe_fields(ack).get('reason', 'Unity rejected sync before dispatch')
    if ack == 'wedged' or ack.startswith('wedged|'):
        return f'GUARD-WEDGED: TriggerSync re-wedge guard fired ({ack}); check get_compile_errors'
    try:
        epoch, will_compile = _parse_ack(ack)
    except (ValueError, KeyError, IndexError):
        return f'STOP: unrecognized sync ack {ack!r} — Unity plugin/server protocol mismatch'
    if not will_compile:
        errors = await _get_errors(ctx.send)
        if errors:
            return errors
        await _warm_type_cache(ctx.send)
        return 'sync clean (no compile needed)'
    return await _await_sync_completion(ctx, epoch, deadline, stamp_pre, send_reload)


async def _await_sync_completion(ctx: SyncContext, epoch: int | None, deadline: float, stamp_pre: str,
                                  send_reload: Callable[..., Awaitable[str]] | None) -> str:
    """Poll sync_status to a terminal state, then run the shared errors+freshness verdict.

    Shared tail for both a normal 'sync_ack|...|will_compile=true' response and a
    compile-guard hit on the 'sync' send itself (see _sync_unity). epoch=None means
    no ack was received (guard hit): the first sync_status read's epoch is adopted
    as ours, so a cycle already in flight -- even one that finishes as 'ready'
    before our first poll -- is still run through the verdict path below rather
    than being treated as a foreign/mismatched epoch.
    """
    started = time.monotonic()
    while True:
        try:
            status = await _timed_send(ctx.send, 'sync_status', {}, deadline)
        except (ConnectionError, OSError) as exc:
            if recovery_barrier(exc) is not None:
                raise
            await asyncio.sleep(_POLL_INTERVAL)
            continue
        try:
            current_epoch, state, error = _parse_status(status)
        except (ValueError, KeyError):
            return 'UNKNOWN: malformed sync status'
        if epoch is None:
            epoch = current_epoch
        elif current_epoch != epoch:
            await asyncio.sleep(_POLL_INTERVAL)
            continue
        if state == 'failed' or error:
            return await _get_errors(ctx.send) or f'compile failed: {error or "details unavailable"}'
        if state not in ('compiling', 'reloading', 'idle', 'ready'):
            return 'UNKNOWN: missing or unknown sync state'
        if state == 'ready':
            # The aggregate stamp covers plugin assemblies, not user assemblies.
            # A matching ready cycle uses current diagnostics even when IL is unchanged.
            errors = await _get_errors(ctx.send)
            if errors:
                return errors
            await _warm_type_cache(ctx.send)
            return 'sync clean'
        stalled = state == 'compiling' and 'dur=0.0' in status and time.monotonic() - started > _FOCUS_HINT_AFTER
        if stalled:
            outcome = await _attempt_recovery(ctx.send, stamp_pre.partition(':')[0] or 'unknown', send_reload,
                                               deadline=deadline, expected_epoch=epoch)
            if outcome:
                if outcome.startswith('REIMPORT-NEEDED'):
                    return await _run_ladder(ctx.send, send_reload=send_reload, start_tier=2, deadline=deadline)
                return outcome
            await _warm_type_cache(ctx.send)
            return 'sync clean'
        await asyncio.sleep(_POLL_INTERVAL)


async def _warm_type_cache(send: Callable[..., Awaitable[str]]) -> None:
    try:
        await send('warm_type_cache', {})
    except (ConnectionError, OSError) as exc:
        if recovery_barrier(exc) is not None:
            raise
