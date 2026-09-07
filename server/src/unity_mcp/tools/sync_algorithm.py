"""Pure, stateless sync helpers: pipe-field parsing, deadline-bounded send,
and the SyncContext/BumpFlag data shapes.

Split out of tools/sync.py purely to keep that file under the project's line
budget. Everything here is side-effect-free (no tools.sync module-global
reads) and none of it is monkeypatched by tests -- unlike the algorithm
functions that stay in sync.py (_sync_unity, _await_sync_completion,
_get_errors, _attempt_recovery, _warm_type_cache). Those reference
tools.sync module globals (_RECOVERY_TIMEOUT, _FOCUS_HINT_AFTER,
_attempt_recovery, _run_ladder, ...) by unqualified name, which only
resolves through monkeypatch.setattr(sync, name, ...) if the functions stay
defined in sync.py's own module namespace -- so they are not candidates for
this split.
"""
import asyncio
import time
from collections.abc import Awaitable, Callable  # noqa: TC003
from dataclasses import dataclass
from pathlib import Path

from unity_mcp.utils import parse_pipe_fields


@dataclass
class BumpFlag:
    """Mutable bump-used state for one SyncContext. A plain bool field on a
    frozen SyncContext could not be flipped by _sync_unity, so it lives in
    its own tiny holder instead -- one BumpFlag per caller (module-global
    legacy facade, or one per SyncModule instance) means bump=True from one
    caller can never suppress or race a different caller's own bump."""
    used: bool = False


@dataclass(frozen=True)
class SyncContext:
    """Explicit send + bump-state binding for one sync_unity call chain.

    Threaded through the algorithm in sync.py instead of reading tools.sync
    module globals -- two independently-configured callers (two SyncModule
    instances, or interleaved async calls sharing one event loop) never
    cross-contaminate each other's Unity connection this way. `args` is not
    carried here: the algorithm never builds wire args through a factory,
    only inline dict literals, so it has nothing to consume.
    """
    send: Callable[..., Awaitable[str]]
    bump: BumpFlag


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


def _package_json_path() -> Path | None:
    pkg = Path(__file__).resolve().parents[4] / 'unity-plugin' / 'package.json'
    return pkg if pkg.exists() else None
