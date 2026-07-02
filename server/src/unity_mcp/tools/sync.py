"""sync_unity tool — unified Unity reload API.

Sequence (ref §13):
  1. bump_version (if bump=True)
  2. C# sync → sync_ack|epoch=N|will_compile=bool
  3. If will_compile=false → fast path (< 5s)
  4. Poll sync_status until epoch=N|state=ready (or failed/timeout)
  5. Reconnect transparently on DomainReloadError
  6. Return errors or "sync clean"
"""
from ._common import bind
import asyncio
import time
from pathlib import Path

from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.bridge import DomainReloadError
from unity_mcp import editor_log
from unity_mcp.constants import SESSION_TIMEOUT as _DEFAULT_TIMEOUT
from unity_mcp.lockfile import read_reload_port
from unity_mcp.tools.reload_ladder import make_reload_send, _send_with_fallback, run_ladder as _run_ladder

_send = None

_POLL_INTERVAL = 1.0
_FOCUS_HINT_AFTER = 15.0  # see backgrounded-editor hint in sync_unity
_RECOVERY_TIMEOUT = 30.0  # max wait for MVID delta after force_refresh
_RECOVERY_POLL    = 1.0   # interval between MVID re-checks during recovery

# D2: bump circuit-breaker — one bump per connection session
_bump_used: bool = False


def _reset_bump_used() -> None:
    global _bump_used
    _bump_used = False


async def _attempt_recovery(send, mvid_pre: str, send_reload=None) -> "str | None":
    """Fire force_refresh, poll MVID up to _RECOVERY_TIMEOUT seconds.

    Returns None if MVID delta detected (healed), else the REIMPORT-NEEDED verdict.
    Called at most once per sync_unity invocation — no recursion.
    send_reload: optional reload-channel fallback when main TCP is down.
    """
    try:
        await _send_with_fallback(send, send_reload, "force_refresh", {})
    except (ConnectionError, OSError):
        return "REIMPORT-NEEDED: TCP unreachable during recovery"

    deadline = time.monotonic() + _RECOVERY_TIMEOUT
    while time.monotonic() < deadline:
        await asyncio.sleep(_RECOVERY_POLL)
        try:
            status = await send("sync_status", {})
        except (ConnectionError, DomainReloadError):
            continue
        stamp_post = _parse_stamp(status)
        if stamp_post:
            mvid_post = stamp_post.partition(":")[0]
            if mvid_post != mvid_pre:
                return None  # HEALED — MVID changed
    return f"REIMPORT-NEEDED: focus Unity (stale MVID {mvid_pre})"


# Path to plugin package.json — resolved relative to this file's repo root.
# None when running as installed package (no sibling unity-plugin dir).
def _package_json_path() -> "Path | None":
    repo_root = Path(__file__).resolve().parents[4]
    pkg = repo_root / "unity-plugin" / "package.json"
    return pkg if pkg.exists() else None


def _parse_ack(ack: str) -> tuple[int, bool]:
    """Parse 'sync_ack|epoch=N|will_compile=bool' → (epoch, will_compile)."""
    parts = {p.split("=", 1)[0]: p.split("=", 1)[1] for p in ack.split("|") if "=" in p}
    epoch = int(parts.get("epoch", "0"))
    will_compile = parts.get("will_compile", "false").lower() == "true"
    return epoch, will_compile


def _parse_status(status: str) -> tuple[int, str, str]:
    """Parse 'epoch=N|state=S[|err=...]' → (epoch, state, err)."""
    parts = {p.split("=", 1)[0]: p.split("=", 1)[1] for p in status.split("|") if "=" in p}
    epoch = int(parts.get("epoch", "0"))
    state = parts.get("state", "idle")
    err   = parts.get("err", "")
    return epoch, state, err


def _parse_stamp(status: str) -> str:
    """Extract stamp= field from sync_status response. Returns '' if absent."""
    parts = {p.split("=", 1)[0]: p.split("=", 1)[1] for p in status.split("|") if "=" in p}
    return parts.get("stamp", "")


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
        new_ver = bump_patch(pkg)
        resolve = True  # bump implies resolve
        _bump_used = True

    # Build reload-channel fallback (None if reload plugin absent)
    _reload_port = read_reload_port()
    _send_reload = make_reload_send(_reload_port) if _reload_port else None

    # D3: read stamp_pre before triggering sync
    try:
        _pre_raw = await _send("sync_status", {})
        stamp_pre = _parse_stamp(_pre_raw)
    except (ConnectionError, OSError, TimeoutError):
        stamp_pre = ""

    # Step 2: trigger sync
    try:
        ack = await _send("sync", {"resolve": "true" if resolve else "false"})
    except ConnectionError as e:
        raise ToolError(f"Unity unreachable: {e}") from e

    try:
        epoch, will_compile = _parse_ack(ack)
    except (ValueError, KeyError, IndexError):
        epoch, will_compile = 0, True  # conservative

    # Step 3: fast path — nothing to compile
    if not will_compile:
        errors = await _get_errors()
        return errors if errors else "sync clean (no compile needed)"

    # Step 4-5: poll until epoch match + state=ready/failed
    deadline = time.monotonic() + timeout
    started = time.monotonic()

    while True:
        if time.monotonic() > deadline:
            return (f"STOP: reload did not converge in {timeout:.0f}s — Unity may be unfocused "
                    "or compile is wedged; check get_compile_errors")

        try:
            status = await _send("sync_status", {})
        except (ConnectionError, DomainReloadError):
            # Domain reload: Unity restarting — wait and retry
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
            recovery = await _attempt_recovery(_send, mvid, _send_reload)
            if recovery is None:
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
                        recovery = await _attempt_recovery(_send, mvid_pre, _send_reload)
                        if recovery is None:
                            return "sync clean"  # healed via force_refresh
                        # BLOCKER1: T1 failed → escalate to run_ladder T2-T5
                        return await _run_ladder(_send, send_reload=_send_reload, start_tier=2)
                    else:
                        # will_compile=false (cache-hit / no-change): frozen MVID is clean.
                        # expected_compile threading (item 1 / A5): never STALE-DOMAIN here.
                        return "sync clean (no-op, cache-hit)"
            errors = await _get_errors()
            return errors if errors else "sync clean"

        # state = compiling/reloading/idle — keep polling
        await asyncio.sleep(_POLL_INTERVAL)


async def _get_errors() -> str:
    """Get compile errors from C# and corroborate with editor_log (both-signals gate).

    Delegates to get_corroborated_errors — sentinel-strip lives there (P3 DRY).
    """
    return await editor_log.get_corroborated_errors(_send)


def register(mcp, send, args):
    bind(globals(), send, args)
    editor_log.init_corroboration()
    from ._annotations import RW as _RW
    mcp.tool(annotations=_RW)(sync_unity)
