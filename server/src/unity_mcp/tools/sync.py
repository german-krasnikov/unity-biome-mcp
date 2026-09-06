"""Bounded sync orchestration. Compiler diagnostics are not source-provenance proof."""
import asyncio
import math
import time
from pathlib import Path

from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp import editor_log
from unity_mcp.constants import SESSION_TIMEOUT as _DEFAULT_TIMEOUT
from unity_mcp.errors import recovery_barrier
from unity_mcp.lockfile import read_reload_port
from unity_mcp.tools.diagnose import _parse_diagnose, _parse_dlls, _verdict
from unity_mcp.tools.reload_ladder import _send_with_fallback, make_reload_send, reject_blocked
from unity_mcp.tools.reload_ladder import run_ladder as _run_ladder
from unity_mcp.utils import parse_pipe_fields

from ._common import bind

_send = None
_POLL_INTERVAL = 1.0
_FOCUS_HINT_AFTER = 15.0
_RECOVERY_TIMEOUT = 30.0
_RECOVERY_POLL = 1.0
_bump_used = False


def _reset_bump_used() -> None:
    global _bump_used
    _bump_used = False


async def _timed_send(send, cmd: str, args: dict, deadline: float) -> str:
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        raise TimeoutError(f"deadline passed before {cmd}")
    return await asyncio.wait_for(send(cmd, args), timeout=remaining)


def _parse_ack(ack: str) -> tuple[int, bool]:
    if ack.partition('|')[0] != 'sync_ack':
        raise ValueError(f"unexpected sync ack: {ack!r}")
    fields = parse_pipe_fields(ack)
    if fields.get('will_compile') not in ('true', 'false'):
        raise ValueError('missing or invalid will_compile')
    return int(fields['epoch']), fields['will_compile'] == 'true'


def _parse_status(status: str) -> tuple[int, str, str]:
    fields = parse_pipe_fields(status)
    return int(fields.get('epoch', '0')), fields.get('state', ''), fields.get('err', '')


def _parse_stamp(status: str) -> str:
    return parse_pipe_fields(status).get('stamp', '')


def _status_issue(status: str, expected_epoch: int | None) -> str:
    try:
        epoch, state, error = _parse_status(status)
    except (ValueError, TypeError):
        return 'UNKNOWN: malformed sync status'
    if expected_epoch is not None and epoch != expected_epoch:
        return 'UNKNOWN: sync epoch changed during recovery'
    if state == 'failed' or error:
        return f"compile failed: {error or 'details unavailable'}"
    if state not in ('ready', 'idle', 'compiling', 'reloading'):
        return 'UNKNOWN: missing or unknown sync state'
    return ''


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
    """One force-refresh attempt within the caller budget; no status alone proves source freshness."""
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


def _package_json_path() -> Path | None:
    pkg = Path(__file__).resolve().parents[4] / 'unity-plugin' / 'package.json'
    return pkg if pkg.exists() else None


async def sync_unity(resolve: bool = False, bump: bool = False, timeout: float = _DEFAULT_TIMEOUT) -> str:
    """Refresh Unity and await its matching compile cycle within one timeout.

    resolve=True resolves packages first; bump=True increments the plugin patch
    version once per connection and implies resolve. 'sync clean' means the
    observed cycle completed without captured errors; it is not a hash proof
    that every source file was compiled. Timeout does not cancel Unity effects.
    """
    if not math.isfinite(timeout) or timeout <= 0:
        return 'STOP: timeout must be a finite positive number'
    deadline = time.monotonic() + timeout
    try:
        async with asyncio.timeout_at(deadline):
            return await _sync_unity(resolve, bump, deadline)
    except TimeoutError:
        return f'STOP: reload observation exceeded {timeout:g}s; Unity operation may still be running'


async def _sync_unity(resolve: bool, bump: bool, deadline: float) -> str:
    global _bump_used
    if _send is None:
        raise ToolError('sync_unity requires a Unity connection (no bridge)')
    if bump and _bump_used:
        return 'STOP: bump already used this session; investigate compile errors instead of re-bumping'
    if bump:
        pkg = _package_json_path()
        if pkg is None:
            raise ToolError('bump=True requires unity-plugin/package.json (not found — standalone install?)')
        from unity_mcp.scripts.bump_version import bump_patch
        bump_patch(pkg)
        resolve = True
        _bump_used = True
    port = read_reload_port()
    send_reload = make_reload_send(port) if port else None
    try:
        stamp_pre = _parse_stamp(await _send('sync_status', {}))
    except (ConnectionError, OSError) as exc:
        if recovery_barrier(exc) is not None:
            raise
        stamp_pre = ''
    try:
        ack = await _send('sync', {'resolve': 'true'} if resolve else {})
    except ConnectionError as exc:
        raise ToolError(f'Unity unreachable: {exc}') from exc
    if ack.startswith('blocked|'):
        return 'BLOCKED: ' + parse_pipe_fields(ack).get('reason', 'Unity rejected sync before dispatch')
    if ack == 'wedged' or ack.startswith('wedged|'):
        return f'GUARD-WEDGED: TriggerSync re-wedge guard fired ({ack}); check get_compile_errors'
    try:
        epoch, will_compile = _parse_ack(ack)
    except (ValueError, KeyError, IndexError):
        return f'STOP: unrecognized sync ack {ack!r} — Unity plugin/server protocol mismatch'
    if not will_compile:
        errors = await _get_errors()
        if errors:
            return errors
        await _warm_type_cache()
        return 'sync clean (no compile needed)'
    started = time.monotonic()
    while True:
        try:
            status = await _timed_send(_send, 'sync_status', {}, deadline)
        except (ConnectionError, OSError) as exc:
            if recovery_barrier(exc) is not None:
                raise
            await asyncio.sleep(_POLL_INTERVAL)
            continue
        try:
            current_epoch, state, error = _parse_status(status)
        except (ValueError, KeyError):
            return 'UNKNOWN: malformed sync status'
        if current_epoch != epoch:
            await asyncio.sleep(_POLL_INTERVAL)
            continue
        if state == 'failed' or error:
            return await _get_errors() or f'compile failed: {error or "details unavailable"}'
        if state not in ('compiling', 'reloading', 'idle', 'ready'):
            return 'UNKNOWN: missing or unknown sync state'
        if state == 'ready':
            # The aggregate stamp covers plugin assemblies, not user assemblies.
            # A matching ready cycle uses current diagnostics even when IL is unchanged.
            errors = await _get_errors()
            if errors:
                return errors
            await _warm_type_cache()
            return 'sync clean'
        stalled = state == 'compiling' and 'dur=0.0' in status and time.monotonic() - started > _FOCUS_HINT_AFTER
        if stalled:
            outcome = await _attempt_recovery(_send, stamp_pre.partition(':')[0] or 'unknown', send_reload,
                                               deadline=deadline, expected_epoch=epoch)
            if outcome:
                if outcome.startswith('REIMPORT-NEEDED'):
                    return await _run_ladder(_send, send_reload=send_reload, start_tier=2, deadline=deadline)
                return outcome
            await _warm_type_cache()
            return 'sync clean'
        await asyncio.sleep(_POLL_INTERVAL)


async def _warm_type_cache() -> None:
    try:
        await _send('warm_type_cache', {})
    except (ConnectionError, OSError) as exc:
        if recovery_barrier(exc) is not None:
            raise


def register(mcp, send, args):
    bind(globals(), send, args)
    editor_log.init_corroboration()
    from ._annotations import RW as _RW
    mcp.tool(annotations=_RW)(sync_unity)
