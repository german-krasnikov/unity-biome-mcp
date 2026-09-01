"""sync_unity tool — unified Unity reload API.

Sequence (ref §13):
  1. bump_version (if bump=True)
  2. C# sync → sync_ack|epoch=N|will_compile=bool
  3. If will_compile=false → fast path (< 5s)
  4. Poll sync_status until epoch=N|state=ready (or failed/timeout)
  5. Reconnect transparently on DomainReloadError
  6. Return errors or "sync clean"
"""
import asyncio
import contextlib
import time
from pathlib import Path

from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp import editor_log
from unity_mcp.bridge import DomainReloadError
from unity_mcp.constants import SESSION_TIMEOUT as _DEFAULT_TIMEOUT
from unity_mcp.lockfile import read_reload_port
from unity_mcp.tools.reload_ladder import _send_with_fallback, make_reload_send
from unity_mcp.tools.reload_ladder import run_ladder as _run_ladder
from unity_mcp.utils import parse_pipe_fields

from ._common import bind

_send = None

_POLL_INTERVAL = 1.0
_FOCUS_HINT_AFTER = 15.0  # see backgrounded-editor hint in sync_unity
_RECOVERY_TIMEOUT = 30.0  # max wait for MVID delta after force_refresh
_RECOVERY_POLL    = 1.0   # interval between MVID re-checks during recovery

async def _timed_send(send, cmd: str, args: dict, deadline: float) -> str:
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        raise TimeoutError(f"deadline passed before {cmd}")
    return await asyncio.wait_for(send(cmd, args), timeout=remaining)


# D2: bump circuit-breaker — one bump per connection session
_bump_used: bool = False


def _reset_bump_used() -> None:
    global _bump_used
    _bump_used = False


async def _attempt_recovery(send, mvid_pre: str, send_reload=None, deadline: float = 0) -> str | None:
    """Fire force_refresh, poll MVID up to _RECOVERY_TIMEOUT seconds.

    Returns None if MVID delta detected (healed), else the REIMPORT-NEEDED verdict.
    Called at most once per sync_unity invocation — no recursion.
    send_reload: optional reload-channel fallback when main TCP is down.
    deadline: outer deadline from sync_unity; 0 = use _RECOVERY_TIMEOUT.
    """
    try:
        await _send_with_fallback(send, send_reload, "force_refresh", {})
    except (ConnectionError, OSError):
        return "REIMPORT-NEEDED: TCP unreachable during recovery"

    recovery_deadline = min(deadline, time.monotonic() + _RECOVERY_TIMEOUT) if deadline else time.monotonic() + _RECOVERY_TIMEOUT
    while time.monotonic() < recovery_deadline:
        await asyncio.sleep(_RECOVERY_POLL)
        try:
            status = await _timed_send(send, "sync_status", {}, recovery_deadline)
        except TimeoutError:
            break
        except (ConnectionError, DomainReloadError):
            continue
        stamp_post = _parse_stamp(status)
        if stamp_post:
            mvid_post = stamp_post.partition(":")[0]
            if mvid_post != mvid_pre:
                return None  # HEALED — MVID changed
    # MCP091-009: no-IL-change compile → MVID legitimately stable.
    # Only return None if compile actually completed (state=ready/idle) AND no errors.
    # Stuck compile (state=compiling) → still REIMPORT-NEEDED.
    try:
        final_deadline = max(deadline, time.monotonic() + 5.0) if deadline else time.monotonic() + 5.0
        final_status = await _timed_send(send, "sync_status", {}, final_deadline)
        _, final_state, _ = _parse_status(final_status)
        if final_state in ("compiling", "reloading"):
            return f"REIMPORT-NEEDED: focus Unity (stale MVID {mvid_pre})"
        errors = await editor_log.get_corroborated_errors(send)
        if not errors:
            return None  # compile completed, no errors = no-op compile, domain is live
    except Exception:
        pass
    return f"REIMPORT-NEEDED: focus Unity (stale MVID {mvid_pre})"


# Path to plugin package.json — resolved relative to this file's repo root.
# None when running as installed package (no sibling unity-plugin dir).
def _package_json_path() -> Path | None:
    repo_root = Path(__file__).resolve().parents[4]
    pkg = repo_root / "unity-plugin" / "package.json"
    return pkg if pkg.exists() else None


def _parse_ack(ack: str) -> tuple[int, bool]:
    """Parse 'sync_ack|epoch=N|will_compile=bool' → (epoch, will_compile)."""
    parts = parse_pipe_fields(ack)
    epoch = int(parts.get("epoch", "0"))
    will_compile = parts.get("will_compile", "false").lower() == "true"
    return epoch, will_compile


def _parse_status(status: str) -> tuple[int, str, str]:
    """Parse 'epoch=N|state=S[|err=...]' → (epoch, state, err)."""
    parts = parse_pipe_fields(status)
    epoch = int(parts.get("epoch", "0"))
    state = parts.get("state", "idle")
    err   = parts.get("err", "")
    return epoch, state, err


def _parse_stamp(status: str) -> str:
    """Extract stamp= field from sync_status response. Returns '' if absent."""
    return parse_pipe_fields(status).get("stamp", "")


async def sync_unity(
    resolve: bool = False,
    bump: bool = False,
    timeout: float = _DEFAULT_TIMEOUT,
) -> str:
    """Unified Unity reload: trigger Refresh (+ optional Resolve), wait for new code to live.

    resolve=True: call Client.Resolve() first (use after package.json change).
    bump=True: atomically increment plugin patch version BEFORE sync, implies resolve=True.
    Returns: 'sync clean' / compile errors / timeout message.
    """
    global _bump_used
    if _send is None:
        raise ToolError("sync_unity requires a Unity connection (no bridge)")

    # D2: bump circuit-breaker
    if bump and _bump_used:
        return "STOP: bump already used this session; investigate compile errors instead of re-bumping"

    # Step 1: bump version if requested (before sync so C# picks up the new package.json)
    if bump:
        pkg = _package_json_path()
        if pkg is None:
            raise ToolError("bump=True requires unity-plugin/package.json (not found — standalone install?)")
        from unity_mcp.scripts.bump_version import bump_patch
        bump_patch(pkg)
        resolve = True  # bump implies resolve
        _bump_used = True

    # Build reload-channel fallback (None if reload plugin absent)
    _reload_port = read_reload_port()
    _send_reload = make_reload_send(_reload_port) if _reload_port else None

    # Compute deadline once — all inner sends respect it
    deadline = time.monotonic() + timeout

    # D3: read stamp_pre before triggering sync
    try:
        _pre_raw = await _send("sync_status", {})
        stamp_pre = _parse_stamp(_pre_raw)
    except (ConnectionError, OSError, TimeoutError):
        stamp_pre = ""

    # Step 2: trigger sync
    try:
        sync_args = {"resolve": "true"} if resolve else {}
        ack = await _send("sync", sync_args)
    except ConnectionError as e:
        raise ToolError(f"Unity unreachable: {e}") from e

    try:
        epoch, will_compile = _parse_ack(ack)
    except (ValueError, KeyError, IndexError):
        epoch, will_compile = 0, True  # conservative

    # Step 3: fast path — nothing to compile
    if not will_compile:
        errors = await _get_errors()
        if not errors:
            await _warm_type_cache()
            return "sync clean (no compile needed)"
        return errors

    # Step 4-5: poll until epoch match + state=ready/failed
    started = time.monotonic()

    while True:
        if time.monotonic() > deadline:
            return (f"STOP: reload did not converge in {timeout:.0f}s — Unity may be unfocused "
                    "or compile is wedged; check get_compile_errors")

        try:
            status = await _timed_send(_send, "sync_status", {}, deadline)
        except TimeoutError:
            return (f"STOP: reload did not converge in {timeout:.0f}s — Unity may be unfocused "
                    "or compile is wedged; check get_compile_errors")
        except (ConnectionError, DomainReloadError):
            await asyncio.sleep(_POLL_INTERVAL)
            continue

        try:
            s_epoch, state, err = _parse_status(status)
        except (ValueError, KeyError):
            await asyncio.sleep(_POLL_INTERVAL)
            continue

        # macOS/Unity 6 defers compilation while the editor is backgrounded:
        # dur stays 0.0 (compile never started) yet C# can't self-heal because
        # isCompiling is held true. Emit REIMPORT-NEEDED so agent can act.
        if (state == "compiling" and "dur=0.0" in status
                and time.monotonic() - started > _FOCUS_HINT_AFTER):
            mvid = stamp_pre.partition(":")[0] if stamp_pre else "unknown"
            recovery = await _attempt_recovery(_send, mvid, _send_reload, deadline=deadline)
            if recovery is None:
                await _warm_type_cache()
                return "sync clean"  # healed via force_refresh
            # BLOCKER1: T1 failed → escalate to run_ladder T2-T5
            return await _run_ladder(_send, send_reload=_send_reload, start_tier=2)

        # R-3: epoch mismatch → stale status, keep polling
        if s_epoch != epoch:
            await asyncio.sleep(_POLL_INTERVAL)
            continue

        if state == "failed":
            # R-2: compile failed → no reload will happen, report errors
            errors = await _get_errors()
            if errors:
                return errors
            return f"compile failed: {err}" if err else "compile failed"

        if state == "ready":
            stamp_post = _parse_stamp(status)
            if stamp_post and stamp_pre:
                # P5: compare MVID halves only (partition on ':').
                # mtime ticks can change (CleanBuildCache IN-93874) without IL change.
                mvid_pre  = stamp_pre.partition(":")[0]
                mvid_post = stamp_post.partition(":")[0]
                if mvid_pre == mvid_post:
                    if will_compile:
                        # Compile was expected but MVID unchanged → try auto-recovery first.
                        # expected_compile=True gate (A5): only fire when compile was triggered.
                        recovery = await _attempt_recovery(_send, mvid_pre, _send_reload, deadline=deadline)
                        if recovery is None:
                            await _warm_type_cache()
                            return "sync clean"  # healed via force_refresh
                        # BLOCKER1: T1 failed → escalate to run_ladder T2-T5
                        return await _run_ladder(_send, send_reload=_send_reload, start_tier=2)
                    # will_compile=false (cache-hit / no-change): frozen MVID is clean.
                    # expected_compile threading (item 1 / A5): never STALE-DOMAIN here.
                    await _warm_type_cache()
                    return "sync clean (no-op, cache-hit)"
            errors = await _get_errors()
            if not errors:
                await _warm_type_cache()
                return "sync clean"
            return errors

        # state = compiling/reloading/idle — keep polling
        await asyncio.sleep(_POLL_INTERVAL)


async def _get_errors() -> str:
    """Get compile errors from C# and corroborate with editor_log (both-signals gate).

    Delegates to get_corroborated_errors — sentinel-strip lives there (P3 DRY).
    ARC-6: a dead TCP call during error fetch surfaces editor_log.UNITY_UNREACHABLE,
    not "" — this local catch is defense-in-depth in case editor_log.py's own
    (ConnectionError, OSError) guard ever regresses back to a silent "".
    """
    try:
        return await editor_log.get_corroborated_errors(_send)
    except (ConnectionError, OSError):
        return editor_log.UNITY_UNREACHABLE


async def _warm_type_cache() -> None:
    with contextlib.suppress(ConnectionError, OSError):
        await _send("warm_type_cache", {})


def register(mcp, send, args):
    bind(globals(), send, args)
    editor_log.init_corroboration()
    from ._annotations import RW as _RW
    mcp.tool(annotations=_RW)(sync_unity)
