"""Code intelligence tools backed by Unity-side Roslyn workspace.

These tools are TIER1 — replace many existing read+grep+recompile patterns:
- compile_preflight: catches typos in 100-300ms vs 30s Unity recompile cycle
- await_compile: block until compile finishes, then return errors (if any)

find_references and semantic_at were removed (S2) — no C# handler was ever
shipped for them, so every call raised ToolError("Command not registered: ...").
Dead wrappers are pure token waste; re-add them if/when a real C# handler ships.
"""
from ._common import bind
import asyncio
import time
from typing import Any

from unity_mcp.utils import parse_pipe_fields

_send = None

_STILL_BUSY_STATES = frozenset({"compiling", "reloading"})


async def compile_preflight(file_path: str, new_content: str) -> str:
    """Validate C# WITHOUT writing/recompiling (Roslyn). Use before writing .cs — catches typos in ~200ms vs 30s recompile.
    file_path: Assets-relative. new_content: full file. Returns OK preflight (ms) / ERR preflight + diagnostics / [ROSLYN UNAVAILABLE]."""
    args: dict[str, Any] = {"file_path": file_path, "new_content": new_content}
    return await _send("compile_preflight", args, timeout=15.0)


def _parse_status(status: str) -> tuple[str, float]:
    """Parse 'state|number' → (state, number). Unknown format → ('idle', 0.0).

    P4: 'idle-never' mapped to non-idle ('idle-never', 0.0) — never compiled ≠ clean.
    """
    try:
        state, val = status.split("|", 1)
        state = state.strip()
        # idle-never: Unity never compiled this session — NOT equivalent to idle/clean
        return state, float(val)
    except (ValueError, AttributeError):
        return "idle", 0.0


def _parse_sync_status(status: str) -> tuple[int, str, str, str]:
    """Parse 'epoch=N|state=S[|dur=X][|stamp=G:T][|err=...]' → (epoch, state, extra, stamp).

    P4: adds stamp field to the return tuple so callers can compare MVIDs.
    """
    try:
        parts = parse_pipe_fields(status)
        epoch = int(parts.get("epoch", "0"))
        state = parts.get("state", "idle")
        extra = parts.get("err", parts.get("dur", ""))
        stamp = parts.get("stamp", "")
        return epoch, state, extra, stamp
    except (ValueError, AttributeError):
        return 0, "idle", "", ""


async def await_compile(timeout: float = 60.0) -> str:
    """Block until Unity finishes compiling + reloading, then return compile errors.
    Use after writing .cs files instead of sleep. Returns errors or 'compile clean (Xs)'.
    Handles domain reload disconnects transparently. timeout=0 → immediate check, no loop.
    Epoch-aware via sync_status when available (+10 from MAJOR-1); falls back to compile_status."""
    async def _get_errors() -> str:
        # Sentinel-strip lives in get_corroborated_errors (P3 DRY).
        from .. import editor_log
        return await editor_log.get_corroborated_errors(_send)

    # timeout=0: single check, no loop
    # G13: only active compile states are "still compiling"; terminal states (idle-failed,
    # idle-never, idle-stale) fall through to _get_errors() which returns the real verdict.
    if timeout == 0:
        try:
            status = await _send("compile_status", {})
            state, _ = _parse_status(status)
            if state in _STILL_BUSY_STATES:
                return "still compiling"
        except ConnectionError:
            pass
        return await _get_errors()

    # Epoch-aware path: try sync_status first; fall back to compile_status if unavailable.
    epoch = None
    stamp_pre = ""
    try:
        raw = await _send("sync_status", {})
        ep, st, _, stamp_pre = _parse_sync_status(raw)
        if ep > 0 and st in ("compiling", "reloading"):
            epoch = ep  # there's a pending sync cycle — wait for this epoch
    except (ConnectionError, Exception):
        pass  # sync_status not available → use compile_status fallback

    deadline = time.monotonic() + timeout

    while True:
        elapsed_total = time.monotonic() - (deadline - timeout)
        if time.monotonic() > deadline:
            errors = await _get_errors()
            msg = f"timeout after {elapsed_total:.1f}s — compile still in progress"
            return f"{msg}\n{errors}" if errors else msg

        if epoch is not None:
            # Epoch-aware: poll sync_status until this epoch is ready/failed/idle
            try:
                raw = await _send("sync_status", {})
                ep, st, err, stamp_post = _parse_sync_status(raw)
            except ConnectionError:
                await asyncio.sleep(1)
                continue

            if ep == epoch:
                if st == "ready" or st == "idle":
                    # P4: MVID gate — unchanged MVID after intended compile → stale domain
                    if stamp_pre and stamp_post:
                        mvid_pre  = stamp_pre.partition(":")[0]
                        mvid_post = stamp_post.partition(":")[0]
                        if mvid_pre == mvid_post:
                            return (
                                "STALE-DOMAIN: stamp unchanged after reload"
                                " — old code may be live"
                            )
                    errors = await _get_errors()
                    return errors if errors else "compile clean (sync)"
                if st == "failed":
                    errors = await _get_errors()
                    return errors if errors else f"compile failed: {err}"
            # still compiling/reloading or epoch mismatch — poll
            await asyncio.sleep(1)
            continue

        # Fallback: compile_status poll (original behavior)
        try:
            status = await _send("compile_status", {})
        except ConnectionError:
            # Domain reload: Unity is restarting — wait and retry
            await asyncio.sleep(1)
            continue

        state, duration = _parse_status(status)

        if state == "idle-failed":
            errors = await _get_errors()
            return errors if errors else f"compile failed ({duration:.1f}s)"

        if state == "idle-never":
            # Cold session — Unity never compiled this session; return immediately
            errors = await _get_errors()
            return errors if errors else "compile clean (never ran this session — idle-never)"

        if state == "idle":
            errors = await _get_errors()
            return errors if errors else f"compile clean ({duration}s)"

        # still compiling — poll
        await asyncio.sleep(1)


def register(mcp, send, args):
    bind(globals(), send, args)
    from .. import editor_log
    editor_log.init_corroboration()
    from ._annotations import RO as _RO
    mcp.tool(annotations=_RO)(compile_preflight)
    mcp.tool(annotations=_RO)(await_compile)
